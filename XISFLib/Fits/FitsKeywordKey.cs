using System.Globalization;

namespace XISFLib;

/// <summary>An immutable FITS keyword definition with its type, units and evidence.</summary>
public abstract class FitsKeywordKey
{
    internal FitsKeywordKey(string keyword, MetadataDefinitionSource source, string sourceReference, string? unit, bool repeatable)
    { Keyword = keyword; Source = source; SourceReference = sourceReference; Unit = unit; IsRepeatable = repeatable; }
    /// <summary>The case-insensitive FITS keyword name.</summary>
    public string Keyword { get; }
    /// <summary>The definition's authority or evidence.</summary>
    public MetadataDefinitionSource Source { get; }
    /// <summary>A documentation URL or repository location supporting the definition.</summary>
    public string SourceReference { get; }
    /// <summary>The physical unit in this convention, or null when unspecified or context-dependent.</summary>
    public string? Unit { get; }
    /// <summary>Whether the keyword is commentary that should be appended rather than replaced.</summary>
    public bool IsRepeatable { get; }
    /// <summary>The CLR value type used by typed access.</summary>
    public abstract Type ValueType { get; }
    /// <inheritdoc />
    public override string ToString() => Keyword;
}

/// <summary>A typed FITS keyword; scalar numbers can be read from compatible numeric storage.</summary>
/// <typeparam name="T">The string, Boolean, Int64 or Double value type.</typeparam>
public sealed class FitsKeywordKey<T> : FitsKeywordKey
{
    internal FitsKeywordKey(string keyword, MetadataDefinitionSource source, string sourceReference, string? unit = null, bool repeatable = false)
        : base(keyword, source, sourceReference, unit, repeatable) { }
    /// <inheritdoc />
    public override Type ValueType => typeof(T);

    internal bool TryRead(object? raw, out T value)
    {
        value = default!;
        if (raw is null) return false;
        if (raw is T typed) { value = typed; return true; }
        // Do not turn a numeric APERTURE into an aperture name, or a Boolean into a number.
        if (typeof(T) == typeof(string) || typeof(T) == typeof(bool) || raw is bool) return false;
        try
        {
            if (raw is string text)
            {
                if (typeof(T) == typeof(double) && double.TryParse(text.Replace('D', 'E'), NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
                { value = (T)(object)number; return true; }
                if (typeof(T) == typeof(long) && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long integer))
                { value = (T)(object)integer; return true; }
                return false;
            }
            if (raw is not (byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal)) return false;
            if (typeof(T) == typeof(long))
            {
                if (raw is double floating)
                {
                    if (!double.IsFinite(floating) || floating != Math.Truncate(floating)) return false;
                    value = (T)(object)checked((long)floating);
                    return true;
                }
                if (raw is float single)
                {
                    if (!float.IsFinite(single) || single != MathF.Truncate(single)) return false;
                    value = (T)(object)checked((long)single);
                    return true;
                }
                decimal number = Convert.ToDecimal(raw, CultureInfo.InvariantCulture);
                if (number != decimal.Truncate(number)) return false;
                value = (T)(object)checked((long)number);
                return true;
            }
            value = ApiContracts.ConvertValue<T>(raw);
            return true;
        }
        catch (Exception e) when (e is FormatException or InvalidCastException or OverflowException or ArgumentException) { return false; }
    }
}
