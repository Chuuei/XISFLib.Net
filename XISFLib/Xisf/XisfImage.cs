using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using System.Xml.Linq;

namespace XISFLib;

/// <summary>A parsed image whose samples are decoded on demand from its source.</summary>
public sealed class XisfImage
{
    private readonly XisfDataBlock _block;
    internal XisfDataSource Source { get; }

    private XisfImage(XElement xml, XisfDataSource source, XisfReferenceResolver references, int[] dimensions, int channels, XisfSampleFormat sampleFormat)
    {
        XmlCore = xml;
        Source = source;
        _block = new XisfDataBlock(xml, source);
        Dimensions = Array.AsReadOnly(dimensions);
        ChannelCount = channels;
        SampleFormat = sampleFormat;
        Id = (string?)xml.Attribute("id");
        ColorSpace = (string?)xml.Attribute("colorSpace") ?? "Gray";
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
        Properties = new System.Collections.ObjectModel.ReadOnlyDictionary<string, XisfProperty>(XisfReader.ParseProperties(xml, source, references));
        FitsKeywords = references.Children(xml, "FITSKeyword").Select(e => new XisfFitsKeyword(XisfReader.RequiredAttribute(e, "name"), (string?)e.Attribute("value") ?? "", (string?)e.Attribute("comment"))).ToList().AsReadOnly();
        Tables = references.Children(xml, "Table").Select(e => XisfReader.ParseTable(e, source, references)).ToList().AsReadOnly();
        List<XisfUnavailableObject> unavailable = [];
        try { IccProfile = ReadOptionalBlock(xml, source, references, "ICCProfile"); }
        catch (XisfUnsupportedFeatureException exception)
        { unavailable.Add(new(references.Children(xml, "ICCProfile").Single(), exception.Message)); }
        if (references.Children(xml, "Thumbnail").SingleOrDefault() is { } thumbnail)
        {
            try { Thumbnail = Parse(thumbnail, source, references); }
            catch (XisfUnsupportedFeatureException exception) { unavailable.Add(new(thumbnail, exception.Message)); }
        }
        UnavailableObjects = unavailable.AsReadOnly();
        ColorFilterArray = ParseColorFilterArray(xml, references);
        RgbWorkingSpace = ParseRgbWorkingSpace(xml, references);
        DisplayFunction = ParseDisplayFunction(xml, references);
        Resolution = ParseResolution(xml, references);
    }

    /// <summary>The identifier of this item within its containing document.</summary>
    public string? Id { get; }
    /// <summary>Axis lengths in storage order, beginning with width and height; channels are separate.</summary>
    public IReadOnlyList<int> Dimensions { get; }
    /// <summary>The number of samples associated with each pixel.</summary>
    public int ChannelCount { get; }
    /// <summary>The product of image axis lengths, excluding channels.</summary>
    public long PixelCount => Dimensions.Aggregate(1L, (total, dimension) => checked(total * dimension));
    /// <summary>The numeric storage type of each sample.</summary>
    public XisfSampleFormat SampleFormat { get; }
    /// <summary>Whether channels occupy separate planes or alternate within each pixel.</summary>
    public XisfPixelLayout PixelStorage { get; }
    /// <summary>The declared color space, such as Gray, RGB, or CIELab.</summary>
    public string? ColorSpace { get; }
    /// <summary>The astronomical image classification, such as Light, Dark, or Flat.</summary>
    public string? ImageType { get; }
    /// <summary>The image pedestal offset recorded in metadata.</summary>
    public double Offset { get; }
    /// <summary>The declared image orientation.</summary>
    public string Orientation { get; }
    /// <summary>The optional globally unique image identifier.</summary>
    public Guid? Uuid { get; }
    /// <summary>Whether multi-byte samples store their most significant byte first.</summary>
    public bool IsBigEndian { get; }
    /// <summary>The lower sample bound used for normalization.</summary>
    public double LowerBound { get; }
    /// <summary>The upper sample bound used for normalization.</summary>
    public double UpperBound { get; }
    /// <summary>Typed properties indexed by case-sensitive identifiers.</summary>
    public IReadOnlyDictionary<string, XisfProperty> Properties { get; }
    /// <summary>The Revision 1 astrometric solution and fallback diagnostics, or null when absent.</summary>
    public XisfAstrometricSolution? AstrometricSolution => XisfAstrometricSolution.FromProperties(Properties);
    /// <summary>The ordered FITS keywords associated with the image.</summary>
    public IReadOnlyList<XisfFitsKeyword> FitsKeywords { get; }
    /// <summary>The tables associated with this image or document.</summary>
    public IReadOnlyList<XisfTable> Tables { get; }
    /// <summary>The embedded ICC profile bytes, or null when absent.</summary>
    public byte[]? IccProfile { get => _iccProfile?.ToArray(); private init => _iccProfile = value; }
    private readonly byte[]? _iccProfile;
    /// <summary>Optional objects with unsupported representations; their original XML remains available.</summary>
    public IReadOnlyList<XisfUnavailableObject> UnavailableObjects { get; }
    /// <summary>The optional thumbnail image.</summary>
    public XisfImage? Thumbnail { get; }
    /// <summary>The optional repeating color-filter mosaic.</summary>
    public XisfColorFilterArray? ColorFilterArray { get; }
    /// <summary>The optional RGB working-space definition.</summary>
    public XisfRgbWorkingSpace? RgbWorkingSpace { get; }
    /// <summary>The optional per-channel display transformation.</summary>
    public XisfDisplayFunction? DisplayFunction { get; }
    /// <summary>The optional horizontal and vertical resolution metadata.</summary>
    public XisfResolution? Resolution { get; }
    /// <summary>A detached XML copy for inspection.</summary>
    public XElement Xml => new(XmlCore);
    internal XElement XmlCore { get; }

    internal static XisfImage Parse(XElement element, XisfDataSource source, XisfReferenceResolver references)
    {
        string[] geometry = XisfReader.RequiredAttribute(element, "geometry").Split(':');
        if (geometry.Length < 2 || geometry.Any(v => !int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out int n) || n <= 0))
            throw new XisfFormatException("Invalid image geometry attribute.");
        if (geometry.Length - 1 > source.Options.MaximumImageDimensions)
            throw new XisfFormatException("The image exceeds the configured dimension-count limit.");
        int[] values = geometry.Select(v => int.Parse(v, CultureInfo.InvariantCulture)).ToArray();
        if (!Enum.TryParse(XisfReader.RequiredAttribute(element, "sampleFormat"), false, out XisfSampleFormat format) || !Enum.IsDefined(format) || int.TryParse((string?)element.Attribute("sampleFormat"), out _))
            throw new XisfUnsupportedFeatureException("Unsupported image sample format.");
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

    /// <summary>Returns the complete raw sample buffer in its declared byte order and layout.</summary>
    /// <returns>A new byte array containing the complete sample storage.</returns>
    public byte[] ReadRawData()
    {
        byte[] data = _block.Read();
        long expected = checked(PixelCount * ChannelCount * BytesPerSample(SampleFormat));
        if (data.LongLength != expected)
            throw new XisfFormatException($"Image data size is {data.LongLength} bytes; expected {expected}.");
        return data;
    }

    /// <summary>Decodes real samples to the requested type and channel layout with checked numeric conversions.</summary>
    /// <param name="layout">The channel order of the returned samples.</param>
    /// <typeparam name="T">The numeric sample or header-value type.</typeparam>
    /// <returns>A new array of checked numeric values in the requested layout.</returns>
    /// <exception cref="OverflowException">A sample cannot be represented by the requested numeric type.</exception>
    public T[] GetSamples<T>(XisfPixelLayout layout = XisfPixelLayout.Planar) where T : INumberBase<T>
    {
        ApiContracts.Defined(layout, nameof(layout));
        if (SampleFormat is XisfSampleFormat.Complex32 or XisfSampleFormat.Complex64)
            throw new InvalidOperationException("Use GetComplexSamples() for a complex image.");
        byte[] bytes = ReadRawData();
        int count = checked((int)(PixelCount * ChannelCount));
        T[] samples = new T[count];
        int size = BytesPerSample(SampleFormat);
        if (XisfSampleBuffer.Matches<T>(SampleFormat)) XisfSampleBuffer.CopyToArray(bytes, samples, size, IsBigEndian);
        else
            for (int i = 0; i < count; i++)
                samples[i] = ReadSample<T>(bytes.AsSpan(i * size, size));
        return Reorder(samples, layout);
    }

    /// <summary>Decodes complex sample pairs into the requested channel layout.</summary>
    /// <param name="layout">The channel order of the returned samples.</param>
    /// <returns>A new array of complex values in the requested layout.</returns>
    public Complex[] GetComplexSamples(XisfPixelLayout layout = XisfPixelLayout.Planar)
    {
        ApiContracts.Defined(layout, nameof(layout));
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

    /// <summary>Returns double-precision samples normalized by the declared image bounds.</summary>
    /// <param name="layout">The channel order of the returned samples.</param>
    /// <returns>A new array of normalized doubles; values are not clamped to zero through one.</returns>
    public double[] GetNormalizedSamples(XisfPixelLayout layout = XisfPixelLayout.Planar)
    {
        double[] samples = GetSamples<double>(layout);
        double range = UpperBound - LowerBound;
        if (range == 0) { Array.Clear(samples); return samples; }
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
        if (e is null) return null;
        string gammaText = (string?)e.Attribute("gamma") ?? "1";
        bool srgb = gammaText.Equals("sRGB", StringComparison.OrdinalIgnoreCase) || XisfReader.ParseBoolean((string?)e.Attribute("sRGB") ?? "false");
        return new XisfRgbWorkingSpace(srgb ? 2.4 : XisfReader.ParseFloatingPoint(gammaText), srgb,
            XisfReader.ParseDoubles((string?)e.Attribute("x") ?? (string?)e.Attribute("chromaticity")),
            XisfReader.ParseDoubles((string?)e.Attribute("Y") ?? (string?)e.Attribute("luminance")))
        {
            YChromaticityCoordinates = e.Attribute("y") is null ? XisfRgbWorkingSpace.Srgb.YChromaticityCoordinates : XisfReader.ParseDoubles((string?)e.Attribute("y")),
            Name = (string?)e.Attribute("name")
        };
    }

    private static XisfDisplayFunction? ParseDisplayFunction(XElement parent, XisfReferenceResolver references)
    {
        XElement? e = references.Children(parent, "DisplayFunction").SingleOrDefault();
        return e is null ? null : new XisfDisplayFunction(XisfReader.ParseDoubles((string?)e.Attribute("m")), XisfReader.ParseDoubles((string?)e.Attribute("s")), XisfReader.ParseDoubles((string?)e.Attribute("h")), XisfReader.ParseDoubles((string?)e.Attribute("l")), XisfReader.ParseDoubles((string?)e.Attribute("r"))) { Name = (string?)e.Attribute("name") };
    }

    private static XisfResolution? ParseResolution(XElement parent, XisfReferenceResolver references)
    {
        XElement? e = references.Children(parent, "Resolution").SingleOrDefault();
        return e is null ? null : new XisfResolution(double.Parse(XisfReader.RequiredAttribute(e, "horizontal"), CultureInfo.InvariantCulture), double.Parse(XisfReader.RequiredAttribute(e, "vertical"), CultureInfo.InvariantCulture), (string?)e.Attribute("unit") ?? "inch");
    }
}
