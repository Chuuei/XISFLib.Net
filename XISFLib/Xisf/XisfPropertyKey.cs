namespace XISFLib;

/// <summary>An immutable typed XISF property definition, with its authority or evidence.</summary>
public abstract class XisfPropertyKey
{
    internal XisfPropertyKey(string id, string propertyType, string? unit)
        : this(id, propertyType, unit, MetadataDefinitionSource.Specification, "https://pixinsight.com/doc/docs/XISF-1.0-spec/XISF-1.0-spec.html") { }

    internal XisfPropertyKey(string id, string propertyType, string? unit, MetadataDefinitionSource source, string sourceReference)
    { Id = id; PropertyType = propertyType; Unit = unit; Source = source; SourceReference = sourceReference; }

    /// <summary>The case-sensitive property identifier in this definition.</summary>
    public string Id { get; }
    /// <summary>The XISF serialization type, such as Float32 or F64Matrix.</summary>
    public string PropertyType { get; }
    /// <summary>The physical unit, or null when unspecified or context-dependent.</summary>
    public string? Unit { get; }
    /// <summary>Whether the definition is specified, published, observed, or a library convention.</summary>
    public MetadataDefinitionSource Source { get; }
    /// <summary>A documentation URL or repository location supporting this definition.</summary>
    public string SourceReference { get; }
    /// <summary>The CLR type used by typed accessors.</summary>
    public abstract Type ValueType { get; }
    /// <inheritdoc />
    public override string ToString() => Id;
}

/// <summary>A predefined property definition carrying its CLR value type and specified array shape.</summary>
/// <typeparam name="T">The scalar, vector or matrix CLR type.</typeparam>
public sealed class XisfPropertyKey<T> : XisfPropertyKey
{
    private readonly int? _length;
    private readonly int? _rows;
    private readonly int? _columns;

    internal XisfPropertyKey(string id, string propertyType, string? unit = null, int? length = null, int? rows = null, int? columns = null)
        : base(id, propertyType, unit)
    { _length = length; _rows = rows; _columns = columns; }

    internal XisfPropertyKey(string id, string propertyType, MetadataDefinitionSource source, string sourceReference, string? unit = null)
        : base(id, propertyType, unit, source, sourceReference) { }

    /// <inheritdoc />
    public override Type ValueType => typeof(T);

    internal XisfPropertyData CreateData(T value, string? format, string? comment)
    {
        Validate(value);
        return new(PropertyType, Copy(value), format, comment);
    }

    internal bool TryRead(object? raw, out T value)
    {
        value = default!;
        if (raw is XisfProperty property)
        {
            if (!property.IsAvailable) return false;
            raw = property.ValueCore;
        }
        else if (raw is XisfPropertyData data) raw = data.Value;
        if (raw is null) return false;
        try
        {
            T converted = ApiContracts.ConvertValue<T>(raw);
            Validate(converted);
            value = Copy(converted);
            return true;
        }
        catch (Exception e) when (e is FormatException or InvalidCastException or OverflowException or ArgumentException) { return false; }
    }

    internal T ReadRequired(object? raw)
    {
        if (!TryRead(raw, out var value)) throw new InvalidOperationException($"Property '{Id}' is unavailable or incompatible with {typeof(T).Name} or its specified shape.");
        return value;
    }

    private void Validate(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value is Array array &&
            (_length is { } length && (array.Rank != 1 || array.Length != length) ||
             _rows is { } rows && (array.Rank != 2 || array.GetLength(0) != rows) ||
             _columns is { } columns && (array.Rank != 2 || array.GetLength(1) != columns)))
            throw new ArgumentException($"Property '{Id}' has an invalid array shape.", nameof(value));
    }

    private static T Copy(T value) => value is Array array ? (T)(object)array.Clone() : value;
}
