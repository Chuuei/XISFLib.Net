using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using System.Xml.Linq;

namespace XISFLib;

public sealed class XisfImage
{
    private readonly XisfDataBlock _block;

    private XisfImage(XElement xml, XisfDataSource source, XisfReferenceResolver references, int[] dimensions, int channels, XisfSampleFormat sampleFormat)
    {
        Xml = xml;
        _block = new XisfDataBlock(xml, source);
        Dimensions = dimensions;
        ChannelCount = channels;
        SampleFormat = sampleFormat;
        Id = (string?)xml.Attribute("id");
        ColorSpace = (string?)xml.Attribute("colorSpace") ?? (channels == 1 ? "Gray" : null);
        ImageType = (string?)xml.Attribute("imageType");
        Offset = double.TryParse((string?)xml.Attribute("offset"), NumberStyles.Float, CultureInfo.InvariantCulture, out double offset) ? offset : 0;
        Orientation = (string?)xml.Attribute("orientation") ?? "0";
        Uuid = Guid.TryParseExact((string?)xml.Attribute("uuid"), "D", out Guid uuid) ? uuid : null;
        string storage = (string?)xml.Attribute("pixelStorage") ?? "Planar";
        PixelStorage = storage.Equals("Normal", StringComparison.OrdinalIgnoreCase)
            ? XisfPixelLayout.Interleaved
            : Enum.TryParse(storage, true, out XisfPixelLayout layout) ? layout : throw new XisfFormatException("Invalid pixelStorage attribute.");
        IsBigEndian = string.Equals((string?)xml.Attribute("byteOrder"), "big", StringComparison.OrdinalIgnoreCase);
        double[] bounds = XisfReader.ParseDoubles((string?)xml.Attribute("bounds"));
        if (sampleFormat is XisfSampleFormat.Float32 or XisfSampleFormat.Float64 && bounds.Length != 2)
            throw new XisfFormatException("Real floating-point images require lower and upper bounds.");
        LowerBound = bounds.Length == 2 ? bounds[0] : 0;
        UpperBound = bounds.Length == 2 ? bounds[1] : IntegerMaximum(sampleFormat);
        Properties = XisfReader.ParseProperties(xml, source, references);
        FitsKeywords = references.Children(xml, "FITSKeyword").Select(e => new XisfFitsKeyword(XisfReader.RequiredAttribute(e, "name"), (string?)e.Attribute("value") ?? "", (string?)e.Attribute("comment"))).ToList();
        Tables = references.Children(xml, "Table").Select(e => XisfReader.ParseTable(e, source, references)).ToList();
        IccProfile = ReadOptionalBlock(xml, source, references, "ICCProfile");
        Thumbnail = references.Children(xml, "Thumbnail").SingleOrDefault() is { } thumbnail ? Parse(thumbnail, source, references) : null;
        ColorFilterArray = ParseColorFilterArray(xml, references);
        RgbWorkingSpace = ParseRgbWorkingSpace(xml, references);
        DisplayFunction = ParseDisplayFunction(xml, references);
        Resolution = ParseResolution(xml, references);
    }

    public string? Id { get; }
    public IReadOnlyList<int> Dimensions { get; }
    public int ChannelCount { get; }
    public long PixelCount => Dimensions.Aggregate(1L, (total, dimension) => checked(total * dimension));
    public XisfSampleFormat SampleFormat { get; }
    public XisfPixelLayout PixelStorage { get; }
    public string? ColorSpace { get; }
    public string? ImageType { get; }
    public double Offset { get; }
    public string Orientation { get; }
    public Guid? Uuid { get; }
    public bool IsBigEndian { get; }
    public double LowerBound { get; }
    public double UpperBound { get; }
    public IReadOnlyDictionary<string, XisfProperty> Properties { get; }
    public IReadOnlyList<XisfFitsKeyword> FitsKeywords { get; }
    public IReadOnlyList<XisfTable> Tables { get; }
    public byte[]? IccProfile { get; }
    public XisfImage? Thumbnail { get; }
    public XisfColorFilterArray? ColorFilterArray { get; }
    public XisfRgbWorkingSpace? RgbWorkingSpace { get; }
    public XisfDisplayFunction? DisplayFunction { get; }
    public XisfResolution? Resolution { get; }
    public XElement Xml { get; }

    internal static XisfImage Parse(XElement element, XisfDataSource source, XisfReferenceResolver references)
    {
        string[] geometry = XisfReader.RequiredAttribute(element, "geometry").Split(':');
        if (geometry.Length < 2 || geometry.Any(v => !int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out int n) || n <= 0))
            throw new XisfFormatException("Invalid image geometry attribute.");
        if (geometry.Length - 1 > source.Options.MaximumImageDimensions)
            throw new XisfFormatException("The image exceeds the configured dimension-count limit.");
        int[] values = geometry.Select(v => int.Parse(v, CultureInfo.InvariantCulture)).ToArray();
        if (!Enum.TryParse(XisfReader.RequiredAttribute(element, "sampleFormat"), false, out XisfSampleFormat format))
            throw new XisfFormatException("Unsupported image sample format.");
        long pixels = 1;
        foreach (int dimension in values[..^1])
            pixels = checked(pixels * dimension);
        if (pixels > source.Options.MaximumPixelsPerImage)
            throw new XisfFormatException("The image exceeds the configured pixel-count limit.");
        long byteLength = checked(pixels * values[^1] * BytesPerSample(format));
        if (byteLength > source.Options.MaximumDataBlockSize)
            throw new XisfFormatException("The image exceeds the configured data-block size limit.");
        return new XisfImage(element, source, references, values[..^1], values[^1], format);
    }

    public byte[] ReadRawData()
    {
        byte[] data = _block.Read();
        long expected = checked(PixelCount * ChannelCount * BytesPerSample(SampleFormat));
        if (data.LongLength != expected)
            throw new XisfFormatException($"Image data size is {data.LongLength} bytes; expected {expected}.");
        return data;
    }

    public T[] GetSamples<T>(XisfPixelLayout layout = XisfPixelLayout.Planar) where T : INumberBase<T>
    {
        if (SampleFormat is XisfSampleFormat.Complex32 or XisfSampleFormat.Complex64)
            throw new InvalidOperationException("Use GetComplexSamples() for a complex image.");
        byte[] bytes = ReadRawData();
        int count = checked((int)(PixelCount * ChannelCount));
        T[] samples = new T[count];
        int size = BytesPerSample(SampleFormat);
        for (int i = 0; i < count; i++)
            samples[i] = ReadSample<T>(bytes.AsSpan(i * size, size));
        return Reorder(samples, layout);
    }

    public Complex[] GetComplexSamples(XisfPixelLayout layout = XisfPixelLayout.Planar)
    {
        if (SampleFormat is not (XisfSampleFormat.Complex32 or XisfSampleFormat.Complex64))
            throw new InvalidOperationException("The image does not contain complex samples.");
        byte[] bytes = ReadRawData();
        int count = checked((int)(PixelCount * ChannelCount));
        int componentSize = SampleFormat == XisfSampleFormat.Complex32 ? 4 : 8;
        Complex[] samples = new Complex[count];
        for (int i = 0; i < count; i++)
        {
            ReadOnlySpan<byte> sample = bytes.AsSpan(i * componentSize * 2, componentSize * 2);
            samples[i] = new Complex(ReadFloat(sample[..componentSize]), ReadFloat(sample[componentSize..]));
        }
        return Reorder(samples, layout);
    }

    public double[] GetNormalizedSamples(XisfPixelLayout layout = XisfPixelLayout.Planar)
    {
        double[] samples = GetSamples<double>(layout);
        double range = UpperBound - LowerBound;
        if (range == 0) return samples.Select(_ => 0d).ToArray();
        for (int i = 0; i < samples.Length; i++)
            samples[i] = (samples[i] - LowerBound) / range;
        return samples;
    }

    private double ReadReal(ReadOnlySpan<byte> data) => SampleFormat switch
    {
        XisfSampleFormat.UInt8 => data[0],
        XisfSampleFormat.UInt16 => IsBigEndian ? BinaryPrimitives.ReadUInt16BigEndian(data) : BinaryPrimitives.ReadUInt16LittleEndian(data),
        XisfSampleFormat.UInt32 => IsBigEndian ? BinaryPrimitives.ReadUInt32BigEndian(data) : BinaryPrimitives.ReadUInt32LittleEndian(data),
        XisfSampleFormat.UInt64 => IsBigEndian ? BinaryPrimitives.ReadUInt64BigEndian(data) : BinaryPrimitives.ReadUInt64LittleEndian(data),
        XisfSampleFormat.Float32 or XisfSampleFormat.Float64 => ReadFloat(data),
        _ => throw new InvalidOperationException()
    };

    private T ReadSample<T>(ReadOnlySpan<byte> data) where T : INumberBase<T> => SampleFormat switch
    {
        XisfSampleFormat.UInt8 => T.CreateChecked(data[0]),
        XisfSampleFormat.UInt16 => T.CreateChecked(IsBigEndian ? BinaryPrimitives.ReadUInt16BigEndian(data) : BinaryPrimitives.ReadUInt16LittleEndian(data)),
        XisfSampleFormat.UInt32 => T.CreateChecked(IsBigEndian ? BinaryPrimitives.ReadUInt32BigEndian(data) : BinaryPrimitives.ReadUInt32LittleEndian(data)),
        XisfSampleFormat.UInt64 => T.CreateChecked(IsBigEndian ? BinaryPrimitives.ReadUInt64BigEndian(data) : BinaryPrimitives.ReadUInt64LittleEndian(data)),
        XisfSampleFormat.Float32 or XisfSampleFormat.Float64 => T.CreateChecked(ReadFloat(data)),
        _ => throw new InvalidOperationException()
    };

    private double ReadFloat(ReadOnlySpan<byte> data)
    {
        if (data.Length == 4)
        {
            int bits = IsBigEndian ? BinaryPrimitives.ReadInt32BigEndian(data) : BinaryPrimitives.ReadInt32LittleEndian(data);
            return BitConverter.Int32BitsToSingle(bits);
        }
        long bits64 = IsBigEndian ? BinaryPrimitives.ReadInt64BigEndian(data) : BinaryPrimitives.ReadInt64LittleEndian(data);
        return BitConverter.Int64BitsToDouble(bits64);
    }

    private T[] Reorder<T>(T[] samples, XisfPixelLayout requested)
    {
        if (requested == PixelStorage || ChannelCount == 1) return samples;
        int pixels = checked((int)PixelCount);
        T[] result = new T[samples.Length];
        for (int pixel = 0; pixel < pixels; pixel++)
        for (int channel = 0; channel < ChannelCount; channel++)
        {
            int planar = channel * pixels + pixel;
            int normal = pixel * ChannelCount + channel;
            if (PixelStorage == XisfPixelLayout.Planar) result[normal] = samples[planar];
            else result[planar] = samples[normal];
        }
        return result;
    }

    private static int BytesPerSample(XisfSampleFormat format) => format switch
    {
        XisfSampleFormat.UInt8 => 1,
        XisfSampleFormat.UInt16 => 2,
        XisfSampleFormat.UInt32 or XisfSampleFormat.Float32 => 4,
        XisfSampleFormat.UInt64 or XisfSampleFormat.Float64 or XisfSampleFormat.Complex32 => 8,
        XisfSampleFormat.Complex64 => 16,
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

    private static double IntegerMaximum(XisfSampleFormat format) => format switch
    {
        XisfSampleFormat.UInt8 => byte.MaxValue,
        XisfSampleFormat.UInt16 => ushort.MaxValue,
        XisfSampleFormat.UInt32 => uint.MaxValue,
        XisfSampleFormat.UInt64 => ulong.MaxValue,
        _ => double.NaN
    };

    private static byte[]? ReadOptionalBlock(XElement parent, XisfDataSource source, XisfReferenceResolver references, string name) =>
        references.Children(parent, name).SingleOrDefault() is { } element ? new XisfDataBlock(element, source).Read() : null;

    private static XisfColorFilterArray? ParseColorFilterArray(XElement parent, XisfReferenceResolver references)
    {
        XElement? e = references.Children(parent, "ColorFilterArray").SingleOrDefault();
        if (e is null) return null;
        string pattern = XisfReader.RequiredAttribute(e, "pattern");
        int width = int.Parse(XisfReader.RequiredAttribute(e, "width"), CultureInfo.InvariantCulture);
        int height = int.Parse(XisfReader.RequiredAttribute(e, "height"), CultureInfo.InvariantCulture);
        if (width <= 0 || height <= 0 || pattern.Length != checked(width * height) || pattern.Any(c => !"0RGBWCMY".Contains(c)))
            throw new XisfFormatException("Invalid color filter array definition.");
        return new XisfColorFilterArray(pattern, width, height, (string?)e.Attribute("name"));
    }

    private static XisfRgbWorkingSpace? ParseRgbWorkingSpace(XElement parent, XisfReferenceResolver references)
    {
        XElement? e = references.Children(parent, "RGBWorkingSpace").SingleOrDefault();
        return e is null ? null : new XisfRgbWorkingSpace(double.TryParse((string?)e.Attribute("gamma"), NumberStyles.Float, CultureInfo.InvariantCulture, out double gamma) ? gamma : 1, XisfReader.ParseBoolean((string?)e.Attribute("sRGB") ?? "false"), XisfReader.ParseDoubles((string?)e.Attribute("x") ?? (string?)e.Attribute("chromaticity")), XisfReader.ParseDoubles((string?)e.Attribute("Y") ?? (string?)e.Attribute("luminance")));
    }

    private static XisfDisplayFunction? ParseDisplayFunction(XElement parent, XisfReferenceResolver references)
    {
        XElement? e = references.Children(parent, "DisplayFunction").SingleOrDefault();
        return e is null ? null : new XisfDisplayFunction(XisfReader.ParseDoubles((string?)e.Attribute("m")), XisfReader.ParseDoubles((string?)e.Attribute("s")), XisfReader.ParseDoubles((string?)e.Attribute("h")), XisfReader.ParseDoubles((string?)e.Attribute("l")), XisfReader.ParseDoubles((string?)e.Attribute("r")));
    }

    private static XisfResolution? ParseResolution(XElement parent, XisfReferenceResolver references)
    {
        XElement? e = references.Children(parent, "Resolution").SingleOrDefault();
        return e is null ? null : new XisfResolution(double.Parse(XisfReader.RequiredAttribute(e, "horizontal"), CultureInfo.InvariantCulture), double.Parse(XisfReader.RequiredAttribute(e, "vertical"), CultureInfo.InvariantCulture), (string?)e.Attribute("unit") ?? "inch");
    }
}