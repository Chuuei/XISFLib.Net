namespace XISFLib;

public static partial class FitsKnownKeywords
{
    /// <summary>Typed FITS definitions in the Common catalog; consult each key's source and units.</summary>
    public static class Common
    {
        /// <summary>AIRMASS (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> AIRMASS { get; } = new("AIRMASS", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>APERTURE (string); PublishedConvention definition.</summary>
        public static FitsKeywordKey<string> APERTURE { get; } = new("APERTURE", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>CHECKSUM (string); PublishedConvention definition.</summary>
        public static FitsKeywordKey<string> CHECKSUM { get; } = new("CHECKSUM", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>CHECKVER (string); PublishedConvention definition.</summary>
        public static FitsKeywordKey<string> CHECKVER { get; } = new("CHECKVER", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>CONFIGUR (string); PublishedConvention definition.</summary>
        public static FitsKeywordKey<string> CONFIGUR { get; } = new("CONFIGUR", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>CREATOR (string); PublishedConvention definition.</summary>
        public static FitsKeywordKey<string> CREATOR { get; } = new("CREATOR", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>DATAMODE (string); PublishedConvention definition.</summary>
        public static FitsKeywordKey<string> DATAMODE { get; } = new("DATAMODE", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>DATASUM (string); PublishedConvention definition.</summary>
        public static FitsKeywordKey<string> DATASUM { get; } = new("DATASUM", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>DATE-END (string); PublishedConvention definition.</summary>
        public static FitsKeywordKey<string> DATE_END { get; } = new("DATE-END", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>DEC (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> DEC { get; } = new("DEC", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html", unit: "deg");
        /// <summary>DEC_NOM (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> DEC_NOM { get; } = new("DEC_NOM", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html", unit: "deg");
        /// <summary>DEC_OBJ (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> DEC_OBJ { get; } = new("DEC_OBJ", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html", unit: "deg");
        /// <summary>DEC_PNT (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> DEC_PNT { get; } = new("DEC_PNT", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html", unit: "deg");
        /// <summary>DEC_SCX (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> DEC_SCX { get; } = new("DEC_SCX", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html", unit: "deg");
        /// <summary>DEC_SCY (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> DEC_SCY { get; } = new("DEC_SCY", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html", unit: "deg");
        /// <summary>DEC_SCZ (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> DEC_SCZ { get; } = new("DEC_SCZ", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html", unit: "deg");
        /// <summary>DEC (string); PublishedConvention definition.</summary>
        public static FitsKeywordKey<string> DECText { get; } = new("DEC", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>DETNAM (string); PublishedConvention definition.</summary>
        public static FitsKeywordKey<string> DETNAM { get; } = new("DETNAM", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>ELAPTIME (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> ELAPTIME { get; } = new("ELAPTIME", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html", unit: "s");
        /// <summary>EXPOSURE (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> EXPOSURE { get; } = new("EXPOSURE", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html", unit: "s");
        /// <summary>EXPTIME (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> EXPTIME { get; } = new("EXPTIME", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html", unit: "s");
        /// <summary>FILENAME (string); PublishedConvention definition.</summary>
        public static FitsKeywordKey<string> FILENAME { get; } = new("FILENAME", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>FILETYPE (string); PublishedConvention definition.</summary>
        public static FitsKeywordKey<string> FILETYPE { get; } = new("FILETYPE", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>FILTER (string); PublishedConvention definition.</summary>
        public static FitsKeywordKey<string> FILTER { get; } = new("FILTER", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>GRATING (string); PublishedConvention definition.</summary>
        public static FitsKeywordKey<string> GRATING { get; } = new("GRATING", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>HDUCLASS (string); PublishedConvention definition.</summary>
        public static FitsKeywordKey<string> HDUCLASS { get; } = new("HDUCLASS", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>HDUDOC (string); PublishedConvention definition.</summary>
        public static FitsKeywordKey<string> HDUDOC { get; } = new("HDUDOC", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>HDULEVEL (long); PublishedConvention definition.</summary>
        public static FitsKeywordKey<long> HDULEVEL { get; } = new("HDULEVEL", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>HDUNAME (string); PublishedConvention definition.</summary>
        public static FitsKeywordKey<string> HDUNAME { get; } = new("HDUNAME", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>HDUVER (long); PublishedConvention definition.</summary>
        public static FitsKeywordKey<long> HDUVER { get; } = new("HDUVER", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>HDUVERS (string); PublishedConvention definition.</summary>
        public static FitsKeywordKey<string> HDUVERS { get; } = new("HDUVERS", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>INHERIT (bool); PublishedConvention definition.</summary>
        public static FitsKeywordKey<bool> INHERIT { get; } = new("INHERIT", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>LATITUDE (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> LATITUDE { get; } = new("LATITUDE", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html", unit: "deg");
        /// <summary>LIVETIME (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> LIVETIME { get; } = new("LIVETIME", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html", unit: "s");
        /// <summary>MOONANGL (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> MOONANGL { get; } = new("MOONANGL", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html", unit: "deg");
        /// <summary>NEXTEND (long); PublishedConvention definition.</summary>
        public static FitsKeywordKey<long> NEXTEND { get; } = new("NEXTEND", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>OBJNAME (string); PublishedConvention definition.</summary>
        public static FitsKeywordKey<string> OBJNAME { get; } = new("OBJNAME", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>OBS_ID (string); PublishedConvention definition.</summary>
        public static FitsKeywordKey<string> OBS_ID { get; } = new("OBS_ID", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>OBS_MODE (string); PublishedConvention definition.</summary>
        public static FitsKeywordKey<string> OBS_MODE { get; } = new("OBS_MODE", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>ONTIME (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> ONTIME { get; } = new("ONTIME", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html", unit: "s");
        /// <summary>ORIENTAT (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> ORIENTAT { get; } = new("ORIENTAT", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>PA_PNT (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> PA_PNT { get; } = new("PA_PNT", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html", unit: "deg");
        /// <summary>PROGRAM (string); PublishedConvention definition.</summary>
        public static FitsKeywordKey<string> PROGRAM { get; } = new("PROGRAM", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>RA (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> RA { get; } = new("RA", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html", unit: "deg");
        /// <summary>RA_NOM (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> RA_NOM { get; } = new("RA_NOM", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html", unit: "deg");
        /// <summary>RA_OBJ (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> RA_OBJ { get; } = new("RA_OBJ", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html", unit: "deg");
        /// <summary>RA_PNT (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> RA_PNT { get; } = new("RA_PNT", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html", unit: "deg");
        /// <summary>RA_SCX (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> RA_SCX { get; } = new("RA_SCX", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html", unit: "deg");
        /// <summary>RA_SCY (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> RA_SCY { get; } = new("RA_SCY", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html", unit: "deg");
        /// <summary>RA_SCZ (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> RA_SCZ { get; } = new("RA_SCZ", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html", unit: "deg");
        /// <summary>RA (string); PublishedConvention definition.</summary>
        public static FitsKeywordKey<string> RAText { get; } = new("RA", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>ROOTNAME (string); PublishedConvention definition.</summary>
        public static FitsKeywordKey<string> ROOTNAME { get; } = new("ROOTNAME", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>SATURATE (long); PublishedConvention definition.</summary>
        public static FitsKeywordKey<long> SATURATE { get; } = new("SATURATE", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>SUNANGLE (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> SUNANGLE { get; } = new("SUNANGLE", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html", unit: "deg");
        /// <summary>TELAPSE (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> TELAPSE { get; } = new("TELAPSE", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html", unit: "s");
        /// <summary>TIME-END (string); PublishedConvention definition.</summary>
        public static FitsKeywordKey<string> TIME_END { get; } = new("TIME-END", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>TIME-OBS (string); PublishedConvention definition.</summary>
        public static FitsKeywordKey<string> TIME_OBS { get; } = new("TIME-OBS", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>TITLE (string); PublishedConvention definition.</summary>
        public static FitsKeywordKey<string> TITLE { get; } = new("TITLE", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>TSORTKEY (string); PublishedConvention definition.</summary>
        public static FitsKeywordKey<string> TSORTKEY { get; } = new("TSORTKEY", MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>Constructs the FILTERn indexed keyword.</summary>
        /// <param name="index">The one-based axis, column or parameter index; the full keyword must fit in eight characters.</param>
        /// <returns>The typed indexed keyword.</returns>
        public static FitsKeywordKey<string> FILTERn(int index) => new(Indexed("FILTER", index), MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>Constructs the GRATINGn indexed keyword.</summary>
        /// <param name="index">The one-based axis, column or parameter index; the full keyword must fit in eight characters.</param>
        /// <returns>The typed indexed keyword.</returns>
        public static FitsKeywordKey<string> GRATINGn(int index) => new(Indexed("GRATING", index), MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>Constructs the HDUCLASn indexed keyword.</summary>
        /// <param name="index">The one-based axis, column or parameter index; the full keyword must fit in eight characters.</param>
        /// <returns>The typed indexed keyword.</returns>
        public static FitsKeywordKey<string> HDUCLAS(int index) => new(Indexed("HDUCLAS", index), MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>Constructs the TDBINn indexed keyword.</summary>
        /// <param name="index">The one-based axis, column or parameter index; the full keyword must fit in eight characters.</param>
        /// <returns>The typed indexed keyword.</returns>
        public static FitsKeywordKey<double> TDBIN(int index) => new(Indexed("TDBIN", index), MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>Constructs the TDMAXn indexed keyword.</summary>
        /// <param name="index">The one-based axis, column or parameter index; the full keyword must fit in eight characters.</param>
        /// <returns>The typed indexed keyword.</returns>
        public static FitsKeywordKey<double> TDMAX(int index) => new(Indexed("TDMAX", index), MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>Constructs the TDMINn indexed keyword.</summary>
        /// <param name="index">The one-based axis, column or parameter index; the full keyword must fit in eight characters.</param>
        /// <returns>The typed indexed keyword.</returns>
        public static FitsKeywordKey<double> TDMIN(int index) => new(Indexed("TDMIN", index), MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>Constructs the TLMAXn indexed keyword.</summary>
        /// <param name="index">The one-based axis, column or parameter index; the full keyword must fit in eight characters.</param>
        /// <returns>The typed indexed keyword.</returns>
        public static FitsKeywordKey<double> TLMAX(int index) => new(Indexed("TLMAX", index), MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
        /// <summary>Constructs the TLMINn indexed keyword.</summary>
        /// <param name="index">The one-based axis, column or parameter index; the full keyword must fit in eight characters.</param>
        /// <returns>The typed indexed keyword.</returns>
        public static FitsKeywordKey<double> TLMIN(int index) => new(Indexed("TLMIN", index), MetadataDefinitionSource.PublishedConvention, "https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html");
    }
}
