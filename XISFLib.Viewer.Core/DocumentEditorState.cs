using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace XISFLib.Viewer;

/// <summary>Document property rows and an atomically parsed snapshot for selected-image output.</summary>
public sealed class DocumentEditorState : ObservableObject
{
    private string _creatorApplication = "XISFLib.AvaloniaViewer";
    public string CreatorApplication { get => _creatorApplication; set => SetProperty(ref _creatorApplication, value); }
    public ObservableCollection<PropertyEditorRow> Metadata { get; } = [];
    public ObservableCollection<PropertyEditorRow> Properties { get; } = [];
    public DocumentEditorSnapshot Applied { get; private set; } = Parse([], [], "XISFLib.AvaloniaViewer");

    public void Load(XisfDocument? document)
    {
        Metadata.Clear(); Properties.Clear();
        if (document is not null)
        {
            foreach (var pair in document.Metadata.Where(p => p.Key is not ("XISF:CreationTime" or "XISF:CreatorApplication")))
                Metadata.Add(ViewerEditorCodec.ToEditorRow(pair));
            foreach (var pair in document.Properties) Properties.Add(ViewerEditorCodec.ToEditorRow(pair));
        }
        Apply();
    }

    public void Apply() => Applied = Parse(Metadata, Properties, CreatorApplication);

    public static DocumentEditorSnapshot Parse(IEnumerable<PropertyEditorRow> metadata, IEnumerable<PropertyEditorRow> properties, string creator)
    {
        var parsedMetadata = ViewerEditorCodec.ParseProperties(metadata);
        var parsedProperties = ViewerEditorCodec.ParseProperties(properties);
        return new(new ReadOnlyDictionary<string, object?>(parsedMetadata), new ReadOnlyDictionary<string, object?>(parsedProperties), creator.Trim());
    }
}

/// <summary>Parsed document settings. Output creation metadata remains governed by writer policy.</summary>
public sealed record DocumentEditorSnapshot(IReadOnlyDictionary<string, object?> Metadata, IReadOnlyDictionary<string, object?> Properties, string CreatorApplication)
{
    public XisfDocumentData CreateContent(XisfImageData selectedImage)
    {
        var content = new XisfDocumentData(); content.Images.Add(selectedImage);
        foreach (var pair in Metadata) content.Metadata.Add(pair.Key, pair.Value);
        foreach (var pair in Properties) content.Properties.Add(pair.Key, pair.Value);
        return content;
    }
}
