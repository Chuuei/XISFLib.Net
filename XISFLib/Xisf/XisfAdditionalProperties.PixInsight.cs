namespace XISFLib;

public static partial class XisfAdditionalProperties
{

    /// <summary>Typed metadata in the PixInsight group.</summary>
    public static class PixInsight
    {
        /// <summary>PixInsight:ProcessingHistory (String); application metadata or a library storage hint, not a reserved XISF property.</summary>
        public static XisfPropertyKey<string> ProcessingHistory { get; } = new("PixInsight:ProcessingHistory", "String", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
    }
}
