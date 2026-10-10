namespace XISFLib;

public static partial class FitsKnownKeywords
{
    /// <summary>Image WCS keyword families, including alternate coordinate systems; units are specified by CUNIT.</summary>
    public static class Wcs
    {
        /// <summary>Constructs the CDELT axis keyword.</summary>
        /// <param name="axis">The axis number, from 1 through 99.</param>
        /// <param name="alternate">An optional alternate system code from A through Z.</param>
        /// <returns>The typed WCS key.</returns>
        public static FitsKeywordKey<double> CDELT(int axis, char? alternate = null) => new(WcsAxis("CDELT", axis, alternate), MetadataDefinitionSource.Specification, WcsReference);
        /// <summary>Constructs the CNAME axis keyword.</summary>
        /// <param name="axis">The axis number, from 1 through 99.</param>
        /// <param name="alternate">An optional alternate system code from A through Z.</param>
        /// <returns>The typed WCS key.</returns>
        public static FitsKeywordKey<string> CNAME(int axis, char? alternate = null) => new(WcsAxis("CNAME", axis, alternate), MetadataDefinitionSource.Specification, WcsReference);
        /// <summary>Constructs the CRDER axis keyword.</summary>
        /// <param name="axis">The axis number, from 1 through 99.</param>
        /// <param name="alternate">An optional alternate system code from A through Z.</param>
        /// <returns>The typed WCS key.</returns>
        public static FitsKeywordKey<double> CRDER(int axis, char? alternate = null) => new(WcsAxis("CRDER", axis, alternate), MetadataDefinitionSource.Specification, WcsReference);
        /// <summary>Constructs the CRPIX axis keyword.</summary>
        /// <param name="axis">The axis number, from 1 through 99.</param>
        /// <param name="alternate">An optional alternate system code from A through Z.</param>
        /// <returns>The typed WCS key.</returns>
        public static FitsKeywordKey<double> CRPIX(int axis, char? alternate = null) => new(WcsAxis("CRPIX", axis, alternate), MetadataDefinitionSource.Specification, WcsReference);
        /// <summary>Constructs the CRVAL axis keyword.</summary>
        /// <param name="axis">The axis number, from 1 through 99.</param>
        /// <param name="alternate">An optional alternate system code from A through Z.</param>
        /// <returns>The typed WCS key.</returns>
        public static FitsKeywordKey<double> CRVAL(int axis, char? alternate = null) => new(WcsAxis("CRVAL", axis, alternate), MetadataDefinitionSource.Specification, WcsReference);
        /// <summary>Constructs the CSYER axis keyword.</summary>
        /// <param name="axis">The axis number, from 1 through 99.</param>
        /// <param name="alternate">An optional alternate system code from A through Z.</param>
        /// <returns>The typed WCS key.</returns>
        public static FitsKeywordKey<double> CSYER(int axis, char? alternate = null) => new(WcsAxis("CSYER", axis, alternate), MetadataDefinitionSource.Specification, WcsReference);
        /// <summary>Constructs the CTYPE axis keyword.</summary>
        /// <param name="axis">The axis number, from 1 through 99.</param>
        /// <param name="alternate">An optional alternate system code from A through Z.</param>
        /// <returns>The typed WCS key.</returns>
        public static FitsKeywordKey<string> CTYPE(int axis, char? alternate = null) => new(WcsAxis("CTYPE", axis, alternate), MetadataDefinitionSource.Specification, WcsReference);
        /// <summary>Constructs the CUNIT axis keyword.</summary>
        /// <param name="axis">The axis number, from 1 through 99.</param>
        /// <param name="alternate">An optional alternate system code from A through Z.</param>
        /// <returns>The typed WCS key.</returns>
        public static FitsKeywordKey<string> CUNIT(int axis, char? alternate = null) => new(WcsAxis("CUNIT", axis, alternate), MetadataDefinitionSource.Specification, WcsReference);
        /// <summary>Constructs the CD matrix or projection parameter keyword.</summary>
        /// <param name="axis">The axis number, from 1 through 99.</param>
        /// <param name="index">The second axis (1 through 99) or projection parameter (0 through 99).</param>
        /// <param name="alternate">An optional alternate system code from A through Z.</param>
        /// <returns>The typed WCS key.</returns>
        public static FitsKeywordKey<double> CD(int axis, int index, char? alternate = null) => new(WcsPair("CD", axis, index, alternate, false), MetadataDefinitionSource.Specification, WcsReference);
        /// <summary>Constructs the PC matrix or projection parameter keyword.</summary>
        /// <param name="axis">The axis number, from 1 through 99.</param>
        /// <param name="index">The second axis (1 through 99) or projection parameter (0 through 99).</param>
        /// <param name="alternate">An optional alternate system code from A through Z.</param>
        /// <returns>The typed WCS key.</returns>
        public static FitsKeywordKey<double> PC(int axis, int index, char? alternate = null) => new(WcsPair("PC", axis, index, alternate, false), MetadataDefinitionSource.Specification, WcsReference);
        /// <summary>Constructs the PV matrix or projection parameter keyword.</summary>
        /// <param name="axis">The axis number, from 1 through 99.</param>
        /// <param name="index">The second axis (1 through 99) or projection parameter (0 through 99).</param>
        /// <param name="alternate">An optional alternate system code from A through Z.</param>
        /// <returns>The typed WCS key.</returns>
        public static FitsKeywordKey<double> PV(int axis, int index, char? alternate = null) => new(WcsPair("PV", axis, index, alternate, true), MetadataDefinitionSource.Specification, WcsReference);
        /// <summary>Constructs the PS matrix or projection parameter keyword.</summary>
        /// <param name="axis">The axis number, from 1 through 99.</param>
        /// <param name="index">The second axis (1 through 99) or projection parameter (0 through 99).</param>
        /// <param name="alternate">An optional alternate system code from A through Z.</param>
        /// <returns>The typed WCS key.</returns>
        public static FitsKeywordKey<string> PS(int axis, int index, char? alternate = null) => new(WcsPair("PS", axis, index, alternate, true), MetadataDefinitionSource.Specification, WcsReference);
    }
}
