using System.Globalization;

namespace XISFLib.Viewer;

/// <summary>Shared text-editor parsing, formatting and preservation rules for the viewers.</summary>
public static class ViewerEditorCodec
{
    private static readonly HashSet<string> EditableTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "String", "Boolean", "Bool", "Int8", "UInt8", "Byte", "Int16", "UInt16", "Int32", "Int", "UInt32", "UInt", "Int64", "UInt64",
        "Float32", "Float", "Float64", "Double", "TimePoint", "ByteArray", "IVector", "UIVector", "Vector", "ByteMatrix", "IMatrix", "UIMatrix", "Matrix"
    };

    static ViewerEditorCodec()
    {
        foreach (string prefix in new[] { "I8", "UI8", "I16", "UI16", "I32", "UI32", "I64", "UI64", "I128", "UI128", "F32", "F64" })
        {
            EditableTypes.Add(prefix + "Vector"); EditableTypes.Add(prefix + "Matrix");
        }
    }

    public static bool CanEditType(string type) => EditableTypes.Contains(type);

    public static PropertyEditorRow ToEditorRow(string id, object? value) => value switch
    {
        XisfPropertyData property => ToEditorRow(id, property),
        XisfProperty property when property.IsAvailable && CanEditType(property.Type) => ToEditorRow(id, new XisfPropertyData(property.Type, property.Value, property.Format, property.Comment)),
        XisfProperty property => new() { Id = id, Type = property.Type, Value = "Preserved; text editing unavailable", Format = property.Format, Comment = property.Comment,
            IsReadOnly = true, OriginalId = id, OriginalValue = property },
        string or bool or DateTimeOffset or sbyte or byte or short or ushort or int or uint or long or ulong or float or double =>
            ToEditorRow(id, new XisfPropertyData(PropertyTypeName(value), value)),
        _ => new() { Id = id, Type = value?.GetType().Name ?? "Unavailable", Value = "Preserved; text editing unavailable",
            IsReadOnly = true, OriginalId = id, OriginalValue = value }
    };

    public static Dictionary<string, object?> ParseProperties(IEnumerable<PropertyEditorRow> rows)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Id)) continue;
            string id = row.Id.Trim();
            if (row.IsReadOnly && id != row.OriginalId) throw new FormatException("Preserved properties cannot be renamed in the text editor.");
            string type = string.IsNullOrWhiteSpace(row.Type) ? "String" : row.Type.Trim();
            object? value = row.IsReadOnly ? row.OriginalValue : row.DeferredOriginal is { } original &&
                type == original.Type && row.Format == original.Format && row.Comment == original.Comment &&
                row.Rows == (((Array)original.Value!).Rank == 2 ? ((Array)original.Value!).GetLength(0) : (int?)null) &&
                row.Columns == (((Array)original.Value!).Rank == 2 ? ((Array)original.Value!).GetLength(1) : null)
                ? original : new XisfPropertyData(type, ParseProperty(type, row.Value, row.Rows, row.Columns), row.Format, row.Comment);
            if (!result.TryAdd(id, value)) throw new FormatException($"Duplicate property identifier '{id}'.");
        }
        return result;
    }

    public static XisfFitsKeyword[] ParseFitsKeywords(IEnumerable<FitsKeywordEditorRow> rows) => rows
        .Where(row => !string.IsNullOrWhiteSpace(row.Name))
        .Select(row => new XisfFitsKeyword(row.Name.Trim(), row.Value, string.IsNullOrWhiteSpace(row.Comment) ? null : row.Comment.Trim())).ToArray();

    public static void ApplyMetadata(XisfImageData image, IEnumerable<PropertyEditorRow> rows, IEnumerable<FitsKeywordEditorRow> keywords)
    {
        var properties = ParseProperties(rows);
        var fits = ParseFitsKeywords(keywords);
        image.Properties.Clear();
        foreach (var pair in properties) image.Properties.Add(pair.Key, pair.Value);
        image.FitsKeywords.Clear();
        foreach (var keyword in fits) image.FitsKeywords.Add(keyword);
    }

    public static object? ParseProperty(string? type, string value, int? rows = null, int? columns = null)
    {
        string normalized = string.IsNullOrWhiteSpace(type) ? "String" : type.Trim();
        if (!CanEditType(normalized)) throw new NotSupportedException($"Property type '{normalized}' cannot be edited as text. Use the typed library API.");
        string key = normalized.ToLowerInvariant();
        if (key == "string") return value;
        string[] values = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (key == "bytearray") return values.Select(v => byte.Parse(v, CultureInfo.InvariantCulture)).ToArray();
        key = key switch
        {
            "ivector" => "i32vector", "uivector" => "ui32vector", "vector" => "f64vector",
            "bytematrix" => "ui8matrix", "imatrix" => "i32matrix", "uimatrix" => "ui32matrix", "matrix" => "f64matrix",
            _ => key
        };
        if (key.EndsWith("vector") || key.EndsWith("matrix"))
        {
            bool matrix = key.EndsWith("matrix");
            string scalar = key[..^6];
            Array parsed = scalar switch
            {
                "i8" => values.Select(v => sbyte.Parse(v, CultureInfo.InvariantCulture)).ToArray(), "ui8" => values.Select(v => byte.Parse(v, CultureInfo.InvariantCulture)).ToArray(),
                "i16" => values.Select(v => short.Parse(v, CultureInfo.InvariantCulture)).ToArray(), "ui16" => values.Select(v => ushort.Parse(v, CultureInfo.InvariantCulture)).ToArray(),
                "i32" => values.Select(v => int.Parse(v, CultureInfo.InvariantCulture)).ToArray(), "ui32" => values.Select(v => uint.Parse(v, CultureInfo.InvariantCulture)).ToArray(),
                "i64" => values.Select(v => long.Parse(v, CultureInfo.InvariantCulture)).ToArray(), "ui64" => values.Select(v => ulong.Parse(v, CultureInfo.InvariantCulture)).ToArray(),
                "i128" => values.Select(v => Int128.Parse(v, CultureInfo.InvariantCulture)).ToArray(), "ui128" => values.Select(v => UInt128.Parse(v, CultureInfo.InvariantCulture)).ToArray(),
                "f32" => values.Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToArray(),
                _ => values.Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray()
            };
            if (!matrix) return parsed;
            if (rows is null || columns is null || rows < 0 || columns < 0 || checked(rows.Value * columns.Value) != parsed.Length)
                throw new FormatException($"Property '{normalized}' requires valid matrix row and column dimensions.");
            Array shaped = Array.CreateInstance(parsed.GetType().GetElementType()!, rows.Value, columns.Value);
            for (int i = 0; i < parsed.Length; i++) shaped.SetValue(parsed.GetValue(i), i / columns.Value, i % columns.Value);
            return shaped;
        }
        return key switch
        {
            "string" => value, "boolean" or "bool" => bool.Parse(value), "int8" => sbyte.Parse(value, CultureInfo.InvariantCulture),
            "uint8" or "byte" => byte.Parse(value, CultureInfo.InvariantCulture), "int16" => short.Parse(value, CultureInfo.InvariantCulture),
            "uint16" => ushort.Parse(value, CultureInfo.InvariantCulture), "int32" or "int" => int.Parse(value, CultureInfo.InvariantCulture),
            "uint32" or "uint" => uint.Parse(value, CultureInfo.InvariantCulture), "int64" => long.Parse(value, CultureInfo.InvariantCulture),
            "uint64" => ulong.Parse(value, CultureInfo.InvariantCulture), "float32" or "float" => float.Parse(value, CultureInfo.InvariantCulture),
            "float64" or "double" => double.Parse(value, CultureInfo.InvariantCulture), "timepoint" => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            _ => value
        };
    }

    public static PropertyEditorRow ToEditorRow(KeyValuePair<string, XisfProperty> pair) =>
        ToEditorRow(pair.Key, (object)pair.Value);

    public static PropertyEditorRow ToEditorRow(string id, XisfPropertyData property)
    {
        if (!CanEditType(property.Type)) return new() { Id = id, Type = property.Type, Value = "Preserved; text editing unavailable", Format = property.Format,
            Comment = property.Comment, IsReadOnly = true, OriginalId = id, OriginalValue = property };
        Array? array = property.Value as Array;
        var row = new PropertyEditorRow
        {
            Id = id,
            Type = property.Type,
            Value = array is { Length: > 128 } ? "" : FormatEditorValue(property.Value),
            Format = property.Format,
            Comment = property.Comment,
            Rows = array?.Rank == 2 ? array.GetLength(0) : null,
            Columns = array?.Rank == 2 ? array.GetLength(1) : null
        };
        if (array is { Length: > 128 }) row.DeferValue(property);
        return row;
    }

    public static string FormatEditorValue(object? value) => value is null ? "" : value is string text ? text : value is DateTimeOffset timestamp ? timestamp.ToString("O", CultureInfo.InvariantCulture) : value is System.Collections.IEnumerable sequence ? string.Join(", ", sequence.Cast<object?>().Select(FormatEditorValue)) : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
    public static string PropertyTypeName(object? value) => value switch { string => "String", bool => "Boolean", DateTimeOffset => "TimePoint", sbyte => "Int8", byte => "UInt8", short => "Int16", ushort => "UInt16", int => "Int32", uint => "UInt32", long => "Int64", ulong => "UInt64", float => "Float32", double => "Float64", _ => "String" };
    public static XisfTableData? ParseTable(string id, string caption, string fields, string rows)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        XisfTableData table = new() { Id = id.Trim(), Caption = EmptyToNull(caption) };
        foreach (string line in Lines(fields))
        {
            string[] values = line.Split('|');
            if (values.Length < 2 || values.Length > 4) throw new FormatException("Each table field requires id|type with optional header|format; literal pipes cannot be escaped.");
            table.Fields.Add(new XisfTableFieldData(values[0].Trim(), values[1].Trim(), values.Length > 2 ? EmptyToNull(values[2]) : null, values.Length > 3 ? EmptyToNull(values[3]) : null));
        }
        if (table.Fields.Count == 0) throw new FormatException("A table requires at least one field.");
        foreach (string line in Lines(rows))
        {
            string[] values = line.Split('|');
            if (values.Length != table.Fields.Count) throw new FormatException("Each table row must have one cell per field.");
            table.Rows.Add(values.Select((value, index) => ParseProperty(table.Fields[index].Type, value.Trim())).ToArray());
        }
        return table;
    }

    private static IEnumerable<string> Lines(string text) => text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
