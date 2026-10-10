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
        if (root.Name != XName.Get("xisf", Namespace)) Error("XISF001", "The root element must use the XISF namespace.", root);
        if ((string?)root.Attribute("version") != "1.0") Error("XISF009", "The root version attribute must be '1.0'.", root);
        if (document.Xml.Declaration is null || !string.Equals(document.Xml.Declaration.Version, "1.0", StringComparison.Ordinal) ||
            !string.Equals(document.Xml.Declaration.Encoding, "UTF-8", StringComparison.OrdinalIgnoreCase))
            Error("XISF002", "The header must have an XML 1.0 UTF-8 declaration.", root);
        XElement[] metadata = root.Elements(XName.Get("Metadata", Namespace)).ToArray();
        if (metadata.Length != 1) Error("XISF003", "The header must contain exactly one Metadata element.", root);
        else
        {
            RequireMetadata("XISF:CreationTime", "TimePoint");
            RequireMetadata("XISF:CreatorApplication", "String");
            foreach (XElement property in metadata[0].Elements(XName.Get("Property", Namespace)))
                if (!((string?)property.Attribute("id") ?? "").StartsWith("XISF:", StringComparison.Ordinal))
                    Error("XISF006", "Metadata property identifiers must use the XISF namespace.", property);
        }
        foreach (XElement element in root.DescendantsAndSelf())
        {
            if (element.Name.NamespaceName == Namespace && element.Name.LocalName != "Reference" && element.Attribute("uid") is { } uid && !Identifier.IsMatch(uid.Value))
                Error("XISF007", $"Invalid core element uid '{uid.Value}'.", element);
            if (element.Name == XName.Get("Property", Namespace) && !PropertyIdentifier.IsMatch((string?)element.Attribute("id") ?? ""))
                Error("XISF008", "Invalid property identifier.", element);
            if (element.Attribute("byteOrder") is { } byteOrder && byteOrder.Value is not ("little" or "big"))
                Error("XISF010", "The byteOrder attribute must be 'little' or 'big'.", element);
            if (element.Name == XName.Get("ICCProfile", Namespace) && element.Attribute("byteOrder") is not null)
                Error("XISF011", "ICCProfile elements cannot define a byteOrder attribute.", element);
        }
        foreach (XisfTable table in document.Tables)
        {
            if (table.Fields.Count == 0) Error("XISF020", $"Table '{table.Id}' has no fields.", table.Xml);
            if (table.Fields.Select(f => f.Id).Distinct(StringComparer.Ordinal).Count() != table.Fields.Count) Error("XISF021", $"Table '{table.Id}' has duplicate fields.", table.Xml);
            if (table.Rows.Any(row => row.Cells.Count != table.Fields.Count)) Error("XISF022", $"Table '{table.Id}' has a row with an invalid cell count.", table.Xml);
        }
        foreach (XisfImage image in document.Images) ValidateImage(image, false);
        return diagnostics;

        void RequireMetadata(string id, string type)
        {
            XElement? property = metadata[0].Elements(XName.Get("Property", Namespace)).SingleOrDefault(e => (string?)e.Attribute("id") == id);
            if (property is null || (string?)property.Attribute("type") != type)
                Error("XISF004", $"Metadata requires one {id} property of type {type}.", metadata[0]);
        }
        void ValidateImage(XisfImage image, bool thumbnail)
        {
            if (image.Id is { } id && !Identifier.IsMatch(id)) Error("XISF030", $"Invalid image identifier '{id}'.", image.Xml);
            if (image.Offset < 0) Error("XISF031", "Image offset cannot be negative.", image.Xml);
            if (!Orientations.Contains(image.Orientation)) Error("XISF032", $"Invalid image orientation '{image.Orientation}'.", image.Xml);
            if (image.Xml.Attribute("uuid") is { } && image.Uuid is null) Error("XISF033", "Image uuid is not in canonical form.", image.Xml);
            if (!ColorSpaces.Contains(image.ColorSpace ?? "Gray")) Error("XISF034", $"Unsupported image color space '{image.ColorSpace}'.", image.Xml);
            if (image.ColorSpace == "Gray" && image.ChannelCount < 1 || image.ColorSpace is "RGB" or "CIELab" && image.ChannelCount < 3)
                Error("XISF035", "Image channel count is inconsistent with its color space.", image.Xml);
            if (image.ImageType is { } imageType && !ImageTypes.Contains(imageType)) Error("XISF039", $"Invalid image type '{imageType}'.", image.Xml);
            if (!double.IsFinite(image.LowerBound) || !double.IsFinite(image.UpperBound) || image.LowerBound >= image.UpperBound)
                Error("XISF040", "Image bounds must be finite and strictly increasing.", image.Xml);
            if (((string?)image.Xml.Attribute("location"))?.StartsWith("inline:", StringComparison.OrdinalIgnoreCase) == true)
                Error("XISF041", "Image pixel data cannot use an inline data block.", image.Xml);
            if (thumbnail && (image.Dimensions.Count != 2 || image.SampleFormat is not (XisfSampleFormat.UInt8 or XisfSampleFormat.UInt16) || image.ColorFilterArray is not null || image.Thumbnail is not null))
                Error("XISF036", "Thumbnail restrictions are not satisfied.", image.Xml);
            if (image.DisplayFunction is { } display && new[] { display.Midtones.Count, display.Shadows.Count, display.Highlights.Count, display.LowRange.Count, display.HighRange.Count }.Any(n => n != 4))
                Error("XISF037", "Display function vectors must have four components.", image.Xml);
            if (image.Resolution is { } resolution && (resolution.Horizontal <= 0 || resolution.Vertical <= 0 || resolution.Unit is not ("inch" or "cm")))
                Error("XISF038", "Invalid image resolution.", image.Xml);
            foreach (XisfFitsKeyword keyword in image.FitsKeywords)
                if (!FitsKeywordName.IsMatch(keyword.Name)) Error("XISF042", $"Invalid FITS keyword name '{keyword.Name}'.", image.Xml);
            if (image.RgbWorkingSpace is { } rgb)
            {
                if (!double.IsFinite(rgb.Gamma) || rgb.Gamma <= 0 || rgb.ChromaticityCoordinates.Count != 3 || rgb.LuminanceCoefficients.Count != 3 ||
                    rgb.ChromaticityCoordinates.Any(value => !double.IsFinite(value) || value is < 0 or > 1) ||
                    rgb.LuminanceCoefficients.Any(value => !double.IsFinite(value) || value is < 0 or > 1))
                    Error("XISF043", "Invalid RGB working-space parameters.", image.Xml);
            }
            if (image.IccProfile is { } profile && !IsEmbeddedIccProfile(profile))
                Error("XISF044", "The ICC profile is malformed or does not set the embedded-profile flag.", image.Xml);
            if (image.Thumbnail is { } child) ValidateImage(child, true);
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
