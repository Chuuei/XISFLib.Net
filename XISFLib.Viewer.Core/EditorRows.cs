namespace XISFLib.Viewer;

public sealed class PropertyEditorRow : System.ComponentModel.INotifyPropertyChanged
{
    private string? _value = "";
    private string _id = "";
    internal Func<string>? DeferredValue { get; set; }
    internal string? DeferredPreview { get; set; }
    internal XisfPropertyData? DeferredOriginal { get; set; }
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public string Id
    {
        get => _id;
        set { _id = value; PropertyChanged?.Invoke(this, new(nameof(Id))); PropertyChanged?.Invoke(this, new(nameof(IdentifierPreview))); }
    }
    public string IdentifierPreview => MetadataText.Preview(Id);
    public string Type { get; set; } = "String";
    public string Value
    {
        get => _value ??= DeferredValue?.Invoke() ?? "";
        set
        {
            _value = value; DeferredValue = null; DeferredPreview = null; DeferredOriginal = null;
            PropertyChanged?.Invoke(this, new(nameof(Value)));
            PropertyChanged?.Invoke(this, new(nameof(InlineValue)));
        }
    }
    // Only this bounded value is bound to grid cells. Never bind a full serialized model to a text control.
    public string InlineValue { get => DeferredPreview ?? MetadataText.Preview(Value); set => Value = value; }
    public bool RequiresValueDialog => DeferredPreview is not null || Value.Length > MetadataText.InlineLimit || Value.Contains('\n') || Value.Contains('\r');
    internal void DeferValue(XisfPropertyData property)
    {
        _value = null; DeferredOriginal = property;
        DeferredValue = () => ViewerEditorCodec.FormatEditorValue(property.Value);
        DeferredPreview = $"{((Array)property.Value!).Length:N0} elements — open value…";
    }
    public string? Format { get; set; }
    public string? Comment { get; set; }
    public int? Rows { get; set; }
    public int? Columns { get; set; }
    public bool IsReadOnly { get; init; }
    internal object? OriginalValue { get; init; }
    internal string? OriginalId { get; init; }
}

public sealed class FitsKeywordEditorRow
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
    public string Comment { get; set; } = "";
}
