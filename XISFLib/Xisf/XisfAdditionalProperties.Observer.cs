namespace XISFLib;

public static partial class XisfAdditionalProperties
{

    /// <summary>Typed metadata in the Observer group.</summary>
    public static class Observer
    {
        /// <summary>Observer:Name (String); application metadata or a library storage hint, not a reserved XISF property.</summary>
        public static XisfPropertyKey<string> Name { get; } = new("Observer:Name", "String", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
    }
}
