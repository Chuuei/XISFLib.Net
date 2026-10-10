using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Numerics;

namespace XISFLib;

/// <summary>Reports malformed or unsupported FITS input and resource-limit violations.</summary>
public class FitsFormatException : Exception
{
    /// <summary>Initializes the exception with a message and optional underlying cause.</summary>
    /// <param name="message">The explanatory exception message.</param>
    public FitsFormatException(string message) : base(message) { }
    /// <summary>Initializes the exception with a message and optional underlying cause.</summary>
    /// <param name="message">The explanatory exception message.</param>
    /// <param name="innerException">The underlying cause of the format error.</param>
    public FitsFormatException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>The importance of a FITS conformance finding.</summary>
public enum FitsValidationSeverity
{
    /// <summary>A recoverable conformance finding.</summary>
    Warning,
    /// <summary>A conformance violation.</summary>
    Error
}

/// <summary>A FITS conformance finding associated with an HDU and optional card.</summary>
/// <param name="Code">The stable diagnostic code.</param>
/// <param name="Severity">The importance of the diagnostic.</param>
/// <param name="Message">The human-readable explanation.</param>
/// <param name="HduIndex">The zero-based HDU index.</param>
/// <param name="CardIndex">The optional zero-based header-card index.</param>
/// <param name="Keyword">The FITS keyword name.</param>
public sealed record FitsValidationDiagnostic(
    string Code,
    FitsValidationSeverity Severity,
    string Message,
    int HduIndex,
    int? CardIndex = null,
    string? Keyword = null);

/// <summary>The kind of FITS header/data unit.</summary>
public enum FitsHduType
{
    /// <summary>The first HDU in a FITS document.</summary>
    Primary,
    /// <summary>A FITS image extension.</summary>
    Image,
    /// <summary>A FITS ASCII table extension.</summary>
    AsciiTable,
    /// <summary>A FITS binary table extension.</summary>
    BinaryTable,
    /// <summary>A FITS random-groups HDU.</summary>
    RandomGroups,
    /// <summary>An unrecognized FITS extension.</summary>
    Unknown
}

/// <summary>A FITS keyword, typed value, and optional comment.</summary>
/// <param name="Keyword">The FITS keyword name.</param>
/// <param name="Value">The typed value; null represents an absent value.</param>
/// <param name="Comment">The optional explanatory comment.</param>
public sealed record FitsHeaderCard(string Keyword, object? Value = null, string? Comment = null)
{
    /// <summary>The trimmed FITS keyword name.</summary>
    public string Keyword { get; } = string.IsNullOrWhiteSpace(Keyword)
        ? throw new ArgumentException("A FITS keyword is required.", nameof(Keyword))
        : Keyword.Trim();
}

/// <summary>An ordered, editable collection of FITS cards preserving repeated keywords.</summary>
public sealed class FitsHeader : Collection<FitsHeaderCard>
{
    /// <summary>Initializes a new instance with the supplied values.</summary>
    public FitsHeader() { }
    /// <summary>Initializes a new instance with the supplied values.</summary>
    /// <param name="cards">The header cards in storage order.</param>
    public FitsHeader(IEnumerable<FitsHeaderCard> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);
        foreach (FitsHeaderCard card in cards) Add(card);
    }

    /// <summary>Returns all keyword matches using case-insensitive comparison.</summary>
    /// <param name="keyword">The FITS keyword matched without case sensitivity.</param>
    /// <returns>All matching cards in header order.</returns>
    public IEnumerable<FitsHeaderCard> Find(string keyword) =>
        this.Where(card => card.Keyword.Equals(keyword, StringComparison.OrdinalIgnoreCase));

    /// <summary>Returns the first keyword match, or null when absent.</summary>
    /// <param name="keyword">The FITS keyword matched without case sensitivity.</param>
    /// <returns>The first matching card, or null if none exists.</returns>
    public FitsHeaderCard? FindFirst(string keyword) => Find(keyword).FirstOrDefault();

    /// <summary>Converts the first keyword value to the requested type, or returns the supplied default.</summary>
    /// <param name="keyword">The FITS keyword matched without case sensitivity.</param>
    /// <param name="defaultValue">The value returned when the keyword is absent or has a null value.</param>
    /// <typeparam name="T">The numeric sample or header-value type.</typeparam>
    /// <returns>The converted value or the supplied default.</returns>
    public T? GetValue<T>(string keyword, T? defaultValue = default)
    {
        object? value = FindFirst(keyword)?.Value;
        if (value is null) return defaultValue;
        if (value is T typed) return typed;
        Type target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        if (target.IsEnum) return (T)Enum.Parse(target, Convert.ToString(value, CultureInfo.InvariantCulture)!, true);
        return (T)Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
    }

    internal void Set(string keyword, object? value, string? comment = null)
    {
        for (int i = Count - 1; i >= 0; i--)
            if (this[i].Keyword.Equals(keyword, StringComparison.OrdinalIgnoreCase)) RemoveAt(i);
        Add(new FitsHeaderCard(keyword, value, comment));
    }
}

/// <summary>An ordered sequence of HDUs beginning with a primary HDU, and conformance diagnostics.</summary>
public sealed class FitsDocument
{
    /// <summary>Initializes a new instance with the supplied values.</summary>
    /// <param name="hdus">The HDUs in document order, beginning with a primary HDU.</param>
    public FitsDocument(IEnumerable<FitsHdu> hdus) : this(hdus, []) { }

    internal FitsDocument(IEnumerable<FitsHdu> hdus, IEnumerable<FitsValidationDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(hdus);
        ArgumentNullException.ThrowIfNull(diagnostics);
        Hdus = hdus.ToList();
        if (Hdus.Count == 0) throw new ArgumentException("A FITS document requires at least one HDU.", nameof(hdus));
        if (Hdus[0].Type != FitsHduType.Primary) throw new ArgumentException("The first FITS HDU must be a primary HDU.", nameof(hdus));
        Diagnostics = diagnostics.ToList();
    }

    /// <summary>The ordered header/data units, beginning with the primary HDU.</summary>
    public IReadOnlyList<FitsHdu> Hdus { get; }
    /// <summary>The conformance findings recorded during reading.</summary>
    public IReadOnlyList<FitsValidationDiagnostic> Diagnostics { get; }
    /// <summary>The images in document order.</summary>
    public IEnumerable<FitsImage> Images => Hdus.Select(hdu => hdu.Content).OfType<FitsImage>();
    /// <summary>The tables associated with this image or document.</summary>
    public IEnumerable<FitsTable> Tables => Hdus.Select(hdu => hdu.Content).OfType<FitsTable>();
}

/// <summary>A FITS header/data unit with raw payload, decoded content, and checksum results.</summary>
public sealed class FitsHdu
{
    /// <summary>Initializes a new instance with the supplied values.</summary>
    /// <param name="type">The kind of FITS HDU.</param>
    /// <param name="header">The FITS header cards.</param>
    /// <param name="data">The raw storage bytes to copy.</param>
    public FitsHdu(FitsHduType type, IEnumerable<FitsHeaderCard> header, byte[] data)
        : this(type, new FitsHeader(header), data) { }

    /// <summary>Initializes a new instance with the supplied values.</summary>
    /// <param name="type">The kind of FITS HDU.</param>
    /// <param name="header">The FITS header cards.</param>
    /// <param name="data">The raw storage bytes to copy.</param>
    public FitsHdu(FitsHduType type, FitsHeader header, byte[] data)
        : this(type, header, data, false) { }

    private FitsHdu(FitsHduType type, FitsHeader header, byte[] data, bool takeOwnership)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(data);
        Type = type;
        Header = header;
        Data = takeOwnership ? data : data.ToArray();
    }

    internal static FitsHdu FromOwnedData(FitsHduType type, FitsHeader header, byte[] data) => new(type, header, data, true);

    /// <summary>The kind of FITS header/data unit.</summary>
    public FitsHduType Type { get; }
    /// <summary>The ordered, mutable FITS header cards.</summary>
    public FitsHeader Header { get; }
    /// <summary>Raw FITS payload bytes excluding block padding.</summary>
    public byte[] Data { get; internal set; }
    /// <summary>The decoded image or table, or null for unsupported or empty content.</summary>
    public object? Content { get; internal set; }
    /// <summary>The HDU checksum result, or null when no CHECKSUM card is present.</summary>
    public bool? ChecksumValid { get; internal set; }
    /// <summary>The data checksum result, or null when no DATASUM card is present.</summary>
    public bool? DataChecksumValid { get; internal set; }

    /// <summary>Creates an empty primary FITS HDU.</summary>
    /// <returns>An empty primary HDU.</returns>
    public static FitsHdu CreatePrimary() => new(FitsHduType.Primary, new FitsHeader(), []);

    /// <summary>Creates a FITS image HDU, optionally as the primary HDU.</summary>
    /// <param name="image">The source image.</param>
    /// <param name="primary">Whether this image is the primary HDU.</param>
    /// <returns>An image HDU containing the supplied image.</returns>
    public static FitsHdu CreateImage(FitsImage image, bool primary)
    {
        ArgumentNullException.ThrowIfNull(image);
        FitsHdu hdu = new(primary ? FitsHduType.Primary : FitsHduType.Image, new FitsHeader(), image.RawData);
        hdu.Content = image;
        return hdu;
    }
}

/// <summary>A FITS image with big-endian storage samples and physical-value scaling metadata.</summary>
public sealed class FitsImage
{
    internal FitsImage(int bitPix, int[] dimensions, byte[] rawData, double bScale, double bZero, long? blank)
    {
        if (bitPix is not (8 or 16 or 32 or 64 or -32 or -64)) throw new ArgumentOutOfRangeException(nameof(bitPix));
        if (dimensions.Any(value => value < 0)) throw new ArgumentException("FITS axis lengths cannot be negative.", nameof(dimensions));
        long samples = dimensions.Aggregate(1L, (total, value) => checked(total * value));
        long expected = checked(samples * Math.Abs(bitPix) / 8);
        if (rawData.LongLength != expected) throw new ArgumentException($"Image data has {rawData.LongLength} bytes; expected {expected}.", nameof(rawData));
        BitPix = bitPix;
        Dimensions = dimensions;
        RawData = rawData;
        BScale = bScale;
        BZero = bZero;
        Blank = blank;
    }

    /// <summary>The FITS BITPIX storage code; negative codes represent floating-point samples.</summary>
    public int BitPix { get; }
    /// <summary>Axis lengths in storage order, beginning with width and height; channels are separate.</summary>
    public IReadOnlyList<int> Dimensions { get; }
    /// <summary>The multiplier in physical = stored * BSCALE + BZERO.</summary>
    public double BScale { get; }
    /// <summary>The additive offset in physical = stored * BSCALE + BZERO.</summary>
    public double BZero { get; }
    /// <summary>The optional integer storage sentinel mapped to NaN in physical samples.</summary>
    public long? Blank { get; }
    internal byte[] RawData { get; }

    /// <summary>Returns the complete raw sample buffer in its declared byte order and layout.</summary>
    /// <returns>A new byte array containing the complete sample storage.</returns>
    public byte[] ReadRawData() => RawData.ToArray();

    /// <summary>Copies raw storage bytes after validating the declared geometry and storage type.</summary>
    /// <param name="data">The raw storage bytes to copy.</param>
    /// <param name="bitPix">The FITS storage code: 8, 16, 32, 64, -32, or -64.</param>
    /// <param name="dimensions">Axis lengths in storage order; channels are specified separately for XISF.</param>
    /// <param name="bScale">The multiplier in physical = stored * BSCALE + BZERO.</param>
    /// <param name="bZero">The additive offset in physical = stored * BSCALE + BZERO.</param>
    /// <param name="blank">The optional integer storage sentinel representing an undefined sample.</param>
    /// <returns>An image owning a copy of the supplied storage bytes.</returns>
    public static FitsImage FromRaw(ReadOnlyMemory<byte> data, int bitPix, IReadOnlyList<int> dimensions, double bScale = 1, double bZero = 0, long? blank = null)
    {
        ArgumentNullException.ThrowIfNull(dimensions);
        return new FitsImage(bitPix, dimensions.ToArray(), data.ToArray(), bScale, bZero, blank);
    }

    /// <summary>Returns double-precision physical samples using BSCALE and BZERO, with integer BLANK mapped to NaN.</summary>
    /// <returns>Physical values as doubles; large integer values may lose precision.</returns>
    public double[] GetPhysicalSamples()
    {
        int size = Math.Abs(BitPix) / 8;
        int count = checked((int)(RawData.LongLength / size));
        double[] result = new double[count];
        for (int i = 0; i < count; i++)
        {
            ReadOnlySpan<byte> sample = RawData.AsSpan(i * size, size);
            double stored = BitPix switch
            {
                8 => sample[0],
                16 => BinaryPrimitives.ReadInt16BigEndian(sample),
                32 => BinaryPrimitives.ReadInt32BigEndian(sample),
                64 => BinaryPrimitives.ReadInt64BigEndian(sample),
                -32 => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32BigEndian(sample)),
                -64 => BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64BigEndian(sample)),
                _ => throw new InvalidOperationException()
            };
            result[i] = Blank.HasValue && BitPix > 0 && stored == Blank.Value ? double.NaN : stored * BScale + BZero;
        }
        return result;
    }

    /// <summary>Encodes physical samples as FITS storage, including unsigned-integer offset conventions.</summary>
    /// <param name="samples">The samples to copy; the count must match the supplied geometry.</param>
    /// <param name="dimensions">Axis lengths in storage order; channels are specified separately for XISF.</param>
    /// <typeparam name="T">The numeric sample or header-value type.</typeparam>
    /// <returns>An image with FITS-compatible storage encoding.</returns>
    public static FitsImage FromPhysicalSamples<T>(T[] samples, IReadOnlyList<int> dimensions) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(dimensions);
        int[] axes = dimensions.ToArray();
        long expected = axes.Aggregate(1L, (total, value) => checked(total * value));
        if (expected != samples.LongLength) throw new ArgumentException("The sample count does not match the FITS dimensions.", nameof(samples));
        Type type = typeof(T);
        int bitPix;
        double zero = 0;
        if (type == typeof(byte)) bitPix = 8;
        else if (type == typeof(short)) bitPix = 16;
        else if (type == typeof(int)) bitPix = 32;
        else if (type == typeof(long)) bitPix = 64;
        else if (type == typeof(float)) bitPix = -32;
        else if (type == typeof(double)) bitPix = -64;
        else if (type == typeof(ushort)) { bitPix = 16; zero = 32768d; }
        else if (type == typeof(uint)) { bitPix = 32; zero = 2147483648d; }
        else if (type == typeof(ulong)) { bitPix = 64; zero = 9223372036854775808d; }
        else throw new NotSupportedException($"The .NET type '{type.Name}' is not a FITS image sample type.");

        int size = Math.Abs(bitPix) / 8;
        byte[] raw = new byte[checked(samples.Length * size)];
        for (int i = 0; i < samples.Length; i++)
        {
            Span<byte> target = raw.AsSpan(i * size, size);
            object value = samples[i];
            if (type == typeof(byte)) target[0] = (byte)value;
            else if (type == typeof(short)) BinaryPrimitives.WriteInt16BigEndian(target, (short)value);
            else if (type == typeof(ushort)) BinaryPrimitives.WriteInt16BigEndian(target, unchecked((short)((ushort)value - 32768)));
            else if (type == typeof(int)) BinaryPrimitives.WriteInt32BigEndian(target, (int)value);
            else if (type == typeof(uint)) BinaryPrimitives.WriteInt32BigEndian(target, unchecked((int)((uint)value - 2147483648U)));
            else if (type == typeof(long)) BinaryPrimitives.WriteInt64BigEndian(target, (long)value);
            else if (type == typeof(ulong)) BinaryPrimitives.WriteInt64BigEndian(target, unchecked((long)((ulong)value - 9223372036854775808UL)));
            else if (type == typeof(float)) BinaryPrimitives.WriteInt32BigEndian(target, BitConverter.SingleToInt32Bits((float)value));
            else BinaryPrimitives.WriteInt64BigEndian(target, BitConverter.DoubleToInt64Bits((double)value));
        }
        return new FitsImage(bitPix, axes, raw, 1, zero, null);
    }
}

/// <summary>A FITS table column with format, unit, scaling, and null sentinel.</summary>
/// <param name="Name">The keyword, column, or mosaic name.</param>
/// <param name="Format">The optional display or storage format.</param>
/// <param name="Unit">The resolution or table-column unit.</param>
/// <param name="Scale">The physical-value multiplier.</param>
/// <param name="Zero">The physical-value offset.</param>
/// <param name="Null">The optional integer null sentinel.</param>
public sealed record FitsTableColumn(string Name, string Format, string? Unit = null, double Scale = 1, double Zero = 0, long? Null = null);

/// <summary>A decoded FITS table with ordered columns and rows.</summary>
public sealed class FitsTable
{
    internal FitsTable(IReadOnlyList<FitsTableColumn> columns, IReadOnlyList<IReadOnlyList<object?>> rows)
    {
        Columns = columns;
        Rows = rows;
    }

    /// <summary>The ordered FITS table column definitions.</summary>
    public IReadOnlyList<FitsTableColumn> Columns { get; }
    /// <summary>The ordered rows; cells follow the field or column order.</summary>
    public IReadOnlyList<IReadOnlyList<object?>> Rows { get; }
}
