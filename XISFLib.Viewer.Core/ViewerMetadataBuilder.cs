using System.Collections;
using System.Globalization;
using System.Xml.Linq;

namespace XISFLib.Viewer;

/// <summary>Builds the shared, ordered information tree without decoding image pixel buffers.</summary>
public static class ViewerMetadataBuilder
{
    public static IReadOnlyList<MetadataNode> BuildMetadata(XisfDocument document, string path)
    {
        FileInfo file = new(path);
        List<MetadataNode> roots =
        [
            new("File", children:
            [
                new("Name", file.Name),
                new("Folder", file.DirectoryName ?? string.Empty),
                new("Size", $"{file.Length:N0} bytes"),
                new("Last modified", file.LastWriteTime.ToString("G", CultureInfo.CurrentCulture))
            ], isExpanded: true),
            new("Document", children:
            [
                new("XISF version", document.Version),
                new("Images", document.Images.Count.ToString(CultureInfo.CurrentCulture)),
                new("Tables", document.Tables.Count.ToString(CultureInfo.CurrentCulture)),
                new("XML signature", document.HasXmlSignature ? "Present" : "Not present")
            ], isExpanded: true),
            BuildPropertiesNode("Metadata", document.Metadata),
            BuildPropertiesNode("Document properties", document.Properties),
            BuildTablesNode(document.Tables),
            new("Images", children: document.Images.Select((image, index) => BuildImageNode(image, index + 1)).ToArray()),
            new("Complete XISF header", children: [BuildXmlNode(document.Xml.Root!)])
        ];

        return roots;
    }

    public static IReadOnlyList<MetadataNode> BuildFitsMetadata(FitsDocument document, string path)
    {
        FileInfo file = new(path);
        return
        [
            new("File", children:
            [
                new("Name", file.Name),
                new("Folder", file.DirectoryName ?? string.Empty),
                new("Size", $"{file.Length:N0} bytes"),
                new("Last modified", file.LastWriteTime.ToString("G", CultureInfo.CurrentCulture))
            ], isExpanded: true),
            new("Document", children:
            [
                new("Format", "FITS"),
                new("Header/data units", document.Hdus.Count.ToString(CultureInfo.CurrentCulture)),
                new("Images", document.Hdus.Count(hdu => hdu.Content is FitsImage).ToString(CultureInfo.CurrentCulture))
            ], isExpanded: true),
            new("Header/data units", children: document.Hdus.Select((hdu, index) => new MetadataNode(
                $"HDU {index + 1}: {hdu.Type}",
                children: hdu.Header.Select(card => new MetadataNode(card.Keyword, FormatValue(card.Value),
                    card.Comment is null ? null : [new("Comment", card.Comment)])).ToArray())).ToArray())
        ];
    }

    private static MetadataNode BuildImageNode(XisfImage image, int number)
    {
        List<MetadataNode> details =
        [
            new("Identifier", image.Id ?? "Not specified"),
            new("Dimensions", string.Join(" × ", image.Dimensions)),
            new("Channels", image.ChannelCount.ToString(CultureInfo.CurrentCulture)),
            new("Pixels per channel", image.PixelCount.ToString("N0", CultureInfo.CurrentCulture)),
            new("Sample format", image.SampleFormat.ToString()),
            new("Pixel storage", image.PixelStorage.ToString()),
            new("Byte order", image.IsBigEndian ? "Big-endian" : "Little-endian"),
            new("Color space", image.ColorSpace ?? "Not specified"),
            new("Image type", image.ImageType ?? "Not specified"),
            new("Offset / pedestal", FormatValue(image.Offset)),
            new("Orientation", image.Orientation),
            new("UUID", image.Uuid?.ToString("D") ?? "Not specified"),
            new("Sample bounds", $"{FormatValue(image.LowerBound)} to {FormatValue(image.UpperBound)}"),
            BuildPropertiesNode("Properties", image.Properties),
            BuildTablesNode(image.Tables),
            new("FITS keywords", children: image.FitsKeywords.Select(keyword =>
                new MetadataNode(keyword.Name, keyword.Value,
                    keyword.Comment is null ? null : [new("Comment", keyword.Comment)])).ToArray())
        ];

        if (image.Resolution is { } resolution)
            details.Add(new("Resolution", $"{FormatValue(resolution.Horizontal)} × {FormatValue(resolution.Vertical)} {resolution.Unit}"));
        if (image.ColorFilterArray is { } cfa)
            details.Add(new("Color filter array", $"{cfa.Pattern} ({cfa.Width} × {cfa.Height})",
                cfa.Name is null ? null : [new("Name", cfa.Name)]));
        if (image.RgbWorkingSpace is { } rgb)
            details.Add(new("RGB working space", children:
            [
                new("Transfer function", rgb.IsSrgb ? "sRGB" : $"Gamma {FormatValue(rgb.Gamma)}"),
                new("Chromaticity coordinates", FormatValue(rgb.ChromaticityCoordinates)),
                new("Luminance coefficients", FormatValue(rgb.LuminanceCoefficients))
            ]));
        if (image.DisplayFunction is { } display)
            details.Add(new("Display function", children:
            [
                new("Midtones", FormatValue(display.Midtones)),
                new("Shadows", FormatValue(display.Shadows)),
                new("Highlights", FormatValue(display.Highlights)),
                new("Low range", FormatValue(display.LowRange)),
                new("High range", FormatValue(display.HighRange))
            ]));
        if (image.IccProfile is { } profile)
            details.Add(new("ICC profile", $"{profile.Length:N0} bytes"));
        if (image.Thumbnail is { } thumbnail)
            details.Add(new("Thumbnail", children: BuildImageNode(thumbnail, 0).Children));

        return new($"Image {number}: {image.Id ?? "unnamed"}", children: details);
    }

    private static MetadataNode BuildPropertiesNode(string name, IReadOnlyDictionary<string, XisfProperty> properties) =>
        new(name, properties.Count == 0 ? "None" : null,
            properties.Select(pair => new MetadataNode(pair.Key, FormatValue(pair.Value.Value),
                new MetadataNode?[]
                {
                    pair.Value.Type.Length == 0 ? null : new("Type", pair.Value.Type),
                    pair.Value.Format is null ? null : new("Format", pair.Value.Format),
                    pair.Value.Comment is null ? null : new("Comment", pair.Value.Comment)
                }.OfType<MetadataNode>().ToArray())).ToArray());

    private static MetadataNode BuildTablesNode(IReadOnlyList<XisfTable> tables) =>
        new("Tables", tables.Count == 0 ? "None" : null, tables.Select(table =>
            new MetadataNode(table.Id, table.Caption, children:
            [
                new("Fields", children: table.Fields.Select(field => new MetadataNode(field.Id, field.Type,
                    new MetadataNode?[]
                    {
                        field.Header is null ? null : new("Header", field.Header),
                        field.Format is null ? null : new("Format", field.Format)
                    }.OfType<MetadataNode>().ToArray())).ToArray()),
                new("Rows", children: table.Rows.Select((row, index) => new MetadataNode($"Row {index + 1}", children:
                    row.Cells.Select((cell, cellIndex) => new MetadataNode(
                        cellIndex < table.Fields.Count ? table.Fields[cellIndex].Id : $"Cell {cellIndex + 1}",
                        FormatValue(cell.Value))).ToArray())).ToArray())
            ])).ToArray());

    private static MetadataNode BuildXmlNode(XElement element)
    {
        List<MetadataNode> children = element.Attributes()
            .Select(attribute => new MetadataNode(FriendlyName(attribute.Name.LocalName), attribute.Value))
            .ToList();
        children.AddRange(element.Elements().Select(BuildXmlNode));

        string? value = null;
        if (!element.HasElements)
        {
            string text = element.Value.Trim();
            if (text.Length > 0)
                value = IsEmbeddedData(element, text) ? $"Embedded data ({text.Length:N0} characters)" : text;
        }

        return new(FriendlyName(element.Name.LocalName), value, children);
    }

    private static bool IsEmbeddedData(XElement element, string value) =>
        value.Length > 512 && (element.AncestorsAndSelf().Attributes("location")
                                   .Any(attribute => attribute.Value.StartsWith("inline:", StringComparison.OrdinalIgnoreCase)) ||
                               element.Name.LocalName.Equals("Data", StringComparison.OrdinalIgnoreCase));

    private static string FriendlyName(string name)
    {
        if (name.Length == 0)
            return name;

        return string.Concat(name.Select((character, index) =>
            index > 0 && char.IsUpper(character) && !char.IsUpper(name[index - 1]) ? $" {char.ToLowerInvariant(character)}" : character.ToString()));
    }

    private static string FormatValue(object? value)
    {
        if (value is null)
            return "Not specified";
        if (value is byte[] bytes)
            return $"{bytes.Length:N0} bytes";
        if (value is string text)
            return text;
        if (value is IEnumerable values)
        {
            // A tree summary must not enumerate or format an entire large vector/matrix.
            if (value is Array { Length: > 128 } array) return $"{array.Length:N0} elements (inspect in Image metadata)";
            return string.Join(", ", values.Cast<object?>().Take(128).Select(FormatValue));
        }
        if (value is IFormattable formattable)
            return formattable.ToString(null, CultureInfo.CurrentCulture) ?? string.Empty;
        return value.ToString() ?? string.Empty;
    }

}
