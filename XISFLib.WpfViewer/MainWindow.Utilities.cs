using System.Globalization;
using System.Windows;
using XISFLib.Viewer;

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

        AstronomyMetadataGrid.ItemsSource = ViewerUtilityService.Overview(_editableImage);
        HistogramChannelComboBox.ItemsSource = Enumerable.Range(0, _editableImage.ChannelCount).ToArray();
        HistogramChannelComboBox.SelectedIndex = 0;
        int width = _editableImage.Dimensions[0];
        int height = _editableImage.Dimensions.Count > 1 ? _editableImage.Dimensions[1] : 1;
        RegionWidthTextBox.Text = Math.Min(width, 16).ToString(CultureInfo.InvariantCulture);
        RegionHeightTextBox.Text = Math.Min(height, 16).ToString(CultureInfo.InvariantCulture);
        WcsXTextBox.Text = ((width - 1) / 2d).ToString("R", CultureInfo.InvariantCulture);
        WcsYTextBox.Text = ((height - 1) / 2d).ToString("R", CultureInfo.InvariantCulture);

        _worldCoordinateSystem = CelestialWorldCoordinateSystem.TryCreate(_editableImage.FitsKeywords, out CelestialWorldCoordinateSystem? wcs) ? wcs : null;
        WcsSummaryText.Text = ViewerUtilityService.WcsSummary(_worldCoordinateSystem);
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
            IReadOnlyList<XisfChannelStatistics> statistics = await Task.Run(() => ViewerUtilityService.Statistics(image, maximumSamples, normalize));
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
            var histogram = await Task.Run(() => ViewerUtilityService.Histogram(_editableImage, channel, bins));
            HistogramGrid.ItemsSource = histogram.Rows;
            HistogramSummaryText.Text = histogram.Summary;
            StatusText.Text = $"Calculated a {bins:N0}-bin histogram for channel {channel}.";
        });
    }

    private async void ReadRegionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_editableImage is null) { ShowError("Load or import an image first."); return; }
        await RunBusyAsync("Reading image region…", async () =>
        {
            int x = ParseNonnegativeInt(RegionXTextBox.Text, "X");
            int y = ParseNonnegativeInt(RegionYTextBox.Text, "Y");
            int width = ParsePositiveInt(RegionWidthTextBox.Text, "Width");
            int height = ParsePositiveInt(RegionHeightTextBox.Text, "Height");
            double[] values = await Task.Run(() => _editableImage.ReadRegion<double>(x, y, width, height, XisfPixelLayout.Interleaved));
            ShowSampleValues(values, $"Region {x},{y} {width}×{height}, interleaved");
        });
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
        SampleOutputTextBox.Text = ViewerUtilityService.SamplePreview(values, description);
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
