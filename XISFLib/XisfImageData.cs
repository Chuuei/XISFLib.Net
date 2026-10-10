using System.Buffers.Binary;
using System.Numerics;

namespace XISFLib;

/// <summary>An owned pixel buffer with editable metadata for serialization.</summary>
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

    /// <summary>Axis lengths in storage order, beginning with width and height; channels are separate.</summary>
    public IReadOnlyList<int> Dimensions { get; }
    /// <summary>The number of samples associated with each pixel.</summary>
    public int ChannelCount { get; }
    /// <summary>The numeric storage type of each sample.</summary>
    public XisfSampleFormat SampleFormat { get; }
    /// <summary>Whether channels occupy separate planes or alternate within each pixel.</summary>
    public XisfPixelLayout PixelStorage { get; }
    /// <summary>Whether multi-byte samples store their most significant byte first.</summary>
    public bool IsBigEndian { get; }
    /// <summary>The identifier of this item within its containing document.</summary>
    public string? Id { get; set; }
    /// <summary>The declared color space, such as Gray, RGB, or CIELab.</summary>
    public string? ColorSpace { get; set; }
    /// <summary>The astronomical image classification, such as Light, Dark, or Flat.</summary>
    public string? ImageType { get; set; }
    /// <summary>The image pedestal offset recorded in metadata.</summary>
    public double Offset { get; set; }
    /// <summary>The declared image orientation.</summary>
    public string? Orientation { get; set; }
    /// <summary>The optional globally unique image identifier.</summary>
    public Guid? Uuid { get; set; }
    /// <summary>The lower sample bound used for normalization.</summary>
    public double? LowerBound { get; set; }
    /// <summary>The upper sample bound used for normalization.</summary>
    public double? UpperBound { get; set; }
    /// <summary>Typed properties indexed by case-sensitive identifiers.</summary>
    public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);
    /// <summary>The ordered FITS keywords associated with the image.</summary>
    public IList<XisfFitsKeyword> FitsKeywords { get; } = [];
    /// <summary>The tables associated with this image or document.</summary>
    public IList<XisfTableData> Tables { get; } = [];
    /// <summary>The embedded ICC profile bytes, or null when absent.</summary>
    public byte[]? IccProfile { get; set; }
    /// <summary>The optional thumbnail image.</summary>
    public XisfImageData? Thumbnail { get; set; }
    /// <summary>The optional repeating color-filter mosaic.</summary>
    public XisfColorFilterArray? ColorFilterArray { get; set; }
    /// <summary>The optional RGB working-space definition.</summary>
    public XisfRgbWorkingSpace? RgbWorkingSpace { get; set; }
    /// <summary>The optional per-channel display transformation.</summary>
    public XisfDisplayFunction? DisplayFunction { get; set; }
    /// <summary>The optional horizontal and vertical resolution metadata.</summary>
    public XisfResolution? Resolution { get; set; }
    /// <summary>A read-only view of the owned sample buffer in its declared byte order and layout.</summary>
    public ReadOnlySpan<byte> RawData => Data;

    internal byte[] Data { get; }

    internal static XisfImageData FromOwnedRaw(byte[] data, IReadOnlyList<int> dimensions, int channelCount, XisfSampleFormat sampleFormat, XisfPixelLayout pixelStorage, bool isBigEndian) =>
        new(data, CopyDimensions(dimensions), channelCount, sampleFormat, pixelStorage, isBigEndian);

    /// <summary>Copies decoded samples and known metadata into an editable image model.</summary>
    /// <param name="image">The source image.</param>
    /// <returns>An editable image with materialized samples and copied known metadata.</returns>
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
            copy.Properties[id] = !property.IsAvailable || id.StartsWith("AstrometricSolution:", StringComparison.Ordinal) ? property : new XisfPropertyData(property.Type, property.Value, property.Format, property.Comment);
        foreach (XisfFitsKeyword keyword in image.FitsKeywords)
            copy.FitsKeywords.Add(keyword);
        foreach (XisfTable table in image.Tables)
        {
            XisfTableData tableCopy = new() { Id = table.Id, Caption = table.Caption, Comment = table.Comment };
            foreach (XisfTableField field in table.Fields)
                tableCopy.Fields.Add(new XisfTableFieldData(field.Id, field.Type, field.Header, field.Format));
            foreach (XisfTableRow row in table.Rows)
                tableCopy.Rows.Add(row.Cells.Select(cell => cell.IsAvailable ? cell.Value : cell).ToArray());
            copy.Tables.Add(tableCopy);
        }
        return copy;
    }

    /// <summary>Copies typed samples into an owned buffer with the specified geometry and channel layout.</summary>
    /// <param name="samples">The samples to copy; the count must match the supplied geometry.</param>
    /// <param name="dimensions">Axis lengths in storage order; channels are specified separately for XISF.</param>
    /// <param name="channelCount">The positive number of channels per pixel.</param>
    /// <param name="pixelStorage">The channel order in the supplied sample buffer.</param>
    /// <param name="isBigEndian">Whether multi-byte storage uses most-significant-byte-first order.</param>
    /// <typeparam name="T">The numeric sample or header-value type.</typeparam>
    /// <returns>An image owning a copy of the supplied samples.</returns>
    public static XisfImageData FromSamples<T>(T[] samples, IReadOnlyList<int> dimensions, int channelCount, XisfPixelLayout pixelStorage = XisfPixelLayout.Planar, bool isBigEndian = false)
        where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(samples);
        return FromSamples((ReadOnlyMemory<T>)samples, dimensions, channelCount, pixelStorage, isBigEndian);
    }

    /// <summary>Copies typed samples into an owned buffer with the specified geometry and channel layout.</summary>
    /// <param name="samples">The samples to copy; the count must match the supplied geometry.</param>
    /// <param name="dimensions">Axis lengths in storage order; channels are specified separately for XISF.</param>
    /// <param name="channelCount">The positive number of channels per pixel.</param>
    /// <param name="pixelStorage">The channel order in the supplied sample buffer.</param>
    /// <param name="isBigEndian">Whether multi-byte storage uses most-significant-byte-first order.</param>
    /// <typeparam name="T">The numeric sample or header-value type.</typeparam>
    /// <returns>An image owning a copy of the supplied samples.</returns>
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

    /// <summary>Copies an array into image storage; indices are height, width, then optional channels.</summary>
    /// <param name="samples">The samples to copy; the count must match the supplied geometry.</param>
    /// <param name="isBigEndian">Whether multi-byte storage uses most-significant-byte-first order.</param>
    /// <typeparam name="T">The numeric sample or header-value type.</typeparam>
    /// <returns>An image owning a copy of the supplied array values.</returns>
    public static XisfImageData FromArray<T>(T[,] samples, bool isBigEndian = false) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(samples);
        T[] flattened = new T[samples.Length];
        int index = 0;
        foreach (T sample in samples) flattened[index++] = sample;
        return FromSamples(flattened, [samples.GetLength(1), samples.GetLength(0)], 1, XisfPixelLayout.Planar, isBigEndian);
    }

    /// <summary>Copies an array into image storage; indices are height, width, then optional channels.</summary>
    /// <param name="samples">The samples to copy; the count must match the supplied geometry.</param>
    /// <param name="lowerBound">The lower normalization or histogram bound.</param>
    /// <param name="upperBound">The upper normalization or histogram bound.</param>
    /// <param name="isBigEndian">Whether multi-byte storage uses most-significant-byte-first order.</param>
    /// <typeparam name="T">The numeric sample or header-value type.</typeparam>
    /// <returns>An image owning a copy of the supplied array values.</returns>
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

    /// <summary>Copies raw storage bytes after validating the declared geometry and storage type.</summary>
    /// <param name="data">The raw storage bytes to copy.</param>
    /// <param name="dimensions">Axis lengths in storage order; channels are specified separately for XISF.</param>
    /// <param name="channelCount">The positive number of channels per pixel.</param>
    /// <param name="sampleFormat">The numeric representation of each raw sample.</param>
    /// <param name="pixelStorage">The channel order in the supplied sample buffer.</param>
    /// <param name="isBigEndian">Whether multi-byte storage uses most-significant-byte-first order.</param>
    /// <returns>An image owning a copy of the supplied storage bytes.</returns>
    public static XisfImageData FromRaw(ReadOnlyMemory<byte> data, IReadOnlyList<int> dimensions, int channelCount, XisfSampleFormat sampleFormat, XisfPixelLayout pixelStorage = XisfPixelLayout.Planar, bool isBigEndian = false)
    {
        return new XisfImageData(data.ToArray(), CopyDimensions(dimensions), channelCount, sampleFormat, pixelStorage, isBigEndian);
    }

    /// <summary>Copies raw storage bytes after validating the declared geometry and storage type.</summary>
    /// <param name="data">The raw storage bytes to copy.</param>
    /// <param name="dimensions">Axis lengths in storage order; channels are specified separately for XISF.</param>
    /// <param name="channelCount">The positive number of channels per pixel.</param>
    /// <param name="sampleFormat">The numeric representation of each raw sample.</param>
    /// <param name="pixelStorage">The channel order in the supplied sample buffer.</param>
    /// <param name="isBigEndian">Whether multi-byte storage uses most-significant-byte-first order.</param>
    /// <returns>An image owning a copy of the supplied storage bytes.</returns>
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
        return [.. dimensions];
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
        if (type == typeof(XisfComplex32)) return XisfSampleFormat.Complex32;
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
            case XisfSampleFormat.Complex32:
                XisfComplex32 complex32 = (XisfComplex32)boxed;
                WriteInt32(destination, BitConverter.SingleToInt32Bits(complex32.Real), bigEndian);
                WriteInt32(destination[4..], BitConverter.SingleToInt32Bits(complex32.Imaginary), bigEndian);
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
