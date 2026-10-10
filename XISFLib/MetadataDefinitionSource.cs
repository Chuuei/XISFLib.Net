namespace XISFLib;

/// <summary>The authority or evidence behind a metadata definition.</summary>
public enum MetadataDefinitionSource
{
    /// <summary>A definition reserved by the format specification.</summary>
    Specification,
    /// <summary>A published community or application convention.</summary>
    PublishedConvention,
    /// <summary>A name and storage type observed in the repository's example files.</summary>
    ObservedExamples,
    /// <summary>A storage hint used by this library's format conversion.</summary>
    LibraryConvention
}
