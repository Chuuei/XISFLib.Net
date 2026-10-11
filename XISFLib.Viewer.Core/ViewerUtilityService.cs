using System.Globalization;

namespace XISFLib.Viewer;

public static class ViewerUtilityService
{
    public static IReadOnlyList<UtilityEntry> Overview(XisfImageData image)
    {
        var m = AstronomyMetadata.From(image);
        return [new("Object", m.ObjectName), new("Observer", m.Observer), new("Instrument", m.Instrument), new("Telescope", m.Telescope),
            new("Filter", m.Filter), new("Exposure", Optional(m.ExposureSeconds, " s")), new("Gain", Optional(m.Gain)), new("Offset", Optional(m.Offset)),
            new("Binning", m.BinningX is null && m.BinningY is null ? null : $"{m.BinningX?.ToString(CultureInfo.CurrentCulture) ?? "?"} × {m.BinningY?.ToString(CultureInfo.CurrentCulture) ?? "?"}"),
            new("Sensor temperature", Optional(m.SensorTemperatureCelsius, " °C")), new("Observation time", m.ObservationTime?.ToString("u", CultureInfo.CurrentCulture)),
            new("Recognized/raw values", m.Values.Count.ToString(CultureInfo.CurrentCulture))];
    }
    public static IReadOnlyList<XisfChannelStatistics> Statistics(XisfImageData image, int limit, bool normalize) =>
        image.CalculateStatistics(new() { MaximumSamplesPerChannel = limit, Normalize = normalize });
    public static HistogramReport Histogram(XisfImageData image, int channel, int bins)
    {
        if (bins is < 1 or > 65_536) throw new ArgumentOutOfRangeException(nameof(bins), "Histogram bins must be from 1 through 65,536.");
        var h = image.CalculateHistogram(channel, bins);
        double width = (h.UpperBound - h.LowerBound) / h.Bins.Count;
        return new(h.Bins.Select((count, i) => new HistogramEntry(i, h.LowerBound + i * width, h.LowerBound + (i + 1) * width, count)).ToArray(),
            $"Range {h.LowerBound:G8} to {h.UpperBound:G8}; underflow {h.UnderflowCount:N0}; overflow {h.OverflowCount:N0}; non-finite {h.NonFiniteCount:N0}.");
    }
    public static string SamplePreview(IReadOnlyList<double> values, string description) =>
        $"{description}: {values.Count:N0} value(s). First {Math.Min(values.Count, 64)}: {string.Join(", ", values.Take(64).Select(v => v.ToString("G8", CultureInfo.InvariantCulture)))}{(values.Count > 64 ? ", …" : "")}";
    public static string WcsSummary(CelestialWorldCoordinateSystem? wcs) => wcs is null
        ? "No supported celestial WCS was found in the selected image's FITS keywords."
        : $"Pixel scale {wcs.PixelScaleArcseconds:N4} arcsec/pixel; rotation {wcs.RotationDegrees:N4}°; {(wcs.IsTangentPlane ? "TAN projection" : "linear projection")}.";
    public static int Positive(string text, string name)
    {
        int value = int.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
        return value > 0 ? value : throw new ArgumentOutOfRangeException(name, $"{name} must be positive.");
    }
    public static int Nonnegative(string text, string name)
    {
        int value = int.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
        return value >= 0 ? value : throw new ArgumentOutOfRangeException(name, $"{name} must not be negative.");
    }
    public static double Number(string text) => double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
    private static string Optional(double? value, string suffix = "") => value is null ? "Not available" : value.Value.ToString("G10", CultureInfo.CurrentCulture) + suffix;
}
public sealed record UtilityEntry(string Name, string? RawValue)
{
    public string Value => string.IsNullOrWhiteSpace(RawValue) ? "Not available" : RawValue;
}
public sealed record HistogramEntry(int Index, double LowerEdge, double UpperEdge, long Count);
public sealed record HistogramReport(IReadOnlyList<HistogramEntry> Rows, string Summary);
