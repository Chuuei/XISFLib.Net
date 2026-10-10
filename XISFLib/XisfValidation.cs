using System.Buffers.Binary;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace XISFLib;

/// <summary>The importance of an XISF conformance finding.</summary>
public enum XisfDiagnosticSeverity
{
    /// <summary>A recoverable conformance finding.</summary>
    Warning,
    /// <summary>A conformance violation.</summary>
    Error
}

/// <summary>An XISF conformance finding and its associated XML element.</summary>
/// <param name="Severity">The importance of the diagnostic.</param>
/// <param name="Code">The stable diagnostic code.</param>
/// <param name="Message">The human-readable explanation.</param>
/// <param name="Element">The related XML element, when available.</param>
public sealed record XisfDiagnostic(XisfDiagnosticSeverity Severity, string Code, string Message, XElement? Element = null);

internal static class XisfValidator
{
    private const string Namespace = "http://www.pixinsight.com/xisf";
    private static readonly Regex Identifier = new("^[_a-zA-Z][_a-zA-Z0-9]*$", RegexOptions.CultureInvariant);
    private static readonly Regex PropertyIdentifier = new("^[_a-zA-Z][_a-zA-Z0-9]*(?::[_a-zA-Z][_a-zA-Z0-9]*)*$", RegexOptions.CultureInvariant);
    private static readonly HashSet<string> Orientations = ["0", "flip", "90", "90;flip", "-90", "-90;flip", "180", "180;flip"];
    private static readonly HashSet<string> ColorSpaces = ["Gray", "RGB", "CIELab"];
    private static readonly HashSet<string> ImageTypes =
    [
        "Bias", "Dark", "Flat", "Light", "MasterBias", "MasterDark", "MasterFlat", "MasterLight", "DefectMap",
        "RejectionMapHigh", "RejectionMapLow", "BinaryRejectionMapHigh", "BinaryRejectionMapLow", "SlopeMap", "WeightMap"
    ];
    private static readonly Regex FitsKeywordName = new("^[A-Z0-9_-]{1,8}$", RegexOptions.CultureInvariant);

    public static IReadOnlyList<XisfDiagnostic> Validate(XisfDocument document)
    {
        List<XisfDiagnostic> diagnostics = [];
        XElement root = document.Xml.Root!;
        if (root.Name.NamespaceName != Namespace) diagnostics.Add(new(XisfDiagnosticSeverity.Warning, "XISF001", "The recommended XISF namespace is absent or different.", root));
        if ((string?)root.Attribute("version") != "1.0") Error("XISF009", "The root version attribute must be '1.0'.", root);
        if (document.Xml.Declaration is null || !string.Equals(document.Xml.Declaration.Version, "1.0", StringComparison.Ordinal) ||
            !string.Equals(document.Xml.Declaration.Encoding, "UTF-8", StringComparison.OrdinalIgnoreCase))
            Error("XISF002", "The header must have an XML 1.0 UTF-8 declaration.", root);
        XElement[] metadata = root.Elements(XName.Get("Metadata", root.Name.NamespaceName)).ToArray();
        if (metadata.Length != 1) Error("XISF003", "The header must contain exactly one Metadata element.", root);
        else
        {
            RequireMetadata("XISF:CreationTime", "TimePoint");
            RequireMetadata("XISF:CreatorApplication", "String");
            foreach (XElement property in metadata[0].Elements(XName.Get("Property", root.Name.NamespaceName)))
                if (!((string?)property.Attribute("id") ?? "").StartsWith("XISF:", StringComparison.Ordinal))
                    Error("XISF006", "Metadata property identifiers must use the XISF namespace.", property);
        }
        foreach (XElement element in root.DescendantsAndSelf())
        {
            if (element.Name.Namespace != root.Name.Namespace) continue;
            if (element.Attribute("location") is { } location)
            {
                bool monolithic = document.Source.FilePath is not null || document.Source.File.AsSpan().StartsWith("XISF0100"u8);
                bool attachment = location.Value.StartsWith("attachment:", StringComparison.Ordinal);
                bool external = location.Value.StartsWith("path(", StringComparison.Ordinal) || location.Value.StartsWith("url(", StringComparison.Ordinal);
                if (monolithic && external || !monolithic && attachment)
                    Error("XISF015", "The data block location is incompatible with the unit's container type.", element);
            }
            if (element.Name.Namespace == root.Name.Namespace && element.Name.LocalName != "Reference" && element.Attribute("uid") is { } uid && !Identifier.IsMatch(uid.Value))
                Error("XISF007", $"Invalid core element uid '{uid.Value}'.", element);
            if (element.Name == XName.Get("Property", root.Name.NamespaceName) && !PropertyIdentifier.IsMatch((string?)element.Attribute("id") ?? ""))
                Error("XISF008", "Invalid property identifier.", element);
            if (element.Attribute("byteOrder") is { } byteOrder && byteOrder.Value is not ("little" or "big"))
                Error("XISF010", "The byteOrder attribute must be 'little' or 'big'.", element);
            if (element.Name == XName.Get("ICCProfile", root.Name.NamespaceName) && element.Attribute("byteOrder") is not null)
                Error("XISF011", "ICCProfile elements cannot define a byteOrder attribute.", element);
        }
        if (root.Nodes().OfType<XText>().Any(t => !string.IsNullOrWhiteSpace(t.Value))) Error("XISF012", "The root cannot contain character data.", root);
        foreach (var duplicate in root.Descendants(XName.Get("Image", root.Name.NamespaceName)).Where(e => e.Attribute("id") is not null).GroupBy(e => (string)e.Attribute("id")!).Where(g => g.Count() > 1))
            Error("XISF013", "Image identifiers must be unique within a unit.", duplicate.First());
        foreach (XisfTable table in document.Tables.Concat(document.Images.SelectMany(i => i.Tables)))
        {
            if (table.Fields.Count == 0) Error("XISF020", $"Table '{table.Id}' has no fields.", table.Xml);
            if (table.Fields.Select(f => f.Id).Distinct(StringComparer.Ordinal).Count() != table.Fields.Count) Error("XISF021", $"Table '{table.Id}' has duplicate fields.", table.Xml);
            if (table.Xml.Attribute("rows") is { } rows && (!int.TryParse(rows.Value, out int rowCount) || rowCount != table.Rows.Count)) Error("XISF023", "Table row count does not match its contents.", table.Xml);
            if (table.Xml.Attribute("columns") is { } columns && (!int.TryParse(columns.Value, out int columnCount) || columnCount != table.Fields.Count)) Error("XISF024", "Table column count does not match its structure.", table.Xml);
            if (table.Rows.Any(row => row.Cells.Count != table.Fields.Count)) Error("XISF022", $"Table '{table.Id}' has a row with an invalid cell count.", table.Xml);
        }
        foreach (XisfImage image in document.Images) ValidateImage(image, false);
        foreach (XElement property in root.Descendants(XName.Get("Property", root.Name.NamespaceName)))
        {
            string type = (string?)property.Attribute("type") ?? "";
            bool vector = type.EndsWith("Vector", StringComparison.Ordinal) || type == "ByteArray";
            bool matrix = type.EndsWith("Matrix", StringComparison.Ordinal);
            bool empty = vector && (string?)property.Attribute("length") == "0" || matrix && ((string?)property.Attribute("rows") == "0" || (string?)property.Attribute("columns") == "0");
            if (empty && (((string?)property.Attribute("location"))?.StartsWith("inline:", StringComparison.Ordinal) != true || property.Elements().Any() || !string.IsNullOrWhiteSpace(property.Value)))
                Error("XISF014", "Empty collections require an empty inline block.", property);
        }
        return diagnostics;

        void RequireMetadata(string id, string type)
        {
            XElement? property = metadata[0].Elements(XName.Get("Property", root.Name.NamespaceName)).SingleOrDefault(e => (string?)e.Attribute("id") == id);
            if (property is null || (string?)property.Attribute("type") != type)
                Error("XISF004", $"Metadata requires one {id} property of type {type}.", metadata[0]);
        }
        void ValidateImage(XisfImage image, bool thumbnail)
        {
            if (image.Id is { } id && !Identifier.IsMatch(id)) Error("XISF030", $"Invalid image identifier '{id}'.", image.Xml);
            if (!double.IsFinite(image.Offset) || image.Offset < 0) Error("XISF031", "Image offset cannot be negative.", image.Xml);
            if (!Orientations.Contains(image.Orientation)) Error("XISF032", $"Invalid image orientation '{image.Orientation}'.", image.Xml);
            if (image.Xml.Attribute("uuid") is { } uuid && (image.Uuid is null || uuid.Value.Length != 36 || uuid.Value[14] != '4' || !"89abAB".Contains(uuid.Value[19])))
                Error("XISF033", "Image uuid must be a canonical version 4 UUID with the RFC variant.", image.Xml);
            if (!ColorSpaces.Contains(image.ColorSpace ?? "Gray")) Error("XISF034", $"Unsupported image color space '{image.ColorSpace}'.", image.Xml);
            if (image.ColorSpace == "Gray" && image.ChannelCount < 1 || image.ColorSpace is "RGB" or "CIELab" && image.ChannelCount < 3)
                Error("XISF035", "Image channel count is inconsistent with its color space.", image.Xml);
            if (image.ImageType is { } imageType && !ImageTypes.Contains(imageType)) Error("XISF039", $"Invalid image type '{imageType}'.", image.Xml);
            if (!double.IsFinite(image.LowerBound) || !double.IsFinite(image.UpperBound) || image.LowerBound >= image.UpperBound)
                Error("XISF040", "Image bounds must be finite and strictly increasing.", image.Xml);
            if (((string?)image.Xml.Attribute("location"))?.StartsWith("inline:", StringComparison.OrdinalIgnoreCase) == true)
                Error("XISF041", "Image pixel data cannot use an inline data block.", image.Xml);
            if (thumbnail && (image.Dimensions.Count != 2 || image.SampleFormat is not (XisfSampleFormat.UInt8 or XisfSampleFormat.UInt16) || image.ColorSpace is not ("Gray" or "RGB") || image.ChannelCount > (image.ColorSpace == "RGB" ? 4 : 2) || image.Xml.Attribute("bounds") is not null || image.ColorFilterArray is not null || image.Thumbnail is not null))
                Error("XISF036", "Thumbnail restrictions are not satisfied.", image.Xml);
            if (image.DisplayFunction is { } display && new[] { display.Midtones.Count, display.Shadows.Count, display.Highlights.Count, display.LowRange.Count, display.HighRange.Count }.Any(n => n != 4))
                Error("XISF037", "Display function vectors must have four components.", image.Xml);
            if (image.Resolution is { } resolution && (!double.IsFinite(resolution.Horizontal) || !double.IsFinite(resolution.Vertical) || resolution.Horizontal <= 0 || resolution.Vertical <= 0 || resolution.Unit is not ("inch" or "cm")))
                Error("XISF038", "Invalid image resolution.", image.Xml);
            foreach (XisfFitsKeyword keyword in image.FitsKeywords)
            {
                if (!FitsKeywordName.IsMatch(keyword.Name)) Error("XISF042", $"Invalid FITS keyword name '{keyword.Name}'.", image.Xml);
                if (keyword.Name is "HISTORY" or "COMMENT" && keyword.Value.Length != 0) Error("XISF045", "HISTORY and COMMENT must have empty values.", image.Xml);
            }
            foreach (XElement keyword in image.Xml.Elements(XName.Get("FITSKeyword", root.Name.NamespaceName)))
                if (keyword.Attribute("comment") is null) Error("XISF046", "FITSKeyword requires its comment attribute.", keyword);
            if (image.RgbWorkingSpace is { } rgb)
            {
                try
                {
                    double[] derived = XisfColorSpace.DeriveLuminanceCoefficients(rgb);
                    if (rgb.LuminanceCoefficients.Count != 3 || rgb.LuminanceCoefficients.Where((v, i) => !double.IsFinite(v) || Math.Abs(v - derived[i]) > 2e-5).Any())
                        Error("XISF043", "RGB luminance coefficients are inconsistent with the primaries and D50 white.", image.Xml);
                }
                catch (ArgumentException exception) { Error("XISF043", exception.Message, image.Xml); }
            }
            if (image.AstrometricSolution is { } astrometry)
            {
                foreach (string diagnostic in astrometry.Diagnostics) diagnostics.Add(new(XisfDiagnosticSeverity.Warning, "XISF050", diagnostic, image.Xml));
                if (astrometry.AvailableLayer > 0) ValidateProvenance(astrometry, image.Xml);
            }
            foreach (XisfUnavailableObject unavailable in image.UnavailableObjects)
                diagnostics.Add(new(XisfDiagnosticSeverity.Warning, "XISF051", unavailable.Reason, unavailable.Xml));
            if (image.IccProfile is { } profile && !IsEmbeddedIccProfile(profile))
                Error("XISF044", "The ICC profile is malformed or does not set the embedded-profile flag.", image.Xml);
            if (image.Thumbnail is { } child) ValidateImage(child, true);
        }
        void ValidateProvenance(XisfAstrometricSolution astrometry, XElement element)
        {
            const string prefix = "AstrometricSolution:";
            object? Value(string id) => astrometry.Properties.GetValueOrDefault(prefix + id)?.Value;
            bool Present(string id) => astrometry.Properties.ContainsKey(prefix + id);
            int? count = null;
            foreach (string id in new[] { "ControlPoints:Celestial", "ControlPoints:Image" })
            {
                if (!Present(id)) continue;
                if (Value(id) is not double[,] matrix || matrix.GetLength(1) != 2 || matrix.Cast<double>().Any(v => !double.IsFinite(v)))
                { Error("XISF052", $"Invalid astrometric provenance matrix {id}.", element); continue; }
                if (count is { } n && n != matrix.GetLength(0)) Error("XISF052", "Astrometric control point counts disagree.", element);
                count = matrix.GetLength(0);
            }
            if (Present("Weights") && (Value("Weights") is not double[] weights || count is null || weights.Length != count || weights.Any(v => !double.IsFinite(v) || v <= 0)))
                Error("XISF052", "Astrometric weights must be positive and match control point count.", element);
            if (Present("ControlPoints:Rejected") && (Value("ControlPoints:Rejected") is not int[] rejected || count is null || rejected.Any(i => i < 0 || i >= count)))
                Error("XISF052", "Rejected control point indices are out of range.", element);
        }
        static bool IsEmbeddedIccProfile(byte[] profile)
        {
            if (profile.Length < 128 || BinaryPrimitives.ReadUInt32BigEndian(profile) != profile.Length)
                return false;
            return profile.AsSpan(36, 4).SequenceEqual("acsp"u8) && (BinaryPrimitives.ReadUInt32BigEndian(profile.AsSpan(44, 4)) & 1) != 0;
        }
        void Error(string code, string message, XElement element) => diagnostics.Add(new(XisfDiagnosticSeverity.Error, code, message, element));
    }
}
