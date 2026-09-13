using System.Buffers.Binary;
using System.Numerics;

namespace XISFLib;

public sealed class XisfImageData
{
    private XisfImageData(byte[] data, int[] dimensions, int channelCount, XisfSampleFormat sampleFormat, XisfPixelLayout pixelStorage, bool isBigEndian)
    {
        Validate(data, dimensions, channelCount, sampleFormat);
        Data = data;
        Dimensions = dimensions;
        ChannelCount = channelCount;
        SampleFormat = sampleFormat;
        PixelStorage = pixelStorage;
        IsBigEndian = isBigEndian;
        ColorSpace = channelCount == 1 ? "Gray" : channelCount is 3 or 4 ? "RGB" : null;
        if (sampleFormat is XisfSampleFormat.Float32 or XisfSampleFormat.Float64)
        {
            LowerBound = 0;
            UpperBound = 1;
        }
    }

    public IReadOnlyList<int> Dimensions { get; }
    public int ChannelCount { get; }
    public XisfSampleFormat SampleFormat { get; }
    public XisfPixelLayout PixelStorage { get; }
    public bool IsBigEndian { get; }
    public string? Id { get; set; }
    public string? ColorSpace { get; set; }
    public string? ImageType { get; set; }
    public double Offset { get; set; }
    public string? Orientation { get; set; }
    public Guid? Uuid { get; set; }
    public double? LowerBound { get; set; }
    public double? UpperBound { get; set; }
    public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);
    public IList<XisfFitsKeyword> FitsKeywords { get; } = new List<XisfFitsKeyword>();
    public IList<XisfTableData> Tables { get; } = new List<XisfTableData>();
    public byte[]? IccProfile { get; set; }
    public XisfImageData? Thumbnail { get; set; }
    public XisfColorFilterArray? ColorFilterArray { get; set; }
    public XisfRgbWorkingSpace? RgbWorkingSpace { get; set; }
    public XisfDisplayFunction? DisplayFunction { get; set; }
    public XisfResolution? Resolution { get; set; }
    public ReadOnlySpan<byte> RawData => Data;

    internal byte[] Data { get; }

    internal static XisfImageData FromOwnedRaw(byte[] data, IReadOnlyList<int> dimensions, int channelCount, XisfSampleFormat sampleFormat, XisfPixelLayout pixelStorage, bool isBigEndian) =>
        new(data, CopyDimensions(dimensions), channelCount, sampleFormat, pixelStorage, isBigEndian);

    public static XisfImageData FromImage(XisfImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        XisfImageData copy = FromOwnedRaw(image.ReadRawData(), image.Dimensions, image.ChannelCount, image.SampleFormat, image.PixelStorage, image.IsBigEndian);
        copy.Id = image.Id;
        copy.ColorSpace = image.ColorSpace;
        copy.ImageType = image.ImageType;
        copy.Offset = image.Offset;
        copy.Orientation = image.Orientation;
        copy.Uuid = image.Uuid;
        copy.LowerBound = image.LowerBound;
        copy.UpperBound = image.UpperBound;
        copy.IccProfile = image.IccProfile;
        copy.Thumbnail = image.Thumbnail is null ? null : FromImage(image.Thumbnail);
        copy.ColorFilterArray = image.ColorFilterArray;
        copy.RgbWorkingSpace = image.RgbWorkingSpace;
        copy.DisplayFunction = image.DisplayFunction;
        copy.Resolution = image.Resolution;
        foreach ((string id, XisfProperty property) in image.Properties)
            copy.Properties[id] = new XisfPropertyData(property.Type, property.Value, property.Format, property.Comment);
        foreach (XisfFitsKeyword keyword in image.FitsKeywords)
            copy.FitsKeywords.Add(keyword);
        foreach (XisfTable table in image.Tables)
        {
            XisfTableData tableCopy = new() { Id = table.Id, Caption = table.Caption };
            foreach (XisfTableField field in table.Fields)
                tableCopy.Fields.Add(new XisfTableFieldData(field.Id, field.Type, field.Header, field.Format));
            foreach (XisfTableRow row in table.Rows)
                tableCopy.Rows.Add(row.Cells.Select(cell => cell.Value).ToArray());
            copy.Tables.Add(tableCopy);
        }
        return copy;
    }

    public static XisfImageData FromSamples<T>(T[] samples, IReadOnlyList<int> dimensions, int channelCount, XisfPixelLayout pixelStorage = XisfPixelLayout.Planar, bool isBigEndian = false)
        where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(samples);
        return FromSamples((ReadOnlyMemory<T>)samples, dimensions, channelCount, pixelStorage, isBigEndian);
    }

    public static XisfImageData FromSamples<T>(ReadOnlyMemory<T> samples, IReadOnlyList<int> dimensions, int channelCount, XisfPixelLayout pixelStorage = XisfPixelLayout.Planar, bool isBigEndian = false)
        where T : unmanaged
    {
        XisfSampleFormat format = SampleFormatOf<T>();
        int size = BytesPerSample(format);
        byte[] data = new byte[checked(samples.Length * size)];
        ReadOnlySpan<T> source = samples.Span;
        for (int i = 0; i < source.Length; i++)
            WriteSample(data.AsSpan(i * size, size), source[i], format, isBigEndian);
        return new XisfImageData(data, CopyDimensions(dimensions), channelCount, format, pixelStorage, isBigEndian);
    }

    public static XisfImageData FromArray<T>(T[,] samples, bool isBigEndian = false) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(samples);
        T[] flattened = new T[samples.Length];
        int index = 0;
        foreach (T sample in samples) flattened[index++] = sample;
        return FromSamples(flattened, [samples.GetLength(1), samples.GetLength(0)], 1, XisfPixelLayout.Planar, isBigEndian);
    }

    public static XisfImageData FromArray<T>(T[,,] samples, double lowerBound = 0, double upperBound = 1, bool isBigEndian = false) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(samples);
        T[] flattened = new T[samples.Length];
        int index = 0;
        foreach (T sample in samples) flattened[index++] = sample;
        XisfImageData image = FromSamples(flattened, [samples.GetLength(1), samples.GetLength(0)], samples.GetLength(2), XisfPixelLayout.Interleaved, isBigEndian);
        if (image.SampleFormat is XisfSampleFormat.Float32 or XisfSampleFormat.Float64)
        {
            image.LowerBound = lowerBound;
            image.UpperBound = upperBound;
        }
        return image;
    }

    public static XisfImageData FromRaw(ReadOnlyMemory<byte> data, IReadOnlyList<int> dimensions, int channelCount, XisfSampleFormat sampleFormat, XisfPixelLayout pixelStorage = XisfPixelLayout.Planar, bool isBigEndian = false)
    {
        return new XisfImageData(data.ToArray(), CopyDimensions(dimensions), channelCount, sampleFormat, pixelStorage, isBigEndian);
    }

    public static XisfImageData FromRaw(byte[] data, IReadOnlyList<int> dimensions, int channelCount, XisfSampleFormat sampleFormat, XisfPixelLayout pixelStorage = XisfPixelLayout.Planar, bool isBigEndian = false)
    {
        ArgumentNullException.ThrowIfNull(data);
        return new XisfImageData(data.ToArray(), CopyDimensions(dimensions), channelCount, sampleFormat, pixelStorage, isBigEndian);
    }

    internal static int BytesPerSample(XisfSampleFormat format) => format switch
    {
        XisfSampleFormat.UInt8 => 1,
        XisfSampleFormat.UInt16 => 2,
        XisfSampleFormat.UInt32 or XisfSampleFormat.Float32 => 4,
        XisfSampleFormat.UInt64 or XisfSampleFormat.Float64 or XisfSampleFormat.Complex32 => 8,
        XisfSampleFormat.Complex64 => 16,
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

    private static int[] CopyDimensions(IReadOnlyList<int> dimensions)
    {
        ArgumentNullException.ThrowIfNull(dimensions);
        return dimensions.ToArray();
    }

    private static void Validate(byte[] data, int[] dimensions, int channelCount, XisfSampleFormat sampleFormat)
    {
        if (dimensions.Length < 1 || dimensions.Any(dimension => dimension <= 0))
            throw new ArgumentException("An image requires at least one positive dimension.", nameof(dimensions));
        if (channelCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(channelCount), "The channel count must be positive.");
        long sampleCount = dimensions.Aggregate(1L, (total, dimension) => checked(total * dimension)) * channelCount;
        long expectedLength = checked(sampleCount * BytesPerSample(sampleFormat));
        if (data.LongLength != expectedLength)
            throw new ArgumentException($"The sample data has {data.LongLength} bytes; expected {expectedLength}.", nameof(data));
    }

    private static XisfSampleFormat SampleFormatOf<T>() where T : unmanaged
    {
        Type type = typeof(T);
        if (type == typeof(byte)) return XisfSampleFormat.UInt8;
        if (type == typeof(ushort)) return XisfSampleFormat.UInt16;
        if (type == typeof(uint)) return XisfSampleFormat.UInt32;
        if (type == typeof(ulong)) return XisfSampleFormat.UInt64;
        if (type == typeof(float)) return XisfSampleFormat.Float32;
        if (type == typeof(double)) return XisfSampleFormat.Float64;
        if (type == typeof(Complex)) return XisfSampleFormat.Complex64;
        throw new NotSupportedException($"The .NET sample type '{type.Name}' has no supported XISF image sample format.");
    }

    private static void WriteSample<T>(Span<byte> destination, T value, XisfSampleFormat format, bool bigEndian) where T : unmanaged
    {
        object boxed = value;
        switch (format)
        {
            case XisfSampleFormat.UInt8:
                destination[0] = (byte)boxed;
                break;
            case XisfSampleFormat.UInt16:
                if (bigEndian) BinaryPrimitives.WriteUInt16BigEndian(destination, (ushort)boxed);
                else BinaryPrimitives.WriteUInt16LittleEndian(destination, (ushort)boxed);
                break;
            case XisfSampleFormat.UInt32:
                if (bigEndian) BinaryPrimitives.WriteUInt32BigEndian(destination, (uint)boxed);
                else BinaryPrimitives.WriteUInt32LittleEndian(destination, (uint)boxed);
                break;
            case XisfSampleFormat.UInt64:
                if (bigEndian) BinaryPrimitives.WriteUInt64BigEndian(destination, (ulong)boxed);
                else BinaryPrimitives.WriteUInt64LittleEndian(destination, (ulong)boxed);
                break;
            case XisfSampleFormat.Float32:
                WriteInt32(destination, BitConverter.SingleToInt32Bits((float)boxed), bigEndian);
                break;
            case XisfSampleFormat.Float64:
                WriteInt64(destination, BitConverter.DoubleToInt64Bits((double)boxed), bigEndian);
                break;
            case XisfSampleFormat.Complex64:
                Complex complex = (Complex)boxed;
                WriteInt64(destination, BitConverter.DoubleToInt64Bits(complex.Real), bigEndian);
                WriteInt64(destination[8..], BitConverter.DoubleToInt64Bits(complex.Imaginary), bigEndian);
                break;
            default:
                throw new NotSupportedException($"Typed conversion to {format} is not supported. Use {nameof(FromRaw)} for this format.");
        }
    }

    private static void WriteInt32(Span<byte> destination, int value, bool bigEndian)
    {
        if (bigEndian) BinaryPrimitives.WriteInt32BigEndian(destination, value);
        else BinaryPrimitives.WriteInt32LittleEndian(destination, value);
    }

    private static void WriteInt64(Span<byte> destination, long value, bool bigEndian)
    {
        if (bigEndian) BinaryPrimitives.WriteInt64BigEndian(destination, value);
        else BinaryPrimitives.WriteInt64LittleEndian(destination, value);
    }
}