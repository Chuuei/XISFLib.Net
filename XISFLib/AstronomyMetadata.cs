using System.Globalization;

namespace XISFLib;

public sealed record AstronomyImageMetadata(
    string? ObjectName,
    string? Observer,
    string? Instrument,
    string? Telescope,
    string? Filter,
    double? ExposureSeconds,
    double? Gain,
    double? Offset,
    int? BinningX,
    int? BinningY,
    double? SensorTemperatureCelsius,
    DateTimeOffset? ObservationTime,
    IReadOnlyDictionary<string, string> Values);

public static class AstronomyMetadata
{
    public static AstronomyImageMetadata From(XisfImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        return Create(image.FitsKeywords, image.Properties.ToDictionary(pair => pair.Key, pair => pair.Value.Value, StringComparer.OrdinalIgnoreCase));
    }

    public static AstronomyImageMetadata From(XisfImageData image)
    {
        ArgumentNullException.ThrowIfNull(image);
        return Create(image.FitsKeywords, image.Properties.ToDictionary(pair => pair.Key,
            pair => pair.Value is XisfPropertyData property ? property.Value : pair.Value, StringComparer.OrdinalIgnoreCase));
    }

    public static AstronomyImageMetadata From(FitsHeader header)
    {
        ArgumentNullException.ThrowIfNull(header);
        IEnumerable<XisfFitsKeyword> keywords = header.Select(card => new XisfFitsKeyword(card.Keyword,
            Convert.ToString(card.Value, CultureInfo.InvariantCulture) ?? string.Empty, card.Comment));
        return Create(keywords, new Dictionary<string, object?>());
    }

    private static AstronomyImageMetadata Create(IEnumerable<XisfFitsKeyword> keywords, IReadOnlyDictionary<string, object?> properties)
    {
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
        foreach (XisfFitsKeyword keyword in keywords)
            values[keyword.Name] = keyword.Value;
        foreach ((string id, object? value) in properties)
            if (value is not null)
                values[id] = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

        string? FindString(params ReadOnlySpan<string> names)
        {
            foreach (string name in names)
                if (values.GetValueOrDefault(name) is { Length: > 0 } value && !string.IsNullOrWhiteSpace(value))
                    return value;
            return null;
        }

        double? FindDouble(params ReadOnlySpan<string> names) => TryDouble(FindString(names));
        int? FindInt(params ReadOnlySpan<string> names) => TryInt(FindString(names));
        DateTimeOffset? time = DateTimeOffset.TryParse(FindString("DATE-OBS", "Observation:Time:Start", "Observation:Time"),
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces, out DateTimeOffset parsed) ? parsed : null;

        return new AstronomyImageMetadata(
            FindString("OBJECT", "Observation:Object:Name"),
            FindString("OBSERVER", "Observation:Observer:Name"),
            FindString("INSTRUME", "Instrument:Camera:Name"),
            FindString("TELESCOP", "Instrument:Telescope:Name"),
            FindString("FILTER", "Instrument:Filter:Name"),
            FindDouble("EXPTIME", "EXPOSURE", "Observation:ExposureTime"),
            FindDouble("GAIN", "Instrument:Camera:Gain"),
            FindDouble("OFFSET", "Instrument:Camera:Offset"),
            FindInt("XBINNING", "Instrument:Camera:BinningX"),
            FindInt("YBINNING", "Instrument:Camera:BinningY"),
            FindDouble("CCD-TEMP", "SENSOR-T", "Instrument:Camera:Temperature"),
            time,
            values);
    }

    private static double? TryDouble(string? value) => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result) ? result : null;
    private static int? TryInt(string? value) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) ? result : null;
}

public sealed class CelestialWorldCoordinateSystem
{
    private readonly double _determinant;

    private CelestialWorldCoordinateSystem(double referencePixelX, double referencePixelY, double referenceRightAscension,
        double referenceDeclination, double cd11, double cd12, double cd21, double cd22, bool tangentPlane)
    {
        ReferencePixelX = referencePixelX;
        ReferencePixelY = referencePixelY;
        ReferenceRightAscension = referenceRightAscension;
        ReferenceDeclination = referenceDeclination;
        Cd11 = cd11;
        Cd12 = cd12;
        Cd21 = cd21;
        Cd22 = cd22;
        IsTangentPlane = tangentPlane;
        _determinant = cd11 * cd22 - cd12 * cd21;
        if (!double.IsFinite(_determinant) || Math.Abs(_determinant) < 1e-30)
            throw new ArgumentException("The WCS transformation matrix is singular.");
    }

    public double ReferencePixelX { get; }
    public double ReferencePixelY { get; }
    public double ReferenceRightAscension { get; }
    public double ReferenceDeclination { get; }
    public double Cd11 { get; }
    public double Cd12 { get; }
    public double Cd21 { get; }
    public double Cd22 { get; }
    public bool IsTangentPlane { get; }
    public double PixelScaleArcseconds => 3600 * Math.Sqrt(Math.Abs(_determinant));
    public double RotationDegrees => Math.Atan2(Cd21, Cd11) * 180 / Math.PI;

    public static bool TryCreate(IEnumerable<XisfFitsKeyword> keywords, out CelestialWorldCoordinateSystem? wcs)
    {
        ArgumentNullException.ThrowIfNull(keywords);
        Dictionary<string, string> values = keywords.GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.OrdinalIgnoreCase);
        return TryCreate(values, out wcs);
    }

    public static bool TryCreate(FitsHeader header, out CelestialWorldCoordinateSystem? wcs)
    {
        ArgumentNullException.ThrowIfNull(header);
        Dictionary<string, string> values = header.GroupBy(item => item.Keyword, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => Convert.ToString(group.Last().Value, CultureInfo.InvariantCulture) ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);
        return TryCreate(values, out wcs);
    }

    public (double RightAscension, double Declination) PixelToSky(double x, double y)
    {
        double dx = x + 1 - ReferencePixelX;
        double dy = y + 1 - ReferencePixelY;
        double xi = Cd11 * dx + Cd12 * dy;
        double eta = Cd21 * dx + Cd22 * dy;
        if (!IsTangentPlane)
            return (NormalizeDegrees(ReferenceRightAscension + xi), ReferenceDeclination + eta);

        xi *= Math.PI / 180;
        eta *= Math.PI / 180;
        double ra0 = ReferenceRightAscension * Math.PI / 180;
        double dec0 = ReferenceDeclination * Math.PI / 180;
        double denominator = Math.Cos(dec0) - eta * Math.Sin(dec0);
        double ra = ra0 + Math.Atan2(xi, denominator);
        double dec = Math.Atan2(Math.Sin(dec0) + eta * Math.Cos(dec0), Math.Sqrt(denominator * denominator + xi * xi));
        return (NormalizeDegrees(ra * 180 / Math.PI), dec * 180 / Math.PI);
    }

    public (double X, double Y) SkyToPixel(double rightAscension, double declination)
    {
        double xi;
        double eta;
        if (IsTangentPlane)
        {
            double ra = rightAscension * Math.PI / 180;
            double dec = declination * Math.PI / 180;
            double ra0 = ReferenceRightAscension * Math.PI / 180;
            double dec0 = ReferenceDeclination * Math.PI / 180;
            double deltaRa = ra - ra0;
            double denominator = Math.Sin(dec) * Math.Sin(dec0) + Math.Cos(dec) * Math.Cos(dec0) * Math.Cos(deltaRa);
            if (denominator <= 0) throw new ArgumentOutOfRangeException(nameof(rightAscension), "The sky position is outside the tangent-plane projection.");
            xi = Math.Cos(dec) * Math.Sin(deltaRa) / denominator * 180 / Math.PI;
            eta = (Math.Sin(dec) * Math.Cos(dec0) - Math.Cos(dec) * Math.Sin(dec0) * Math.Cos(deltaRa)) / denominator * 180 / Math.PI;
        }
        else
        {
            xi = NormalizeDelta(rightAscension - ReferenceRightAscension);
            eta = declination - ReferenceDeclination;
        }
        double dx = (Cd22 * xi - Cd12 * eta) / _determinant;
        double dy = (-Cd21 * xi + Cd11 * eta) / _determinant;
        return (dx + ReferencePixelX - 1, dy + ReferencePixelY - 1);
    }

    private static bool TryCreate(IReadOnlyDictionary<string, string> values, out CelestialWorldCoordinateSystem? wcs)
    {
        wcs = null;
        if (!Get(values, "CRPIX1", out double crpix1) || !Get(values, "CRPIX2", out double crpix2) ||
            !Get(values, "CRVAL1", out double crval1) || !Get(values, "CRVAL2", out double crval2))
            return false;
        double cd11, cd12, cd21, cd22;
        if (!Get(values, "CD1_1", out cd11) || !Get(values, "CD1_2", out cd12) || !Get(values, "CD2_1", out cd21) || !Get(values, "CD2_2", out cd22))
        {
            if (!Get(values, "CDELT1", out double cdelt1) || !Get(values, "CDELT2", out double cdelt2)) return false;
            double pc11 = Get(values, "PC1_1", out double p11) ? p11 : 1;
            double pc12 = Get(values, "PC1_2", out double p12) ? p12 : 0;
            double pc21 = Get(values, "PC2_1", out double p21) ? p21 : 0;
            double pc22 = Get(values, "PC2_2", out double p22) ? p22 : 1;
            cd11 = cdelt1 * pc11;
            cd12 = cdelt1 * pc12;
            cd21 = cdelt2 * pc21;
            cd22 = cdelt2 * pc22;
        }
        bool tangent = values.GetValueOrDefault("CTYPE1")?.Contains("TAN", StringComparison.OrdinalIgnoreCase) == true &&
                       values.GetValueOrDefault("CTYPE2")?.Contains("TAN", StringComparison.OrdinalIgnoreCase) == true;
        try
        {
            wcs = new CelestialWorldCoordinateSystem(crpix1, crpix2, crval1, crval2, cd11, cd12, cd21, cd22, tangent);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool Get(IReadOnlyDictionary<string, string> values, string name, out double value) =>
        double.TryParse(values.GetValueOrDefault(name), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);

    private static double NormalizeDegrees(double value) => (value % 360 + 360) % 360;
    private static double NormalizeDelta(double value) => (value + 540) % 360 - 180;
}