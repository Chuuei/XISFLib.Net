using System.Globalization;

namespace XISFLib.Viewer;

/// <summary>Text fields shared by image attribute and ancillary metadata editors.</summary>
public sealed class ImageEditorFields
{
    public string Id { get; set; } = "";
    public string ImageType { get; set; } = "";
    public string ColorSpace { get; set; } = "";
    public string Orientation { get; set; } = "0";
    public string Offset { get; set; } = "0";
    public string Uuid { get; set; } = "";
    public string ResolutionX { get; set; } = "";
    public string ResolutionY { get; set; } = "";
    public string ResolutionUnit { get; set; } = "inch";
    public string CfaPattern { get; set; } = "";
    public string CfaWidth { get; set; } = "";
    public string CfaHeight { get; set; } = "";
    public string CfaName { get; set; } = "";
    public string Gamma { get; set; } = "";
    public bool IsSrgb { get; set; }
    public string ChromaticityX { get; set; } = "";
    public string ChromaticityY { get; set; } = "";
    public string Luminance { get; set; } = "";
    public string DisplayFunction { get; set; } = "";

    public static ImageEditorFields FromImage(XisfImageData image) => new()
    {
        Id = image.Id ?? "", ImageType = image.ImageType ?? "", ColorSpace = image.ColorSpace ?? "", Orientation = image.Orientation ?? "0",
        Offset = Number(image.Offset), Uuid = image.Uuid?.ToString("D") ?? "",
        ResolutionX = image.Resolution is { } resolution ? Number(resolution.Horizontal) : "",
        ResolutionY = image.Resolution is { } vertical ? Number(vertical.Vertical) : "", ResolutionUnit = image.Resolution?.Unit ?? "inch",
        CfaPattern = image.ColorFilterArray?.Pattern ?? "", CfaWidth = image.ColorFilterArray?.Width.ToString(CultureInfo.InvariantCulture) ?? "",
        CfaHeight = image.ColorFilterArray?.Height.ToString(CultureInfo.InvariantCulture) ?? "", CfaName = image.ColorFilterArray?.Name ?? "",
        Gamma = image.RgbWorkingSpace is { } rgb ? Number(rgb.Gamma) : "", IsSrgb = image.RgbWorkingSpace?.IsSrgb == true,
        ChromaticityX = Numbers(image.RgbWorkingSpace?.ChromaticityCoordinates), ChromaticityY = Numbers(image.RgbWorkingSpace?.YChromaticityCoordinates),
        Luminance = Numbers(image.RgbWorkingSpace?.LuminanceCoefficients),
        DisplayFunction = image.DisplayFunction is { } display ? string.Join("; ", new[] { Numbers(display.Midtones), Numbers(display.Shadows), Numbers(display.Highlights), Numbers(display.LowRange), Numbers(display.HighRange) }) : ""
    };

    /// <summary>Parses a complete snapshot without modifying the image or scientific pixel storage.</summary>
    public ImageAttributeEdit Parse(XisfImageData image)
    {
        string? id = Empty(Id);
        if (id is not null && (!char.IsAsciiLetter(id[0]) && id[0] != '_' || id.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_')))
            throw new FormatException("Image identifier must start with a letter or underscore and contain letters, digits or underscores.");
        string? type = Empty(ImageType);
        type = type switch { "BiasFrame" => "Bias", "DarkFrame" => "Dark", "FlatField" => "Flat", "LightFrame" or "ScienceFrame" => "Light", _ => type };
        if (type is not null && type is not ("Bias" or "Dark" or "Flat" or "Light" or "MasterBias" or "MasterDark" or "MasterFlat" or "MasterLight" or
            "DefectMap" or "RejectionMapHigh" or "RejectionMapLow" or "BinaryRejectionMapHigh" or "BinaryRejectionMapLow" or "SlopeMap" or "WeightMap"))
            throw new FormatException("Invalid XISF image type.");
        double offset = Optional(Offset) ?? 0;
        if (!double.IsFinite(offset) || offset < 0) throw new FormatException("Offset must be finite and nonnegative.");
        Guid? uuid = string.IsNullOrWhiteSpace(Uuid) ? null : Guid.Parse(Uuid);
        if (uuid is { } guid && (guid.ToString("D")[14] != '4' || !"89ab".Contains(guid.ToString("D")[19])))
            throw new FormatException("Image UUIDs must be version 4.");
        if (Orientation is not ("0" or "flip" or "90" or "90;flip" or "-90" or "-90;flip" or "180" or "180;flip"))
            throw new FormatException("Invalid image orientation.");
        string? color = Empty(ColorSpace);
        if (color is not null && (color is not ("Gray" or "RGB" or "CIELab") || color != "Gray" && image.ChannelCount < 3))
            throw new FormatException("Invalid color space for the image channel count.");
        double? x = Optional(ResolutionX), y = Optional(ResolutionY);
        XisfResolution? resolution = null;
        if (x is not null || y is not null)
        {
            if (x is null || y is null) throw new FormatException("Both resolution values are required.");
            if (!double.IsFinite(x.Value) || !double.IsFinite(y.Value) || x <= 0 || y <= 0 || ResolutionUnit is not ("inch" or "cm"))
                throw new FormatException("Resolution requires positive finite values and inch or cm units.");
            resolution = new(x.Value, y.Value, ResolutionUnit);
        }
        XisfColorFilterArray? cfa = null;
        if (!string.IsNullOrWhiteSpace(CfaPattern))
        {
            int width = int.Parse(CfaWidth, CultureInfo.InvariantCulture), height = int.Parse(CfaHeight, CultureInfo.InvariantCulture);
            string pattern = CfaPattern.Trim();
            if (width <= 0 || height <= 0 || pattern.Length != (long)width * height || pattern.Any(c => !"0RGBWCMY".Contains(c)))
                throw new FormatException("CFA pattern must match its positive dimensions and contain XISF filter symbols.");
            cfa = new(pattern, width, height, Empty(CfaName));
        }
        XisfRgbWorkingSpace? space = null;
        if (Optional(Gamma) is { } gamma)
        {
            space = new(gamma, IsSrgb, ParseNumbers(ChromaticityX, 3), ParseNumbers(Luminance, 3))
                { YChromaticityCoordinates = ParseNumbers(ChromaticityY, 3), Name = image.RgbWorkingSpace?.Name };
            space = space with { LuminanceCoefficients = XisfColorSpace.DeriveLuminanceCoefficients(space) };
        }
        XisfDisplayFunction? display = null;
        if (!string.IsNullOrWhiteSpace(DisplayFunction))
        {
            string[] groups = DisplayFunction.Split(';', StringSplitOptions.TrimEntries);
            if (groups.Length != 5) throw new FormatException("A display function requires five semicolon-separated groups of four values.");
            display = new(ParseNumbers(groups[0], 4), ParseNumbers(groups[1], 4), ParseNumbers(groups[2], 4), ParseNumbers(groups[3], 4), ParseNumbers(groups[4], 4))
                { Name = image.DisplayFunction?.Name };
        }
        return new(id, type, color, Orientation, offset, uuid, resolution, cfa, space, display);
    }

    private static string? Empty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static double? Optional(string value) => string.IsNullOrWhiteSpace(value) ? null : double.Parse(value, CultureInfo.InvariantCulture);
    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static string Numbers(IEnumerable<double>? values) => values is null ? "" : string.Join(", ", values.Select(Number));
    private static double[] ParseNumbers(string text, int count)
    {
        var values = text.Split(',', StringSplitOptions.TrimEntries).Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray();
        if (values.Length != count || values.Any(v => !double.IsFinite(v))) throw new FormatException($"Expected {count} finite comma-separated values.");
        return values;
    }
}

/// <summary>A parsed image metadata snapshot. Applying never changes sample bytes or geometry.</summary>
public sealed record ImageAttributeEdit(string? Id, string? ImageType, string? ColorSpace, string Orientation, double Offset, Guid? Uuid,
    XisfResolution? Resolution, XisfColorFilterArray? ColorFilterArray, XisfRgbWorkingSpace? WorkingSpace, XisfDisplayFunction? DisplayFunction)
{
    public void Apply(XisfImageData image)
    {
        image.Id = Id; image.ImageType = ImageType; image.ColorSpace = ColorSpace; image.Orientation = Orientation;
        image.Offset = Offset; image.Uuid = Uuid; image.Resolution = Resolution; image.ColorFilterArray = ColorFilterArray;
        image.RgbWorkingSpace = WorkingSpace; image.DisplayFunction = DisplayFunction;
    }
}
