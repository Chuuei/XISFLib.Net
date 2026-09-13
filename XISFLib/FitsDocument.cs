using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Numerics;

namespace XISFLib;

public class FitsFormatException : Exception
{
    public FitsFormatException(string message) : base(message) { }
    public FitsFormatException(string message, Exception innerException) : base(message, innerException) { }
}

public enum FitsValidationSeverity
{
    Warning,
    Error
}

public sealed record FitsValidationDiagnostic(
    string Code,
    FitsValidationSeverity Severity,
    string Message,
    int HduIndex,
    int? CardIndex = null,
    string? Keyword = null);

public enum FitsHduType
{
    Primary,
    Image,
    AsciiTable,
    BinaryTable,
    RandomGroups,
    Unknown
}

public sealed record FitsHeaderCard(string Keyword, object? Value = null, string? Comment = null)
{
    public string Keyword { get; } = string.IsNullOrWhiteSpace(Keyword)
        ? throw new ArgumentException("A FITS keyword is required.", nameof(Keyword))
        : Keyword.Trim();
}

public sealed class FitsHeader : Collection<FitsHeaderCard>
{
    public FitsHeader() { }
    public FitsHeader(IEnumerable<FitsHeaderCard> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);
        foreach (FitsHeaderCard card in cards) Add(card);
    }

    public IEnumerable<FitsHeaderCard> Find(string keyword) =>
        this.Where(card => card.Keyword.Equals(keyword, StringComparison.OrdinalIgnoreCase));

    public FitsHeaderCard? FindFirst(string keyword) => Find(keyword).FirstOrDefault();

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

public sealed class FitsDocument
{
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

    public IReadOnlyList<FitsHdu> Hdus { get; }
    public IReadOnlyList<FitsValidationDiagnostic> Diagnostics { get; }
    public IEnumerable<FitsImage> Images => Hdus.Select(hdu => hdu.Content).OfType<FitsImage>();
    public IEnumerable<FitsTable> Tables => Hdus.Select(hdu => hdu.Content).OfType<FitsTable>();
}

public sealed class FitsHdu
{
    public FitsHdu(FitsHduType type, IEnumerable<FitsHeaderCard> header, byte[] data)
        : this(type, new FitsHeader(header), data) { }

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

    public FitsHduType Type { get; }
    public FitsHeader Header { get; }
    public byte[] Data { get; internal set; }
    public object? Content { get; internal set; }
    public bool? ChecksumValid { get; internal set; }
    public bool? DataChecksumValid { get; internal set; }

    public static FitsHdu CreatePrimary() => new(FitsHduType.Primary, new FitsHeader(), []);

    public static FitsHdu CreateImage(FitsImage image, bool primary)
    {
        ArgumentNullException.ThrowIfNull(image);
        FitsHdu hdu = new(primary ? FitsHduType.Primary : FitsHduType.Image, new FitsHeader(), image.RawData);
        hdu.Content = image;
        return hdu;
    }
}

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

    public int BitPix { get; }
    public IReadOnlyList<int> Dimensions { get; }
    public double BScale { get; }
    public double BZero { get; }
    public long? Blank { get; }
    internal byte[] RawData { get; }

    public byte[] ReadRawData() => RawData.ToArray();

    public static FitsImage FromRaw(ReadOnlyMemory<byte> data, int bitPix, IReadOnlyList<int> dimensions, double bScale = 1, double bZero = 0, long? blank = null)
    {
        ArgumentNullException.ThrowIfNull(dimensions);
        return new FitsImage(bitPix, dimensions.ToArray(), data.ToArray(), bScale, bZero, blank);
    }

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

public sealed record FitsTableColumn(string Name, string Format, string? Unit = null, double Scale = 1, double Zero = 0, long? Null = null);

public sealed class FitsTable
{
    internal FitsTable(IReadOnlyList<FitsTableColumn> columns, IReadOnlyList<IReadOnlyList<object?>> rows)
    {
        Columns = columns;
        Rows = rows;
    }

    public IReadOnlyList<FitsTableColumn> Columns { get; }
    public IReadOnlyList<IReadOnlyList<object?>> Rows { get; }
}