namespace XISFLib.Viewer;

/// <summary>A framework-independent information node. Expansion defaults belong to the frontend container.</summary>
public sealed class MetadataNode
{
    public MetadataNode(string name, string? value = null, IReadOnlyList<MetadataNode>? children = null,
        bool isExpanded = false)
    {
        Name = name;
        Value = value;
        Children = children ?? Array.Empty<MetadataNode>();
        IsExpanded = isExpanded;
    }

    public string Name { get; }
    public string? Value { get; }
    public string? DisplayValue => Value is null ? null : MetadataText.Preview(Value);
    public IReadOnlyList<MetadataNode> Children { get; }
    public bool IsExpanded { get; }
}
