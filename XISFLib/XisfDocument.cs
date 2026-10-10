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
public sealed record XisfProperty(string Id, string Type, object? Value, string? Format, XElement Xml, string? Comment = null);
/// <summary>An explicitly typed property and display hints for serialization.</summary>
/// <param name="Type">The declared scalar or collection type.</param>
/// <param name="Value">The typed value; null represents an absent value.</param>
/// <param name="Format">The optional display or storage format.</param>
/// <param name="Comment">The optional explanatory comment.</param>
public sealed record XisfPropertyData(string Type, object? Value, string? Format = null, string? Comment = null);
/// <summary>A FITS keyword preserved in an XISF image header.</summary>
/// <param name="Name">The keyword, column, or mosaic name.</param>
/// <param name="Value">The typed value; null represents an absent value.</param>
/// <param name="Comment">The optional explanatory comment.</param>
public sealed record XisfFitsKeyword(string Name, string Value, string? Comment);
/// <summary>A repeating color-filter mosaic and its tile dimensions.</summary>
/// <param name="Pattern">The repeating color-filter tile in row-major order.</param>
/// <param name="Width">The tile width in pixels.</param>
/// <param name="Height">The tile height in pixels.</param>
/// <param name="Name">The keyword, column, or mosaic name.</param>
public sealed record XisfColorFilterArray(string Pattern, int Width, int Height, string? Name);
/// <summary>RGB gamma, chromaticity coordinates, and luminance coefficients.</summary>
/// <param name="Gamma">The working-space gamma.</param>
/// <param name="IsSrgb">Whether the sRGB transfer function is used.</param>
/// <param name="ChromaticityCoordinates">The RGB chromaticity coordinates.</param>
/// <param name="LuminanceCoefficients">The RGB luminance weights.</param>
public sealed record XisfRgbWorkingSpace(double Gamma, bool IsSrgb, IReadOnlyList<double> ChromaticityCoordinates, IReadOnlyList<double> LuminanceCoefficients);
/// <summary>Per-channel display parameters that do not modify stored samples.</summary>
/// <param name="Midtones">Per-channel midtone transfer parameters.</param>
/// <param name="Shadows">Per-channel shadow clipping points.</param>
/// <param name="Highlights">Per-channel highlight clipping points.</param>
/// <param name="LowRange">Per-channel output lower ranges.</param>
/// <param name="HighRange">Per-channel output upper ranges.</param>
public sealed record XisfDisplayFunction(IReadOnlyList<double> Midtones, IReadOnlyList<double> Shadows, IReadOnlyList<double> Highlights, IReadOnlyList<double> LowRange, IReadOnlyList<double> HighRange);
/// <summary>Horizontal and vertical resolution expressed in a named unit.</summary>
/// <param name="Horizontal">The horizontal resolution in the specified unit.</param>
/// <param name="Vertical">The vertical resolution in the specified unit.</param>
/// <param name="Unit">The resolution or table-column unit.</param>
public sealed record XisfResolution(double Horizontal, double Vertical, string Unit);

/// <summary>The identifier, scalar type, and display hints for a parsed table column.</summary>
/// <param name="Id">The case-sensitive identifier.</param>
/// <param name="Type">The declared scalar or collection type.</param>
/// <param name="Header">The optional display heading.</param>
/// <param name="Format">The optional display or storage format.</param>
public sealed record XisfTableField(string Id, string Type, string? Header, string? Format);
/// <summary>A parsed cell value and its original XML element.</summary>
/// <param name="Value">The typed value; null represents an absent value.</param>
/// <param name="Xml">The original XML element.</param>
public sealed record XisfTableCell(object? Value, XElement Xml);
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
    internal XisfDataSource Source { get; }

    /// <summary>Whether a signature is present; presence does not establish validity or trust.</summary>
    public bool HasXmlSignature => Xml.Descendants(XName.Get("Signature", "http://www.w3.org/2000/09/xmldsig#")).Any();

    /// <summary>Returns conformance diagnostics for the document.</summary>
    /// <returns>The detected conformance diagnostics.</returns>
    public IReadOnlyList<XisfDiagnostic> Validate() => XisfValidator.Validate(this);
}
