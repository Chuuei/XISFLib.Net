namespace XISFLib;

public static partial class FitsKnownKeywords
{
    /// <summary>Typed FITS definitions in the Astrophotography catalog; consult each key's source and units.</summary>
    public static class Astrophotography
    {
        /// <summary>AMBTEMP (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> AMBTEMP { get; } = new("AMBTEMP", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/", unit: "degrees Celsius");
        /// <summary>BAYERPAT (string); PublishedConvention definition.</summary>
        public static FitsKeywordKey<string> BAYERPAT { get; } = new("BAYERPAT", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/");
        /// <summary>CCD-TEMP (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> CCD_TEMP { get; } = new("CCD-TEMP", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/", unit: "degrees Celsius");
        /// <summary>CLOUDCVR (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> CLOUDCVR { get; } = new("CLOUDCVR", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/", unit: "percent");
        /// <summary>DATE-LOC (string); PublishedConvention definition.</summary>
        public static FitsKeywordKey<string> DATE_LOC { get; } = new("DATE-LOC", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/");
        /// <summary>DATE-UTC (string); PublishedConvention definition.</summary>
        public static FitsKeywordKey<string> DATE_UTC { get; } = new("DATE-UTC", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/");
        /// <summary>DEWPOINT (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> DEWPOINT { get; } = new("DEWPOINT", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/", unit: "degrees Celsius");
        /// <summary>EGAIN (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> EGAIN { get; } = new("EGAIN", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/", unit: "electrons/ADU");
        /// <summary>FOCALLEN (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> FOCALLEN { get; } = new("FOCALLEN", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/");
        /// <summary>FOCNAME (string); PublishedConvention definition.</summary>
        public static FitsKeywordKey<string> FOCNAME { get; } = new("FOCNAME", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/");
        /// <summary>FOCPOS (long); PublishedConvention definition.</summary>
        public static FitsKeywordKey<long> FOCPOS { get; } = new("FOCPOS", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/");
        /// <summary>FOCRATIO (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> FOCRATIO { get; } = new("FOCRATIO", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/");
        /// <summary>FOCTEMP (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> FOCTEMP { get; } = new("FOCTEMP", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/", unit: "degrees Celsius");
        /// <summary>FOCUSPOS (long); PublishedConvention definition.</summary>
        public static FitsKeywordKey<long> FOCUSPOS { get; } = new("FOCUSPOS", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/");
        /// <summary>FOCUSSZ (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> FOCUSSZ { get; } = new("FOCUSSZ", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/");
        /// <summary>FOCUSTEM (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> FOCUSTEM { get; } = new("FOCUSTEM", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/", unit: "degrees Celsius");
        /// <summary>FWHEEL (string); PublishedConvention definition.</summary>
        public static FitsKeywordKey<string> FWHEEL { get; } = new("FWHEEL", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/");
        /// <summary>GAIN (long); PublishedConvention definition.</summary>
        public static FitsKeywordKey<long> GAIN { get; } = new("GAIN", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/");
        /// <summary>HUMIDITY (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> HUMIDITY { get; } = new("HUMIDITY", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/", unit: "percent");
        /// <summary>IMAGETYP (string); PublishedConvention definition.</summary>
        public static FitsKeywordKey<string> IMAGETYP { get; } = new("IMAGETYP", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/");
        /// <summary>MPSAS (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> MPSAS { get; } = new("MPSAS", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/", unit: "magnitudes/arcsecond squared");
        /// <summary>OBJCTDEC (string); PublishedConvention definition.</summary>
        public static FitsKeywordKey<string> OBJCTDEC { get; } = new("OBJCTDEC", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/");
        /// <summary>OBJCTRA (string); PublishedConvention definition.</summary>
        public static FitsKeywordKey<string> OBJCTRA { get; } = new("OBJCTRA", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/");
        /// <summary>OFFSET (long); PublishedConvention definition.</summary>
        public static FitsKeywordKey<long> OFFSET { get; } = new("OFFSET", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/");
        /// <summary>PRESSURE (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> PRESSURE { get; } = new("PRESSURE", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/", unit: "hectopascals");
        /// <summary>READOUTM (string); PublishedConvention definition.</summary>
        public static FitsKeywordKey<string> READOUTM { get; } = new("READOUTM", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/");
        /// <summary>ROTATANG (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> ROTATANG { get; } = new("ROTATANG", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/", unit: "degrees");
        /// <summary>ROTATOR (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> ROTATOR { get; } = new("ROTATOR", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/", unit: "degrees");
        /// <summary>ROTNAME (string); PublishedConvention definition.</summary>
        public static FitsKeywordKey<string> ROTNAME { get; } = new("ROTNAME", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/");
        /// <summary>ROTSTPSZ (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> ROTSTPSZ { get; } = new("ROTSTPSZ", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/");
        /// <summary>ROWORDER (string); PublishedConvention definition.</summary>
        public static FitsKeywordKey<string> ROWORDER { get; } = new("ROWORDER", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/");
        /// <summary>SET-TEMP (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> SET_TEMP { get; } = new("SET-TEMP", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/", unit: "degrees Celsius");
        /// <summary>SITEELEV (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> SITEELEV { get; } = new("SITEELEV", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/", unit: "meters");
        /// <summary>SITELAT (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> SITELAT { get; } = new("SITELAT", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/", unit: "degrees");
        /// <summary>SITELONG (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> SITELONG { get; } = new("SITELONG", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/", unit: "degrees");
        /// <summary>SKYBRGHT (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> SKYBRGHT { get; } = new("SKYBRGHT", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/", unit: "lux");
        /// <summary>SKYTEMP (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> SKYTEMP { get; } = new("SKYTEMP", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/", unit: "degrees Celsius");
        /// <summary>STARFWHM (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> STARFWHM { get; } = new("STARFWHM", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/");
        /// <summary>SWCREATE (string); PublishedConvention definition.</summary>
        public static FitsKeywordKey<string> SWCREATE { get; } = new("SWCREATE", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/");
        /// <summary>WINDDIR (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> WINDDIR { get; } = new("WINDDIR", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/", unit: "degrees (north azimuth)");
        /// <summary>WINDGUST (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> WINDGUST { get; } = new("WINDGUST", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/", unit: "kilometers/hour");
        /// <summary>WINDSPD (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> WINDSPD { get; } = new("WINDSPD", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/", unit: "kilometers/hour");
        /// <summary>XBAYEROFF (long); PublishedConvention definition.</summary>
        public static FitsKeywordKey<long> XBAYEROFF { get; } = new("XBAYEROFF", MetadataDefinitionSource.PublishedConvention, "https://github.com/isbeorn/nina/blob/develop/RELEASE_NOTES.md");
        /// <summary>XBAYROFF (long); PublishedConvention definition.</summary>
        public static FitsKeywordKey<long> XBAYROFF { get; } = new("XBAYROFF", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/");
        /// <summary>XBINNING (long); PublishedConvention definition.</summary>
        public static FitsKeywordKey<long> XBINNING { get; } = new("XBINNING", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/");
        /// <summary>XPIXSZ (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> XPIXSZ { get; } = new("XPIXSZ", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/", unit: "microns");
        /// <summary>YBAYEROFF (long); PublishedConvention definition.</summary>
        public static FitsKeywordKey<long> YBAYEROFF { get; } = new("YBAYEROFF", MetadataDefinitionSource.PublishedConvention, "https://github.com/isbeorn/nina/blob/develop/RELEASE_NOTES.md");
        /// <summary>YBAYROFF (long); PublishedConvention definition.</summary>
        public static FitsKeywordKey<long> YBAYROFF { get; } = new("YBAYROFF", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/");
        /// <summary>YBINNING (long); PublishedConvention definition.</summary>
        public static FitsKeywordKey<long> YBINNING { get; } = new("YBINNING", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/");
        /// <summary>YPIXSZ (double); PublishedConvention definition.</summary>
        public static FitsKeywordKey<double> YPIXSZ { get; } = new("YPIXSZ", MetadataDefinitionSource.PublishedConvention, "https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/", unit: "microns");
    }
}
