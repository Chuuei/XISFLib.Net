namespace XISFLib;

public static partial class XisfAdditionalProperties
{

    /// <summary>Typed metadata in the FitsStorage group.</summary>
    public static class FitsStorage
    {
        /// <summary>FITS:BITPIX (Int32); application metadata or a library storage hint, not a reserved XISF property.</summary>
        public static XisfPropertyKey<int> BITPIX { get; } = new("FITS:BITPIX", "Int32", MetadataDefinitionSource.LibraryConvention, "Examples/metadata-inventory.json");
        /// <summary>FITS:BLANK (Int64); application metadata or a library storage hint, not a reserved XISF property.</summary>
        public static XisfPropertyKey<long> BLANK { get; } = new("FITS:BLANK", "Int64", MetadataDefinitionSource.LibraryConvention, "docs/io-and-limitations.md");
        /// <summary>FITS:BSCALE (Float64); application metadata or a library storage hint, not a reserved XISF property.</summary>
        public static XisfPropertyKey<double> BSCALE { get; } = new("FITS:BSCALE", "Float64", MetadataDefinitionSource.LibraryConvention, "Examples/metadata-inventory.json");
        /// <summary>FITS:BZERO (Float64); application metadata or a library storage hint, not a reserved XISF property.</summary>
        public static XisfPropertyKey<double> BZERO { get; } = new("FITS:BZERO", "Float64", MetadataDefinitionSource.LibraryConvention, "Examples/metadata-inventory.json");
    }
}
