namespace XISFLib;

public static partial class FitsKnownKeywords
{
    /// <summary>Typed FITS definitions in the Standard catalog; consult each key's source and units.</summary>
    public static class Standard
    {
        /// <summary>AUTHOR (string); Specification definition.</summary>
        public static FitsKeywordKey<string> AUTHOR { get; } = new("AUTHOR", MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>BITPIX (long); Specification definition.</summary>
        public static FitsKeywordKey<long> BITPIX { get; } = new("BITPIX", MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>BLANK (long); Specification definition.</summary>
        public static FitsKeywordKey<long> BLANK { get; } = new("BLANK", MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>BLOCKED (bool); Specification definition.</summary>
        public static FitsKeywordKey<bool> BLOCKED { get; } = new("BLOCKED", MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>BSCALE (double); Specification definition.</summary>
        public static FitsKeywordKey<double> BSCALE { get; } = new("BSCALE", MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>BUNIT (string); Specification definition.</summary>
        public static FitsKeywordKey<string> BUNIT { get; } = new("BUNIT", MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>BZERO (double); Specification definition.</summary>
        public static FitsKeywordKey<double> BZERO { get; } = new("BZERO", MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>COMMENT (string); Specification definition.</summary>
        public static FitsKeywordKey<string> COMMENT { get; } = new("COMMENT", MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html", repeatable: true);
        /// <summary>DATAMAX (double); Specification definition.</summary>
        public static FitsKeywordKey<double> DATAMAX { get; } = new("DATAMAX", MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>DATAMIN (double); Specification definition.</summary>
        public static FitsKeywordKey<double> DATAMIN { get; } = new("DATAMIN", MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>DATE (string); Specification definition.</summary>
        public static FitsKeywordKey<string> DATE { get; } = new("DATE", MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>DATE-OBS (string); Specification definition.</summary>
        public static FitsKeywordKey<string> DATE_OBS { get; } = new("DATE-OBS", MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>EPOCH (double); Specification definition.</summary>
        public static FitsKeywordKey<double> EPOCH { get; } = new("EPOCH", MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>EQUINOX (double); Specification definition.</summary>
        public static FitsKeywordKey<double> EQUINOX { get; } = new("EQUINOX", MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>EXTEND (bool); Specification definition.</summary>
        public static FitsKeywordKey<bool> EXTEND { get; } = new("EXTEND", MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>EXTLEVEL (long); Specification definition.</summary>
        public static FitsKeywordKey<long> EXTLEVEL { get; } = new("EXTLEVEL", MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>EXTNAME (string); Specification definition.</summary>
        public static FitsKeywordKey<string> EXTNAME { get; } = new("EXTNAME", MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>EXTVER (long); Specification definition.</summary>
        public static FitsKeywordKey<long> EXTVER { get; } = new("EXTVER", MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>GCOUNT (long); Specification definition.</summary>
        public static FitsKeywordKey<long> GCOUNT { get; } = new("GCOUNT", MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>GROUPS (bool); Specification definition.</summary>
        public static FitsKeywordKey<bool> GROUPS { get; } = new("GROUPS", MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>HISTORY (string); Specification definition.</summary>
        public static FitsKeywordKey<string> HISTORY { get; } = new("HISTORY", MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html", repeatable: true);
        /// <summary>INSTRUME (string); Specification definition.</summary>
        public static FitsKeywordKey<string> INSTRUME { get; } = new("INSTRUME", MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>NAXIS (long); Specification definition.</summary>
        public static FitsKeywordKey<long> NAXIS { get; } = new("NAXIS", MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>OBJECT (string); Specification definition.</summary>
        public static FitsKeywordKey<string> OBJECT { get; } = new("OBJECT", MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>OBSERVER (string); Specification definition.</summary>
        public static FitsKeywordKey<string> OBSERVER { get; } = new("OBSERVER", MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>ORIGIN (string); Specification definition.</summary>
        public static FitsKeywordKey<string> ORIGIN { get; } = new("ORIGIN", MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>PCOUNT (long); Specification definition.</summary>
        public static FitsKeywordKey<long> PCOUNT { get; } = new("PCOUNT", MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>REFERENC (string); Specification definition.</summary>
        public static FitsKeywordKey<string> REFERENC { get; } = new("REFERENC", MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>SIMPLE (bool); Specification definition.</summary>
        public static FitsKeywordKey<bool> SIMPLE { get; } = new("SIMPLE", MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>TELESCOP (string); Specification definition.</summary>
        public static FitsKeywordKey<string> TELESCOP { get; } = new("TELESCOP", MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>TFIELDS (long); Specification definition.</summary>
        public static FitsKeywordKey<long> TFIELDS { get; } = new("TFIELDS", MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>THEAP (long); Specification definition.</summary>
        public static FitsKeywordKey<long> THEAP { get; } = new("THEAP", MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>XTENSION (string); Specification definition.</summary>
        public static FitsKeywordKey<string> XTENSION { get; } = new("XTENSION", MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>Constructs the CDELTn indexed keyword.</summary>
        /// <param name="index">The one-based axis, column or parameter index; the full keyword must fit in eight characters.</param>
        /// <returns>The typed indexed keyword.</returns>
        public static FitsKeywordKey<double> CDELT(int index) => new(Indexed("CDELT", index), MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>Constructs the CROTAn indexed keyword.</summary>
        /// <param name="index">The one-based axis, column or parameter index; the full keyword must fit in eight characters.</param>
        /// <returns>The typed indexed keyword.</returns>
        public static FitsKeywordKey<double> CROTA(int index) => new(Indexed("CROTA", index), MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>Constructs the CRPIXn indexed keyword.</summary>
        /// <param name="index">The one-based axis, column or parameter index; the full keyword must fit in eight characters.</param>
        /// <returns>The typed indexed keyword.</returns>
        public static FitsKeywordKey<double> CRPIX(int index) => new(Indexed("CRPIX", index), MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>Constructs the CRVALn indexed keyword.</summary>
        /// <param name="index">The one-based axis, column or parameter index; the full keyword must fit in eight characters.</param>
        /// <returns>The typed indexed keyword.</returns>
        public static FitsKeywordKey<double> CRVAL(int index) => new(Indexed("CRVAL", index), MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>Constructs the CTYPEn indexed keyword.</summary>
        /// <param name="index">The one-based axis, column or parameter index; the full keyword must fit in eight characters.</param>
        /// <returns>The typed indexed keyword.</returns>
        public static FitsKeywordKey<string> CTYPE(int index) => new(Indexed("CTYPE", index), MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>Constructs the NAXISn indexed keyword.</summary>
        /// <param name="index">The one-based axis, column or parameter index; the full keyword must fit in eight characters.</param>
        /// <returns>The typed indexed keyword.</returns>
        public static FitsKeywordKey<long> NAXISn(int index) => new(Indexed("NAXIS", index), MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>Constructs the PSCALn indexed keyword.</summary>
        /// <param name="index">The one-based axis, column or parameter index; the full keyword must fit in eight characters.</param>
        /// <returns>The typed indexed keyword.</returns>
        public static FitsKeywordKey<double> PSCAL(int index) => new(Indexed("PSCAL", index), MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>Constructs the PTYPEn indexed keyword.</summary>
        /// <param name="index">The one-based axis, column or parameter index; the full keyword must fit in eight characters.</param>
        /// <returns>The typed indexed keyword.</returns>
        public static FitsKeywordKey<string> PTYPE(int index) => new(Indexed("PTYPE", index), MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>Constructs the PZEROn indexed keyword.</summary>
        /// <param name="index">The one-based axis, column or parameter index; the full keyword must fit in eight characters.</param>
        /// <returns>The typed indexed keyword.</returns>
        public static FitsKeywordKey<double> PZERO(int index) => new(Indexed("PZERO", index), MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>Constructs the TBCOLn indexed keyword.</summary>
        /// <param name="index">The one-based axis, column or parameter index; the full keyword must fit in eight characters.</param>
        /// <returns>The typed indexed keyword.</returns>
        public static FitsKeywordKey<long> TBCOL(int index) => new(Indexed("TBCOL", index), MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>Constructs the TDIMn indexed keyword.</summary>
        /// <param name="index">The one-based axis, column or parameter index; the full keyword must fit in eight characters.</param>
        /// <returns>The typed indexed keyword.</returns>
        public static FitsKeywordKey<string> TDIM(int index) => new(Indexed("TDIM", index), MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>Constructs the TDISPn indexed keyword.</summary>
        /// <param name="index">The one-based axis, column or parameter index; the full keyword must fit in eight characters.</param>
        /// <returns>The typed indexed keyword.</returns>
        public static FitsKeywordKey<string> TDISP(int index) => new(Indexed("TDISP", index), MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>Constructs the TFORMn indexed keyword.</summary>
        /// <param name="index">The one-based axis, column or parameter index; the full keyword must fit in eight characters.</param>
        /// <returns>The typed indexed keyword.</returns>
        public static FitsKeywordKey<string> TFORM(int index) => new(Indexed("TFORM", index), MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>Constructs the TNULLn indexed keyword.</summary>
        /// <param name="index">The one-based axis, column or parameter index; the full keyword must fit in eight characters.</param>
        /// <returns>The typed indexed keyword.</returns>
        public static FitsKeywordKey<long> TNULL(int index) => new(Indexed("TNULL", index), MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>Constructs the TNULLn indexed keyword.</summary>
        /// <param name="index">The one-based axis, column or parameter index; the full keyword must fit in eight characters.</param>
        /// <returns>The typed indexed keyword.</returns>
        public static FitsKeywordKey<string> TNULLText(int index) => new(Indexed("TNULL", index), MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>Constructs the TSCALn indexed keyword.</summary>
        /// <param name="index">The one-based axis, column or parameter index; the full keyword must fit in eight characters.</param>
        /// <returns>The typed indexed keyword.</returns>
        public static FitsKeywordKey<double> TSCAL(int index) => new(Indexed("TSCAL", index), MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>Constructs the TTYPEn indexed keyword.</summary>
        /// <param name="index">The one-based axis, column or parameter index; the full keyword must fit in eight characters.</param>
        /// <returns>The typed indexed keyword.</returns>
        public static FitsKeywordKey<string> TTYPE(int index) => new(Indexed("TTYPE", index), MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>Constructs the TUNITn indexed keyword.</summary>
        /// <param name="index">The one-based axis, column or parameter index; the full keyword must fit in eight characters.</param>
        /// <returns>The typed indexed keyword.</returns>
        public static FitsKeywordKey<string> TUNIT(int index) => new(Indexed("TUNIT", index), MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
        /// <summary>Constructs the TZEROn indexed keyword.</summary>
        /// <param name="index">The one-based axis, column or parameter index; the full keyword must fit in eight characters.</param>
        /// <returns>The typed indexed keyword.</returns>
        public static FitsKeywordKey<double> TZERO(int index) => new(Indexed("TZERO", index), MetadataDefinitionSource.Specification, "https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html");
    }
}
