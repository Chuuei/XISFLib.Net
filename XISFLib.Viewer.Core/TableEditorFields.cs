namespace XISFLib.Viewer;

/// <summary>First-table text editing with preservation of content that cannot be represented by this syntax.</summary>
public sealed class TableEditorFields
{
    private XisfTableData? _original;
    public string Id { get; set; } = "";
    public string Caption { get; set; } = "";
    public string Fields { get; set; } = "";
    public string Rows { get; set; } = "";
    public bool IsReadOnly { get; private set; }
    public bool CanEdit => !IsReadOnly;
    public string Notice => IsReadOnly ? "This table contains unsupported types or delimiters. It is preserved unchanged; use the typed library API to edit it."
        : "First table only. Fields: id|type|header|format. Rows: one line per row with | separated cells. Blank identifier leaves an existing table unchanged.";

    public static TableEditorFields FromTable(XisfTableData? table)
    {
        var editor = new TableEditorFields { _original = table, Id = table?.Id ?? "", Caption = table?.Caption ?? "" };
        if (table is null) return editor;
        editor.IsReadOnly = table.Fields.Any(f => !ViewerEditorCodec.CanEditType(f.Type) ||
            f.Type.EndsWith("Vector", StringComparison.OrdinalIgnoreCase) || f.Type.EndsWith("Matrix", StringComparison.OrdinalIgnoreCase) || f.Type.Equals("ByteArray", StringComparison.OrdinalIgnoreCase) ||
            new[] { f.Id, f.Header, f.Format }.Any(HasDelimiter)) || table.Rows.Any(row => row.Any(value =>
                value is not (null or string or bool or DateTimeOffset or sbyte or byte or short or ushort or int or uint or long or ulong or float or double) ||
                value is null || value is string text && (HasDelimiter(text) || text != text.Trim()))) ||
            table.Fields.Count == 1 && table.Rows.Any(row => row.Count() == 1 && row[0] is string { Length: 0 });
        if (!editor.IsReadOnly)
        {
            editor.Fields = string.Join(Environment.NewLine, table.Fields.Select(f => $"{f.Id}|{f.Type}|{f.Header}|{f.Format}"));
            editor.Rows = string.Join(Environment.NewLine, table.Rows.Select(row => string.Join('|', row.Select(ViewerEditorCodec.FormatEditorValue))));
        }
        return editor;
    }

    public XisfTableData? Parse()
    {
        if (IsReadOnly) return _original;
        var table = ViewerEditorCodec.ParseTable(Id, Caption, Fields, Rows);
        if (table is null) return null;
        var preserved = new XisfTableData { Id = table.Id, Caption = table.Caption, Comment = _original?.Comment };
        foreach (var field in table.Fields) preserved.Fields.Add(field);
        foreach (var row in table.Rows) preserved.Rows.Add(row);
        return preserved;
    }

    private static bool HasDelimiter(string? value) => value?.IndexOfAny(['|', '\r', '\n']) >= 0;
}
