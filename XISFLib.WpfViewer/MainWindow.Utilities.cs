using System.Globalization;
using System.Windows;

namespace XISFLib.WpfViewer;

public partial class MainWindow
{
    private CelestialWorldCoordinateSystem? _worldCoordinateSystem;

    private void RefreshUtilityOverview()
    {
        if (_editableImage is null)
        {
            ClearUtilityResults();
            return;
        }

        AstronomyImageMetadata metadata = AstronomyMetadata.From(_editableImage);
        AstronomyMetadataGrid.ItemsSource = new[]
        {
            new UtilityValue("Object", metadata.ObjectName),
            new UtilityValue("Observer", metadata.Observer),
            new UtilityValue("Instrument", metadata.Instrument),
            new UtilityValue("Telescope", metadata.Telescope),
            new UtilityValue("Filter", metadata.Filter),
            new UtilityValue("Exposure", FormatOptional(metadata.ExposureSeconds, " s")),
            new UtilityValue("Gain", FormatOptional(metadata.Gain)),
            new UtilityValue("Offset", FormatOptional(metadata.Offset)),
            new UtilityValue("Binning", metadata.BinningX is null && metadata.BinningY is null ? null : $"{metadata.BinningX?.ToString(CultureInfo.CurrentCulture) ?? "?"} × {metadata.BinningY?.ToString(CultureInfo.CurrentCulture) ?? "?"}"),
            new UtilityValue("Sensor temperature", FormatOptional(metadata.SensorTemperatureCelsius, " °C")),
            new UtilityValue("Observation time", metadata.ObservationTime?.ToString("u", CultureInfo.CurrentCulture)),
            new UtilityValue("Recognized/raw values", metadata.Values.Count.ToString(CultureInfo.CurrentCulture))
        };

        HistogramChannelComboBox.ItemsSource = Enumerable.Range(0, _editableImage.ChannelCount).ToArray();
        HistogramChannelComboBox.SelectedIndex = 0;
        int width = _editableImage.Dimensions[0];
        int height = _editableImage.Dimensions.Count > 1 ? _editableImage.Dimensions[1] : 1;
        RegionWidthTextBox.Text = Math.Min(width, 16).ToString(CultureInfo.InvariantCulture);
        RegionHeightTextBox.Text = Math.Min(height, 16).ToString(CultureInfo.InvariantCulture);
        WcsXTextBox.Text = ((width - 1) / 2d).ToString("R", CultureInfo.InvariantCulture);
        WcsYTextBox.Text = ((height - 1) / 2d).ToString("R", CultureInfo.InvariantCulture);

        _worldCoordinateSystem = CelestialWorldCoordinateSystem.TryCreate(_editableImage.FitsKeywords, out CelestialWorldCoordinateSystem? wcs) ? wcs : null;
        WcsSummaryText.Text = _worldCoordinateSystem is null
            ? "No supported celestial WCS was found in the selected image's FITS keywords."
            : $"Pixel scale {_worldCoordinateSystem.PixelScaleArcseconds:N4} arcsec/pixel; rotation {_worldCoordinateSystem.RotationDegrees:N4}°; {(_worldCoordinateSystem.IsTangentPlane ? "TAN projection" : "linear projection")}.";
        PixelToSkyButton.IsEnabled = _worldCoordinateSystem is not null;
        SkyToPixelButton.IsEnabled = _worldCoordinateSystem is not null;
        StatisticsGrid.ItemsSource = null;
        HistogramGrid.ItemsSource = null;
        HistogramSummaryText.Text = string.Empty;
        SampleOutputTextBox.Clear();
        PixelToSkyResultText.Text = string.Empty;
        SkyToPixelResultText.Text = string.Empty;
    }

    private void ClearUtilityResults()
    {
        _worldCoordinateSystem = null;
        AstronomyMetadataGrid.ItemsSource = null;
        StatisticsGrid.ItemsSource = null;
        HistogramChannelComboBox.ItemsSource = null;
        HistogramGrid.ItemsSource = null;
        HistogramSummaryText.Text = string.Empty;
        SampleOutputTextBox.Clear();
        WcsSummaryText.Text = "Load an image to inspect its WCS.";
        PixelToSkyResultText.Text = string.Empty;
        SkyToPixelResultText.Text = string.Empty;
        PixelToSkyButton.IsEnabled = false;
        SkyToPixelButton.IsEnabled = false;
    }

    private async void CalculateStatisticsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_editableImage is not { } image) { ShowError("Load or import an image first."); return; }
        await RunBusyAsync("Calculating channel statistics…", async () =>
        {
            int maximumSamples = ParsePositiveInt(StatisticsSampleLimitTextBox.Text, "Maximum samples");
            bool normalize = NormalizeStatisticsCheckBox.IsChecked == true;
            XisfStatisticsOptions options = new()
            {
                Normalize = normalize,
                MaximumSamplesPerChannel = maximumSamples
            };
            IReadOnlyList<XisfChannelStatistics> statistics = await Task.Run(() => image.CalculateStatistics(options));
            StatisticsGrid.ItemsSource = statistics;
            StatusText.Text = $"Calculated statistics for {statistics.Count} channel(s).";
        });
    }

    private async void CalculateHistogramButton_Click(object sender, RoutedEventArgs e)
    {
        if (_editableImage is null) { ShowError("Load or import an image first."); return; }
        await RunBusyAsync("Calculating histogram…", async () =>
        {
            int channel = HistogramChannelComboBox.SelectedItem is int selected ? selected : 0;
            int bins = ParsePositiveInt(HistogramBinCountTextBox.Text, "Histogram bins");
            if (bins > 65_536) throw new ArgumentOutOfRangeException(nameof(bins), "Histogram bins must not exceed 65,536.");
            XisfHistogram histogram = await Task.Run(() => _editableImage.CalculateHistogram(channel, bins));
            double width = (histogram.UpperBound - histogram.LowerBound) / histogram.Bins.Count;
            HistogramGrid.ItemsSource = histogram.Bins.Select((count, index) => new HistogramBinRow(index,
                histogram.LowerBound + index * width, histogram.LowerBound + (index + 1) * width, count)).ToArray();
            HistogramSummaryText.Text = $"Range {histogram.LowerBound:G8} to {histogram.UpperBound:G8}; underflow {histogram.UnderflowCount:N0}; overflow {histogram.OverflowCount:N0}; non-finite {histogram.NonFiniteCount:N0}.";
            StatusText.Text = $"Calculated a {bins:N0}-bin histogram for channel {channel}.";
        });
    }

    private void ReadRegionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_editableImage is null) { ShowError("Load or import an image first."); return; }
        try
        {
            int x = ParseNonnegativeInt(RegionXTextBox.Text, "X");
            int y = ParseNonnegativeInt(RegionYTextBox.Text, "Y");
            int width = ParsePositiveInt(RegionWidthTextBox.Text, "Width");
            int height = ParsePositiveInt(RegionHeightTextBox.Text, "Height");
            double[] values = _editableImage.ReadRegion<double>(x, y, width, height, XisfPixelLayout.Interleaved);
            ShowSampleValues(values, $"Region {x},{y} {width}×{height}, interleaved");
        }
        catch (Exception exception) { ShowError(exception.Message); }
    }

    private async void ReadChannelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_editableImage is null) { ShowError("Load or import an image first."); return; }
        await RunBusyAsync("Reading image channel…", async () =>
        {
            int channel = HistogramChannelComboBox.SelectedItem is int selected ? selected : 0;
            double[] values = await Task.Run(() => _editableImage.ReadChannel<double>(channel));
            ShowSampleValues(values, $"Complete channel {channel}");
        });
    }

    private void ShowSampleValues(IReadOnlyList<double> values, string description)
    {
        string preview = string.Join(", ", values.Take(64).Select(value => value.ToString("G8", CultureInfo.InvariantCulture)));
        SampleOutputTextBox.Text = $"{description}: {values.Count:N0} value(s). First {Math.Min(values.Count, 64)}: {preview}{(values.Count > 64 ? ", …" : string.Empty)}";
        StatusText.Text = $"Read {values.Count:N0} samples with the pixel access API.";
    }

    private void PixelToSkyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_worldCoordinateSystem is null) return;
        try
        {
            (double ra, double dec) = _worldCoordinateSystem.PixelToSky(ParseNumber(WcsXTextBox.Text, "Pixel X"), ParseNumber(WcsYTextBox.Text, "Pixel Y"));
            WcsRaTextBox.Text = ra.ToString("R", CultureInfo.InvariantCulture);
            WcsDecTextBox.Text = dec.ToString("R", CultureInfo.InvariantCulture);
            PixelToSkyResultText.Text = $"RA {ra:F8}°, Dec {dec:F8}°";
        }
        catch (Exception exception) { ShowError(exception.Message); }
    }

    private void SkyToPixelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_worldCoordinateSystem is null) return;
        try
        {
            (double x, double y) = _worldCoordinateSystem.SkyToPixel(ParseNumber(WcsRaTextBox.Text, "Right ascension"), ParseNumber(WcsDecTextBox.Text, "Declination"));
            WcsXTextBox.Text = x.ToString("R", CultureInfo.InvariantCulture);
            WcsYTextBox.Text = y.ToString("R", CultureInfo.InvariantCulture);
            SkyToPixelResultText.Text = $"X {x:F4}, Y {y:F4}";
        }
        catch (Exception exception) { ShowError(exception.Message); }
    }

    private static int ParsePositiveInt(string text, string name)
    {
        int value = int.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
        return value > 0 ? value : throw new ArgumentOutOfRangeException(name, $"{name} must be positive.");
    }

    private static int ParseNonnegativeInt(string text, string name)
    {
        int value = int.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
        return value >= 0 ? value : throw new ArgumentOutOfRangeException(name, $"{name} must not be negative.");
    }

    private static string FormatOptional(double? value, string suffix = "") =>
        value is null ? "Not available" : value.Value.ToString("G10", CultureInfo.CurrentCulture) + suffix;
}

public sealed record UtilityValue(string Name, string? RawValue)
{
    public string Value => string.IsNullOrWhiteSpace(RawValue) ? "Not available" : RawValue;
}

public sealed record HistogramBinRow(int Index, double LowerEdge, double UpperEdge, long Count);