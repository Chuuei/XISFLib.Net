using System.Xml.Linq;

namespace XISFLib;

/// <summary>The numeric representation of each stored image sample.</summary>
public enum XisfSampleFormat
{
    /// <summary>Unsigned 8-bit integer samples.</summary>
    UInt8,
    /// <summary>Unsigned 16-bit integer samples.</summary>
    UInt16,
    /// <summary>Unsigned 32-bit integer samples.</summary>
    UInt32,
    /// <summary>Unsigned 64-bit integer samples.</summary>
    UInt64,
    /// <summary>IEEE 32-bit real samples.</summary>
    Float32,
    /// <summary>IEEE 64-bit real samples.</summary>
    Float64,
    /// <summary>Complex samples with two 32-bit components.</summary>
    Complex32,
    /// <summary>Complex samples with two 64-bit components.</summary>
    Complex64
}

/// <summary>The order of channel samples within an image buffer.</summary>
public enum XisfPixelLayout
{
    /// <summary>Channels occupy consecutive image-sized planes.</summary>
    Planar,
    /// <summary>Channel samples alternate within each pixel.</summary>
    Interleaved
}

/// <summary>Reports malformed or unsupported XISF input and resource-limit violations.</summary>
public class XisfFormatException : Exception
{
    /// <summary>Initializes the exception with a message and optional underlying cause.</summary>
    /// <param name="message">The explanatory exception message.</param>
    public XisfFormatException(string message) : base(message) { }
    /// <summary>Initializes the exception with a message and optional underlying cause.</summary>
    /// <param name="message">The explanatory exception message.</param>
    /// <param name="innerException">The underlying cause of the format error.</param>
    public XisfFormatException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Identifies an unsupported feature while allowing the rest of an XISF unit to remain accessible.</summary>
public sealed class XisfUnsupportedFeatureException : XisfFormatException
{
    /// <summary>Initializes an unsupported-feature exception.</summary>
    /// <param name="message">The unsupported type, vocabulary or codec.</param>
    public XisfUnsupportedFeatureException(string message) : base(message) { }
}

/// <summary>A core object retained in XML when its representation is unsupported.</summary>
/// <param name="Xml">The original element.</param>
/// <param name="Reason">The reason decoding was unavailable.</param>
public sealed record XisfUnavailableObject(XElement Xml, string Reason);

/// <summary>Reports a data block whose bytes do not match its declared checksum.</summary>
public sealed class XisfChecksumException : XisfFormatException
{
    /// <summary>Initializes the exception with a message and optional underlying cause.</summary>
    /// <param name="message">The explanatory exception message.</param>
    public XisfChecksumException(string message) : base(message) { }
}

/// <summary>A parsed typed property, display hints, and its original XML element.</summary>
/// <param name="Id">The case-sensitive identifier.</param>
/// <param name="Type">The declared scalar or collection type.</param>
/// <param name="Value">The typed value; null represents an absent value.</param>
/// <param name="Format">The optional display or storage format.</param>
/// <param name="Xml">The original XML element.</param>
/// <param name="Comment">The optional explanatory comment.</param>
public sealed record XisfProperty(string Id, string Type, object? Value, string? Format, XElement Xml, string? Comment = null)
{
    /// <summary>Whether the value could be decoded with the available type and codec support.</summary>
    public bool IsAvailable { get; init; } = true;
    /// <summary>The reason a value is unavailable; original XML remains preserved.</summary>
    public string? UnavailableReason { get; init; }
    internal XisfDataSource? Source { get; init; }
}
/// <summary>An explicitly typed property and display hints for serialization.</summary>
/// <param name="Type">The declared scalar or collection type.</param>
/// <param name="Value">The typed value; null represents an absent value.</param>
/// <param name="Format">The optional display or storage format.</param>
/// <param name="Comment">The optional explanatory comment.</param>
public sealed record XisfPropertyData(string Type, object? Value, string? Format = null, string? Comment = null);
/// <summary>A FITS keyword preserved in an XISF image header.</summary>
/// <param name="Name">The FITS keyword name, such as OBJECT or HISTORY.</param>
/// <param name="Value">The textual FITS value, including any quoting required for a string; empty for COMMENT and HISTORY.</param>
/// <param name="Comment">The explanatory comment, or the commentary text for COMMENT and HISTORY.</param>
public sealed record XisfFitsKeyword(string Name, string Value, string? Comment);
/// <summary>A repeating color-filter mosaic and its tile dimensions.</summary>
/// <param name="Pattern">The repeating color-filter tile in row-major order.</param>
/// <param name="Width">The tile width in pixels.</param>
/// <param name="Height">The tile height in pixels.</param>
/// <param name="Name">The optional human-readable mosaic name.</param>
public sealed record XisfColorFilterArray(string Pattern, int Width, int Height, string? Name);
/// <summary>RGB gamma, chromaticity coordinates, and luminance coefficients.</summary>
/// <param name="Gamma">The working-space gamma.</param>
/// <param name="IsSrgb">Whether the sRGB transfer function is used.</param>
/// <param name="ChromaticityCoordinates">The x chromaticities of the red, green and blue primaries, in that order.</param>
/// <param name="LuminanceCoefficients">The RGB luminance weights.</param>
public sealed record XisfRgbWorkingSpace(double Gamma, bool IsSrgb, IReadOnlyList<double> ChromaticityCoordinates, IReadOnlyList<double> LuminanceCoefficients)
{
    /// <summary>The y chromaticities of the three primaries; defaults to D50-adapted sRGB.</summary>
    public IReadOnlyList<double> YChromaticityCoordinates { get; init; } = [0.330856, 0.597871, 0.066044];
    /// <summary>The optional human-readable working-space name.</summary>
    public string? Name { get; init; }
    /// <summary>The standard D50-adapted sRGB working space.</summary>
    public static XisfRgbWorkingSpace Srgb { get; } = new(2.4, true, [0.648431, 0.321152, 0.155886], [0.222491, 0.716888, 0.060621]) { Name = "sRGB IEC61966-2.1" };
}
/// <summary>Per-channel display parameters that do not modify stored samples.</summary>
/// <param name="Midtones">Per-channel midtone transfer parameters.</param>
/// <param name="Shadows">Per-channel shadow clipping points.</param>
/// <param name="Highlights">Per-channel highlight clipping points.</param>
/// <param name="LowRange">Per-channel output lower ranges.</param>
/// <param name="HighRange">Per-channel output upper ranges.</param>
public sealed record XisfDisplayFunction(IReadOnlyList<double> Midtones, IReadOnlyList<double> Shadows, IReadOnlyList<double> Highlights, IReadOnlyList<double> LowRange, IReadOnlyList<double> HighRange)
{
    /// <summary>The optional human-readable display-function name.</summary>
    public string? Name { get; init; }
}
/// <summary>Horizontal and vertical resolution expressed in a named unit.</summary>
/// <param name="Horizontal">The horizontal resolution in the specified unit.</param>
/// <param name="Vertical">The vertical resolution in the specified unit.</param>
/// <param name="Unit">The resolution unit: inch or cm.</param>
public sealed record XisfResolution(double Horizontal, double Vertical, string Unit);

/// <summary>The identifier, scalar or collection type, and display hints for a parsed table column.</summary>
/// <param name="Id">The case-sensitive identifier.</param>
/// <param name="Type">The declared scalar or collection type.</param>
/// <param name="Header">The optional display heading.</param>
/// <param name="Format">The optional display or storage format.</param>
public sealed record XisfTableField(string Id, string Type, string? Header, string? Format);
/// <summary>A parsed cell value and its original XML element.</summary>
/// <param name="Value">The typed value; null represents an absent value.</param>
/// <param name="Xml">The original XML element.</param>
public sealed record XisfTableCell(object? Value, XElement Xml)
{
    /// <summary>Whether this cell's type and codec are supported.</summary>
    public bool IsAvailable { get; init; } = true;
    /// <summary>The reason an original cell could not be decoded.</summary>
    public string? UnavailableReason { get; init; }
    internal XisfDataSource? Source { get; init; }
}
/// <summary>An ordered collection of parsed table cells.</summary>
/// <param name="Cells">The ordered cells in this row.</param>
public sealed record XisfTableRow(IReadOnlyList<XisfTableCell> Cells);
/// <summary>The definition of a column in an editable table.</summary>
/// <param name="Id">The case-sensitive identifier.</param>
/// <param name="Type">The declared scalar or collection type.</param>
/// <param name="Header">The optional display heading.</param>
/// <param name="Format">The optional display or storage format.</param>
public sealed record XisfTableFieldData(string Id, string Type, string? Header = null, string? Format = null);

/// <summary>An editable table whose rows must match its field count.</summary>
public sealed class XisfTableData
{
    /// <summary>The identifier of this item within its containing document.</summary>
    public required string Id { get; init; }
    /// <summary>The optional human-readable table caption.</summary>
    public string? Caption { get; init; }
    /// <summary>The optional explanatory table comment.</summary>
    public string? Comment { get; init; }
    /// <summary>The ordered table field definitions.</summary>
    public IList<XisfTableFieldData> Fields { get; } = [];
    /// <summary>The ordered rows; cells follow the field or column order.</summary>
    public IList<IReadOnlyList<object?>> Rows { get; } = [];
}

/// <summary>A parsed table with field definitions and typed rows.</summary>
public sealed class XisfTable
{
    internal XisfTable(string id, string? caption, IReadOnlyList<XisfTableField> fields, IReadOnlyList<XisfTableRow> rows, XElement xml)
    {
        Id = id;
        Caption = caption;
        Fields = fields;
        Rows = rows;
        Xml = xml;
    }

    /// <summary>The identifier of this item within its containing document.</summary>
    public string Id { get; }
    /// <summary>The optional human-readable table caption.</summary>
    public string? Caption { get; }
    /// <summary>The optional explanatory table comment.</summary>
    public string? Comment => (string?)Xml.Attribute("comment");
    /// <summary>The ordered table field definitions.</summary>
    public IReadOnlyList<XisfTableField> Fields { get; }
    /// <summary>The ordered rows; cells follow the field or column order.</summary>
    public IReadOnlyList<XisfTableRow> Rows { get; }
    /// <summary>The mutable XML representation; edits do not automatically refresh parsed values.</summary>
    public XElement Xml { get; }
}

/// <summary>A parsed XISF header with metadata, tables, and lazily decoded images.</summary>
public sealed class XisfDocument
{
    internal XisfDocument(
        string version,
        XDocument xml,
        IReadOnlyDictionary<string, XisfProperty> metadata,
        IReadOnlyDictionary<string, XisfProperty> properties,
        IReadOnlyList<XisfTable> tables,
        IReadOnlyList<XisfImage> images,
        XisfDataSource source)
    {
        Version = version;
        Xml = xml;
        Metadata = metadata;
        Properties = properties;
        Tables = tables;
        Images = images;
        Source = source;
    }

    /// <summary>The XISF version declared by the header.</summary>
    public string Version { get; }
    /// <summary>The mutable XML representation; edits do not automatically refresh parsed values.</summary>
    public XDocument Xml { get; }
    /// <summary>Document-level metadata indexed by property identifiers.</summary>
    public IReadOnlyDictionary<string, XisfProperty> Metadata { get; }
    /// <summary>Typed properties indexed by case-sensitive identifiers.</summary>
    public IReadOnlyDictionary<string, XisfProperty> Properties { get; }
    /// <summary>The tables associated with this image or document.</summary>
    public IReadOnlyList<XisfTable> Tables { get; }
    /// <summary>The images in document order.</summary>
    public IReadOnlyList<XisfImage> Images { get; }
    /// <summary>Unsupported objects retained in the header, without preventing access to supported objects.</summary>
    public IReadOnlyList<XisfUnavailableObject> UnavailableObjects { get; internal init; } = [];
    internal XisfDataSource Source { get; }

    /// <summary>Whether a signature is present; presence does not establish validity or trust.</summary>
    public bool HasXmlSignature => DetachedSignature is not null || Xml.Descendants(XName.Get("Signature", "http://www.w3.org/2000/09/xmldsig#")).Any();

    /// <summary>The optional detached XML signature, stored after the root in the serialized header.</summary>
    public XElement? DetachedSignature => Xml.Annotation<XisfDetachedSignature>()?.Element;

    /// <summary>Returns conformance diagnostics for the document.</summary>
    /// <returns>The detected conformance diagnostics.</returns>
    public IReadOnlyList<XisfDiagnostic> Validate() => XisfValidator.Validate(this);

    /// <summary>Runs semantic checks and validates the isolated root against caller-supplied schemas.</summary>
    /// <param name="schemas">A schema set, normally loaded from the official XISF XSD.</param>
    /// <returns>Semantic and XML schema diagnostics; the detached signature is validated separately.</returns>
    public IReadOnlyList<XisfDiagnostic> Validate(System.Xml.Schema.XmlSchemaSet schemas)
    {
        ArgumentNullException.ThrowIfNull(schemas);
        List<XisfDiagnostic> diagnostics = [.. Validate()];
        System.Xml.Schema.Extensions.Validate(Xml, schemas, (_, args) => diagnostics.Add(new(
            args.Severity == System.Xml.Schema.XmlSeverityType.Error ? XisfDiagnosticSeverity.Error : XisfDiagnosticSeverity.Warning,
            "XISF100", args.Message)), false);
        return diagnostics;
    }

}
