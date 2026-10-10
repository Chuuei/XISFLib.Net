namespace XISFLib;

public static partial class XisfAdditionalProperties
{

    /// <summary>Typed metadata in the Instrument group.</summary>
    public static class Instrument
    {
        /// <summary>Instrument:FrameExposureTime (Float64); application metadata or a library storage hint, not a reserved XISF property.</summary>
        public static XisfPropertyKey<double> FrameExposureTime { get; } = new("Instrument:FrameExposureTime", "Float64", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
    }
}
