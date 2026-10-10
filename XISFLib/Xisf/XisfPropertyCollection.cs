using System.Collections.ObjectModel;

namespace XISFLib;

/// <summary>Editable properties with typed construction and explicit missing-value access.</summary>
public sealed class XisfPropertyCollection : IDictionary<string, object?>
{
    // Dictionary composition avoids exposing a mutable parsed-property dictionary.
    private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);
    /// <summary>Gets or replaces a property by its case-sensitive identifier.</summary>
    public object? this[string key] { get => _values[key]; set { ValidateId(key); _values[key] = value; } }
    /// <summary>The property identifiers.</summary>
    public ICollection<string> Keys => _values.Keys;
    /// <summary>The stored values.</summary>
    public ICollection<object?> Values => _values.Values;
    /// <summary>The number of properties.</summary>
    public int Count => _values.Count;
    bool ICollection<KeyValuePair<string, object?>>.IsReadOnly => false;
    /// <summary>Stores a typed property, inferring its XISF type and validating support immediately.</summary>
    /// <typeparam name="T">A supported scalar, vector or matrix CLR type.</typeparam>
    /// <param name="id">The property identifier.</param>
    /// <param name="value">The CLR value.</param>
    /// <param name="format">Optional presentation format.</param>
    /// <param name="comment">Optional comment.</param>
    public void Set<T>(string id, T value, string? format = null, string? comment = null) => this[id] = XisfPropertyData.FromValue(value, format, comment);
    /// <summary>Reads a required typed property.</summary>
    /// <typeparam name="T">The requested CLR type.</typeparam>
    /// <param name="id">The property identifier.</param>
    /// <returns>The converted value.</returns>
    public T GetRequiredValue<T>(string id)
    {
        if (!_values.TryGetValue(id, out var raw)) throw new KeyNotFoundException($"Required XISF property '{id}' is absent.");
        return XisfPropertyAccess.Required<T>(raw, id);
    }
    /// <summary>Tries to read an available property as the requested type.</summary>
    /// <typeparam name="T">The CLR type.</typeparam>
    /// <param name="id">The identifier.</param>
    /// <param name="value">The converted value.</param>
    /// <returns>Whether the property was available and compatible.</returns>
    public bool TryGetValue<T>(string id, out T value) => XisfPropertyAccess.Try(_values.GetValueOrDefault(id), out value);
    /// <inheritdoc />
    public void Add(string key, object? value) { ValidateId(key); _values.Add(key, value); }
    /// <inheritdoc />
    public bool ContainsKey(string key) => _values.ContainsKey(key);
    /// <inheritdoc />
    public bool Remove(string key) => _values.Remove(key);
    /// <inheritdoc />
    public bool TryGetValue(string key, out object? value) => _values.TryGetValue(key, out value);
    /// <inheritdoc />
    public void Clear() => _values.Clear();
    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => _values.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    void ICollection<KeyValuePair<string, object?>>.Add(KeyValuePair<string, object?> item) => Add(item.Key, item.Value);
    bool ICollection<KeyValuePair<string, object?>>.Contains(KeyValuePair<string, object?> item) => ((ICollection<KeyValuePair<string, object?>>)_values).Contains(item);
    void ICollection<KeyValuePair<string, object?>>.CopyTo(KeyValuePair<string, object?>[] array, int index) => ((ICollection<KeyValuePair<string, object?>>)_values).CopyTo(array, index);
    bool ICollection<KeyValuePair<string, object?>>.Remove(KeyValuePair<string, object?> item) => ((ICollection<KeyValuePair<string, object?>>)_values).Remove(item);
    private static void ValidateId(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!System.Text.RegularExpressions.Regex.IsMatch(id, "^[_a-zA-Z][_a-zA-Z0-9]*(:[_a-zA-Z][_a-zA-Z0-9]*)*$", System.Text.RegularExpressions.RegexOptions.CultureInvariant))
            throw new ArgumentException("Invalid XISF property identifier.", nameof(id));
    }
}

/// <summary>Explicit typed access to parsed properties, including availability checks.</summary>
public static class XisfPropertyExtensions
{
    /// <summary>Reads a required available property as the requested type.</summary>
    /// <typeparam name="T">The CLR type.</typeparam>
    /// <param name="properties">Parsed properties.</param>
    /// <param name="id">The case-sensitive identifier.</param>
    /// <returns>The value, with owned copies for arrays.</returns>
    public static T GetRequiredValue<T>(this IReadOnlyDictionary<string, XisfProperty> properties, string id)
    {
        ArgumentNullException.ThrowIfNull(properties);
        if (!properties.TryGetValue(id, out var property)) throw new KeyNotFoundException($"Required XISF property '{id}' is absent.");
        return XisfPropertyAccess.Required<T>(property, id);
    }
    /// <summary>Tries to read an available compatible property.</summary>
    /// <typeparam name="T">The CLR type.</typeparam>
    /// <param name="properties">Parsed properties.</param>
    /// <param name="id">The identifier.</param>
    /// <param name="value">The decoded value.</param>
    /// <returns>Whether the value was available and compatible.</returns>
    public static bool TryGetValue<T>(this IReadOnlyDictionary<string, XisfProperty> properties, string id, out T value)
    {
        ArgumentNullException.ThrowIfNull(properties);
        return XisfPropertyAccess.Try(properties.GetValueOrDefault(id), out value);
    }
}

internal static class XisfPropertyAccess
{
    public static T Required<T>(object? raw, string id)
    {
        if (!Try<T>(raw, out var value)) throw new InvalidOperationException($"Property '{id}' is unavailable or incompatible with {typeof(T).Name}.");
        return value;
    }
    public static bool Try<T>(object? raw, out T value)
    {
        value = default!;
        if (raw is XisfProperty property) { if (!property.IsAvailable) return false; raw = property.Value; }
        else if (raw is XisfPropertyData data) raw = data.Value;
        if (raw is null) return false;
        try { value = ApiContracts.ConvertValue<T>(raw); return true; }
        catch (Exception e) when (e is FormatException or InvalidCastException or OverflowException or ArgumentException) { return false; }
    }
}
