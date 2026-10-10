namespace XISFLib;

public sealed partial class FitsHeader
{
    /// <summary>Stores a typed keyword, using the existing structural-header guards.</summary>
    /// <typeparam name="T">The key's CLR value type.</typeparam>
    /// <param name="key">A keyword definition from FitsKnownKeywords.</param>
    /// <param name="value">The CLR value; strings are quoted by the writer.</param>
    /// <param name="comment">The optional comment.</param>
    public void Set<T>(FitsKeywordKey<T> key, T value, string? comment = null)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        if (key.IsRepeatable) throw new InvalidOperationException($"Use Add for repeated {key.Keyword} commentary.");
        Set(key.Keyword, value, comment);
    }
    /// <summary>Appends a typed card, preserving ordered commentary and duplicate keywords.</summary>
    /// <typeparam name="T">The key's CLR value type.</typeparam>
    /// <param name="key">The keyword definition.</param>
    /// <param name="value">The CLR value.</param>
    /// <param name="comment">The optional comment.</param>
    public void Add<T>(FitsKeywordKey<T> key, T value, string? comment = null)
    { ArgumentNullException.ThrowIfNull(key); ArgumentNullException.ThrowIfNull(value); Add(new FitsHeaderCard(key.Keyword, value, comment)); }
    /// <summary>Reads a required typed keyword; absence or incompatible representation throws.</summary>
    /// <typeparam name="T">The key's CLR value type.</typeparam>
    /// <param name="key">The keyword definition.</param>
    /// <returns>The converted first matching value.</returns>
    public T GetRequiredValue<T>(FitsKeywordKey<T> key)
    {
        ArgumentNullException.ThrowIfNull(key);
        object? raw = FindFirst(key.Keyword)?.Value;
        if (raw is null) throw new KeyNotFoundException($"Required FITS keyword '{key.Keyword}' is absent or has no value.");
        if (!key.TryRead(raw, out var value)) throw new FormatException($"FITS keyword '{key.Keyword}' cannot be read as {typeof(T).Name} in this convention.");
        return value;
    }
    /// <summary>Tries to read a compatible first matching keyword value, distinguishing absence from zero.</summary>
    /// <typeparam name="T">The key's CLR value type.</typeparam>
    /// <param name="key">The keyword definition.</param>
    /// <param name="value">The value when successful.</param>
    /// <returns>Whether a compatible value was read.</returns>
    public bool TryGetValue<T>(FitsKeywordKey<T> key, out T value)
    { ArgumentNullException.ThrowIfNull(key); return key.TryRead(FindFirst(key.Keyword)?.Value, out value); }
    /// <summary>Returns a fallback for absent or null values; incompatible stored values throw.</summary>
    /// <typeparam name="T">The key's CLR value type.</typeparam>
    /// <param name="key">The keyword definition.</param>
    /// <param name="defaultValue">The explicit fallback.</param>
    /// <returns>The converted value or fallback.</returns>
    public T GetValueOrDefault<T>(FitsKeywordKey<T> key, T defaultValue = default!)
    { ArgumentNullException.ThrowIfNull(key); return FindFirst(key.Keyword)?.Value is null ? defaultValue : GetRequiredValue(key); }
    /// <summary>Returns every matching card, including ordered commentary.</summary>
    /// <param name="key">The keyword definition.</param>
    /// <returns>The matching cards in header order.</returns>
    public IEnumerable<FitsHeaderCard> Find(FitsKeywordKey key)
    { ArgumentNullException.ThrowIfNull(key); return Find(key.Keyword); }
    /// <summary>Removes all matching cards; image structural-header guards still apply.</summary>
    /// <param name="key">The keyword definition.</param>
    /// <returns>Whether any cards were removed.</returns>
    public bool Remove(FitsKeywordKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        bool removed = false;
        for (int index = Count - 1; index >= 0; index--)
            if (this[index].Keyword.Equals(key.Keyword, StringComparison.OrdinalIgnoreCase)) { RemoveAt(index); removed = true; }
        return removed;
    }
}
