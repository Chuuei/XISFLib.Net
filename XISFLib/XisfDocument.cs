using System.Xml.Linq;

namespace XISFLib;

public enum XisfSampleFormat
{
    UInt8,
    UInt16,
    UInt32,
    UInt64,
    Float32,
    Float64,
    Complex32,
    Complex64
}

public enum XisfPixelLayout
{
    Planar,
    Interleaved
}

public class XisfFormatException : Exception
{
    public XisfFormatException(string message) : base(message) { }
    public XisfFormatException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class XisfChecksumException : XisfFormatException
{
    public XisfChecksumException(string message) : base(message) { }
}

public sealed record XisfProperty(string Id, string Type, object? Value, string? Format, XElement Xml, string? Comment = null);
public sealed record XisfPropertyData(string Type, object? Value, string? Format = null, string? Comment = null);
public sealed record XisfFitsKeyword(string Name, string Value, string? Comment);
public sealed record XisfColorFilterArray(string Pattern, int Width, int Height, string? Name);
public sealed record XisfRgbWorkingSpace(double Gamma, bool IsSrgb, IReadOnlyList<double> ChromaticityCoordinates, IReadOnlyList<double> LuminanceCoefficients);
public sealed record XisfDisplayFunction(IReadOnlyList<double> Midtones, IReadOnlyList<double> Shadows, IReadOnlyList<double> Highlights, IReadOnlyList<double> LowRange, IReadOnlyList<double> HighRange);
public sealed record XisfResolution(double Horizontal, double Vertical, string Unit);

public sealed record XisfTableField(string Id, string Type, string? Header, string? Format);
public sealed record XisfTableCell(object? Value, XElement Xml);
public sealed record XisfTableRow(IReadOnlyList<XisfTableCell> Cells);
public sealed record XisfTableFieldData(string Id, string Type, string? Header = null, string? Format = null);

public sealed class XisfTableData
{
    public required string Id { get; init; }
    public string? Caption { get; init; }
    public IList<XisfTableFieldData> Fields { get; } = [];
    public IList<IReadOnlyList<object?>> Rows { get; } = [];
}

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

    public string Id { get; }
    public string? Caption { get; }
    public IReadOnlyList<XisfTableField> Fields { get; }
    public IReadOnlyList<XisfTableRow> Rows { get; }
    public XElement Xml { get; }
}

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

    public string Version { get; }
    public XDocument Xml { get; }
    public IReadOnlyDictionary<string, XisfProperty> Metadata { get; }
    public IReadOnlyDictionary<string, XisfProperty> Properties { get; }
    public IReadOnlyList<XisfTable> Tables { get; }
    public IReadOnlyList<XisfImage> Images { get; }
    internal XisfDataSource Source { get; }

    public bool HasXmlSignature => Xml.Descendants(XName.Get("Signature", "http://www.w3.org/2000/09/xmldsig#")).Any();

    public IReadOnlyList<XisfDiagnostic> Validate() => XisfValidator.Validate(this);
}
