using System.Xml.Linq;

namespace XISFLib;

/// <summary>An editable XISF document. Content is independent of serialization options.</summary>
public sealed class XisfDocumentData
{
    /// <summary>Images in serialization order.</summary>
    public IList<XisfImageData> Images { get; } = [];
    /// <summary>Document properties, using case-sensitive identifiers.</summary>
    public XisfPropertyCollection Properties { get; } = new();
    /// <summary>Creation metadata; identifiers must begin with XISF:.</summary>
    public XisfPropertyCollection Metadata { get; } = new();
    /// <summary>Document-level tables.</summary>
    public IList<XisfTableData> Tables { get; } = [];
    /// <summary>Application-defined extension XML, in non-XISF namespaces.</summary>
    public IList<XElement> Extensions { get; } = [];
    internal IList<XisfPreservedElement> PreservedElements { get; } = [];

    /// <summary>Creates an editable document with materialized pixels and preserved opaque objects.</summary>
    /// <param name="document">The parsed document. Keep its block sources available through saving for opaque metadata.</param>
    /// <returns>An editable document retaining global and image content. Original signatures are not retained.</returns>
    public static XisfDocumentData FromDocument(XisfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var result = new XisfDocumentData();
        foreach (var pair in document.Properties) result.Properties.Add(pair.Key, pair.Value);
        foreach (var pair in document.Metadata) result.Metadata.Add(pair.Key, pair.Value);
        foreach (var image in document.Images) result.Images.Add(XisfImageData.FromImage(image));
        foreach (var table in document.Tables) result.Tables.Add(CopyTable(table));
        foreach (var element in document.XmlCore.Root!.Elements().Where(e => e.Name.Namespace != document.XmlCore.Root.Name.Namespace && e.Name != XName.Get("Signature", "http://www.w3.org/2000/09/xmldsig#")))
            result.PreservedElements.Add(new(new XElement(element), document.Source));
        foreach (var unavailable in document.UnavailableObjects)
            result.PreservedElements.Add(new(unavailable.Xml, document.Source));
        return result;
    }

    internal static XisfTableData CopyTable(XisfTable table)
    {
        var copy = new XisfTableData { Id = table.Id, Caption = table.Caption, Comment = table.Comment };
        foreach (var field in table.Fields) copy.Fields.Add(new(field.Id, field.Type, field.Header, field.Format));
        foreach (var row in table.Rows) copy.Rows.Add(row.Cells.Select(cell => cell.IsAvailable ? cell.Value : cell).ToArray());
        return copy;
    }
}

internal sealed record XisfPreservedElement(XElement Xml, XisfDataSource Source);
