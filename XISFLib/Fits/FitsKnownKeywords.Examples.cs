namespace XISFLib;

public static partial class FitsKnownKeywords
{
    /// <summary>Typed FITS definitions in the Examples catalog; consult each key's source and units.</summary>
    public static class Examples
    {
        /// <summary>AD-PED (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> AD_PED { get; } = new("AD-PED", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>ALT-OBS (long); ObservedExamples definition.</summary>
        public static FitsKeywordKey<long> ALT_OBS { get; } = new("ALT-OBS", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>APERTURE (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> APERTURE { get; } = new("APERTURE", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>AWSOLRMS (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> AWSOLRMS { get; } = new("AWSOLRMS", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>AWSOLVED (long); ObservedExamples definition.</summary>
        public static FitsKeywordKey<long> AWSOLVED { get; } = new("AWSOLVED", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>BG-1 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> BG_1 { get; } = new("BG-1", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>BG-2 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> BG_2 { get; } = new("BG-2", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>BG-3 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> BG_3 { get; } = new("BG-3", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>CAMERAID (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> CAMERAID { get; } = new("CAMERAID", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>CD1_1 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> CD1_1 { get; } = new("CD1_1", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>CD1_2 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> CD1_2 { get; } = new("CD1_2", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>CD2_1 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> CD2_1 { get; } = new("CD2_1", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>CD2_2 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> CD2_2 { get; } = new("CD2_2", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>CENTALT (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> CENTALT { get; } = new("CENTALT", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>CENTAZ (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> CENTAZ { get; } = new("CENTAZ", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>CFAIMAGE (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> CFAIMAGE { get; } = new("CFAIMAGE", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>COMPMODE (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> COMPMODE { get; } = new("COMPMODE", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>CRPIX1 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> CRPIX1 { get; } = new("CRPIX1", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>CRPIX2 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> CRPIX2 { get; } = new("CRPIX2", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>CRVAL1 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> CRVAL1 { get; } = new("CRVAL1", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>CRVAL2 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> CRVAL2 { get; } = new("CRVAL2", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>CTYPE1 (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> CTYPE1 { get; } = new("CTYPE1", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>CTYPE2 (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> CTYPE2 { get; } = new("CTYPE2", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>CUNIT1 (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> CUNIT1 { get; } = new("CUNIT1", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>CUNIT2 (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> CUNIT2 { get; } = new("CUNIT2", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>CVF (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> CVF { get; } = new("CVF", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>DATE-AVG (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> DATE_AVG { get; } = new("DATE-AVG", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>IDNR-1 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> IDNR_1 { get; } = new("IDNR-1", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>IDNR-2 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> IDNR_2 { get; } = new("IDNR-2", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>IDNR-3 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> IDNR_3 { get; } = new("IDNR-3", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>INT-METH (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> INT_METH { get; } = new("INT-METH", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>INT-MODE (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> INT_MODE { get; } = new("INT-MODE", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>INTEGRAT (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> INTEGRAT { get; } = new("INTEGRAT", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>INTERPOL (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> INTERPOL { get; } = new("INTERPOL", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>INTSCALE (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> INTSCALE { get; } = new("INTSCALE", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>LAT-OBS (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> LAT_OBS { get; } = new("LAT-OBS", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>LNC-DEG (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> LNC_DEG { get; } = new("LNC-DEG", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>LONG-OBS (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> LONG_OBS { get; } = new("LONG-OBS", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>MBBLEND (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> MBBLEND { get; } = new("MBBLEND", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>MEDENR-1 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> MEDENR_1 { get; } = new("MEDENR-1", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>MEDENR-2 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> MEDENR_2 { get; } = new("MEDENR-2", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>MEDENR-3 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> MEDENR_3 { get; } = new("MEDENR-3", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>MEDNR-1 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> MEDNR_1 { get; } = new("MEDNR-1", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>MEDNR-2 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> MEDNR_2 { get; } = new("MEDNR-2", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>MEDNR-3 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> MEDNR_3 { get; } = new("MEDNR-3", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>MJD-AVG (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> MJD_AVG { get; } = new("MJD-AVG", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>MJD-OBS (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> MJD_OBS { get; } = new("MJD-OBS", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>NAXIS1 (long); ObservedExamples definition.</summary>
        public static FitsKeywordKey<long> NAXIS1 { get; } = new("NAXIS1", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>NAXIS2 (long); ObservedExamples definition.</summary>
        public static FitsKeywordKey<long> NAXIS2 { get; } = new("NAXIS2", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>NAXIS3 (long); ObservedExamples definition.</summary>
        public static FitsKeywordKey<long> NAXIS3 { get; } = new("NAXIS3", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>NCOMBINE (long); ObservedExamples definition.</summary>
        public static FitsKeywordKey<long> NCOMBINE { get; } = new("NCOMBINE", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>NOISE-1 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> NOISE_1 { get; } = new("NOISE-1", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>NOISE-2 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> NOISE_2 { get; } = new("NOISE-2", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>NOISE-3 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> NOISE_3 { get; } = new("NOISE-3", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>NOISE00 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> NOISE00 { get; } = new("NOISE00", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>NOISEA00 (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> NOISEA00 { get; } = new("NOISEA00", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>NOISEH00 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> NOISEH00 { get; } = new("NOISEH00", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>NOISEL00 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> NOISEL00 { get; } = new("NOISEL00", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>NORM-BGN (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> NORM_BGN { get; } = new("NORM-BGN", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>NORMMETH (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> NORMMETH { get; } = new("NORMMETH", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>NORMMODE (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> NORMMODE { get; } = new("NORMMODE", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>NORMSCAL (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> NORMSCAL { get; } = new("NORMSCAL", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>NOTE-1 (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> NOTE_1 { get; } = new("NOTE-1", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>NOTE-10 (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> NOTE_10 { get; } = new("NOTE-10", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>NOTE-11 (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> NOTE_11 { get; } = new("NOTE-11", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>NOTE-12 (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> NOTE_12 { get; } = new("NOTE-12", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>NOTE-2 (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> NOTE_2 { get; } = new("NOTE-2", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>NOTE-3 (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> NOTE_3 { get; } = new("NOTE-3", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>NOTE-4 (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> NOTE_4 { get; } = new("NOTE-4", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>NOTE-5 (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> NOTE_5 { get; } = new("NOTE-5", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>NOTE-6 (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> NOTE_6 { get; } = new("NOTE-6", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>NOTE-7 (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> NOTE_7 { get; } = new("NOTE-7", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>NOTE-8 (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> NOTE_8 { get; } = new("NOTE-8", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>NOTE-9 (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> NOTE_9 { get; } = new("NOTE-9", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>NUMFRAME (long); ObservedExamples definition.</summary>
        public static FitsKeywordKey<long> NUMFRAME { get; } = new("NUMFRAME", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>OBJCTROT (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> OBJCTROT { get; } = new("OBJCTROT", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>OBSGEO-B (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> OBSGEO_B { get; } = new("OBSGEO-B", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>OBSGEO-H (long); ObservedExamples definition.</summary>
        public static FitsKeywordKey<long> OBSGEO_H { get; } = new("OBSGEO-H", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>OBSGEO-L (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> OBSGEO_L { get; } = new("OBSGEO-L", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>OPT-DC (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> OPT_DC { get; } = new("OPT-DC", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>OUTL-DP (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> OUTL_DP { get; } = new("OUTL-DP", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>OUTL-KH (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> OUTL_KH { get; } = new("OUTL-KH", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>OUTL-KL (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> OUTL_KL { get; } = new("OUTL-KL", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>OUTL-LN (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> OUTL_LN { get; } = new("OUTL-LN", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>OUTL-REJ (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> OUTL_REJ { get; } = new("OUTL-REJ", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>PIERSIDE (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> PIERSIDE { get; } = new("PIERSIDE", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>PLTSOLVD (bool); ObservedExamples definition.</summary>
        public static FitsKeywordKey<bool> PLTSOLVD { get; } = new("PLTSOLVD", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>PROJECT (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> PROJECT { get; } = new("PROJECT", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>PSFFLP00 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> PSFFLP00 { get; } = new("PSFFLP00", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>PSFFLX00 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> PSFFLX00 { get; } = new("PSFFLX00", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>PSFMFL00 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> PSFMFL00 { get; } = new("PSFMFL00", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>PSFMFP00 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> PSFMFP00 { get; } = new("PSFMFP00", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>PSFMST00 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> PSFMST00 { get; } = new("PSFMST00", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>PSFNST00 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> PSFNST00 { get; } = new("PSFNST00", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>PSFSGN00 (long); ObservedExamples definition.</summary>
        public static FitsKeywordKey<long> PSFSGN00 { get; } = new("PSFSGN00", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>PSFSGTYP (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> PSFSGTYP { get; } = new("PSFSGTYP", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>RADESYS (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> RADESYS { get; } = new("RADESYS", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>RATNR-1 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> RATNR_1 { get; } = new("RATNR-1", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>RATNR-2 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> RATNR_2 { get; } = new("RATNR-2", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>RATNR-3 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> RATNR_3 { get; } = new("RATNR-3", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>REF-1 (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> REF_1 { get; } = new("REF-1", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>REF-2 (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> REF_2 { get; } = new("REF-2", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>REFENR-1 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> REFENR_1 { get; } = new("REFENR-1", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>REFENR-2 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> REFENR_2 { get; } = new("REFENR-2", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>REFENR-3 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> REFENR_3 { get; } = new("REFENR-3", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>REFNR-1 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> REFNR_1 { get; } = new("REFNR-1", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>REFNR-2 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> REFNR_2 { get; } = new("REFNR-2", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>REFNR-3 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> REFNR_3 { get; } = new("REFNR-3", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>REGMODE (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> REGMODE { get; } = new("REGMODE", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>REGMODEL (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> REGMODEL { get; } = new("REGMODEL", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>SCALE-1 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> SCALE_1 { get; } = new("SCALE-1", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>SCALE-2 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> SCALE_2 { get; } = new("SCALE-2", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>SCALE-3 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> SCALE_3 { get; } = new("SCALE-3", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>SNR-1 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> SNR_1 { get; } = new("SNR-1", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>SNR-2 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> SNR_2 { get; } = new("SNR-2", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>SNR-3 (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> SNR_3 { get; } = new("SNR-3", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>SOFTWARE (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> SOFTWARE { get; } = new("SOFTWARE", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>SSWEIGHT (double); ObservedExamples definition.</summary>
        public static FitsKeywordKey<double> SSWEIGHT { get; } = new("SSWEIGHT", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>TIMESYS (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> TIMESYS { get; } = new("TIMESYS", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>USBLIMIT (long); ObservedExamples definition.</summary>
        public static FitsKeywordKey<long> USBLIMIT { get; } = new("USBLIMIT", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>VERSION (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> VERSION { get; } = new("VERSION", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>WBPPCROP (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> WBPPCROP { get; } = new("WBPPCROP", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>WEIGHTS (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> WEIGHTS { get; } = new("WEIGHTS", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>WZFPRNT (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> WZFPRNT { get; } = new("WZFPRNT", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>WZNOTE (string); ObservedExamples definition.</summary>
        public static FitsKeywordKey<string> WZNOTE { get; } = new("WZNOTE", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        /// <summary>WZNSUBS (long); ObservedExamples definition.</summary>
        public static FitsKeywordKey<long> WZNSUBS { get; } = new("WZNSUBS", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
    }
}
