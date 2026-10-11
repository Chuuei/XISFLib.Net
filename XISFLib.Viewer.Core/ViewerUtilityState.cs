using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XISFLib.Viewer;

/// <summary>Utility controls and results shared with the portable workbench.</summary>
public sealed class ViewerUtilityState : ObservableObject
{
    private readonly Func<XisfImageData?> _image;
    private CelestialWorldCoordinateSystem? _wcs;
    private IReadOnlyList<UtilityEntry> _overview = [];
    private IReadOnlyList<XisfChannelStatistics> _statistics = [];
    private IReadOnlyList<HistogramEntry> _histogram = [];
    private string _histogramSummary = "", _samples = "", _wcsSummary = "Load an image to inspect its WCS.", _pixelResult = "", _skyResult = "";
    private string _x = "0", _y = "0", _ra = "", _dec = "", _width = "16", _height = "16";
    private IReadOnlyList<int> _channels = [];

    public ViewerUtilityState(Func<XisfImageData?> image, Func<string, Func<Task>, Task> run, Func<bool>? canRun = null)
    {
        _image = image;
        bool CanRun() => _image() is not null && (canRun?.Invoke() ?? true);
        StatisticsCommand = new AsyncRelayCommand(() => run("Calculating channel statistics…", async () =>
        {
            var current = _image()!; var limit = ViewerUtilityService.Positive(SampleLimit, "Maximum samples"); var normalize = Normalize;
            var result = await Task.Run(() => ViewerUtilityService.Statistics(current, limit, normalize));
            if (IsCurrent(current)) Statistics = result;
        }), CanRun);
        HistogramCommand = new AsyncRelayCommand(() => run("Calculating histogram…", async () =>
        {
            var current = _image()!; int channel = Channel, bins = ViewerUtilityService.Positive(Bins, "Histogram bins");
            var result = await Task.Run(() => ViewerUtilityService.Histogram(current, channel, bins));
            if (!IsCurrent(current)) return;
            Histogram = result.Rows; HistogramSummary = result.Summary;
        }), CanRun);
        RegionCommand = new AsyncRelayCommand(() => run("Reading image region…", async () =>
        {
            var current = _image()!; int x = ViewerUtilityService.Nonnegative(RegionX, "X"), y = ViewerUtilityService.Nonnegative(RegionY, "Y"),
                width = ViewerUtilityService.Positive(RegionWidth, "Width"), height = ViewerUtilityService.Positive(RegionHeight, "Height");
            var values = await Task.Run(() => current.ReadRegion<double>(x, y, width, height, XisfPixelLayout.Interleaved));
            if (!IsCurrent(current)) return;
            Samples = ViewerUtilityService.SamplePreview(values, $"Region {x},{y} {width}×{height}, interleaved");
        }), CanRun);
        ChannelCommand = new AsyncRelayCommand(() => run("Reading image channel…", async () =>
        {
            var current = _image()!; int channel = Channel;
            var values = await Task.Run(() => current.ReadChannel<double>(channel));
            if (!IsCurrent(current)) return;
            Samples = ViewerUtilityService.SamplePreview(values, $"Complete channel {channel}");
        }), CanRun);
        PixelToSkyCommand = new AsyncRelayCommand(() => run("Converting pixel coordinates…", () =>
        {
            var (ra, dec) = _wcs!.PixelToSky(ViewerUtilityService.Number(PixelX), ViewerUtilityService.Number(PixelY));
            RightAscension = ra.ToString("R", CultureInfo.InvariantCulture); Declination = dec.ToString("R", CultureInfo.InvariantCulture);
            PixelResult = $"RA {ra:F8}°, Dec {dec:F8}°"; return Task.CompletedTask;
        }), () => CanRun() && _wcs is not null);
        SkyToPixelCommand = new AsyncRelayCommand(() => run("Converting sky coordinates…", () =>
        {
            var (x, y) = _wcs!.SkyToPixel(ViewerUtilityService.Number(RightAscension), ViewerUtilityService.Number(Declination));
            PixelX = x.ToString("R", CultureInfo.InvariantCulture); PixelY = y.ToString("R", CultureInfo.InvariantCulture);
            SkyResult = $"X {x:F4}, Y {y:F4}"; return Task.CompletedTask;
        }), () => CanRun() && _wcs is not null);
    }
    public string SampleLimit { get; set; } = "500000";
    public Func<bool> CanPublish { get; init; } = () => true;
    private bool IsCurrent(XisfImageData image) => CanPublish() && ReferenceEquals(_image(), image);
    public bool Normalize { get; set; }
    public string Bins { get; set; } = "256";
    public int Channel { get; set; }
    public string RegionX { get; set; } = "0";
    public string RegionY { get; set; } = "0";
    public string RegionWidth { get => _width; set => SetProperty(ref _width, value); }
    public string RegionHeight { get => _height; set => SetProperty(ref _height, value); }
    public string PixelX { get => _x; set => SetProperty(ref _x, value); }
    public string PixelY { get => _y; set => SetProperty(ref _y, value); }
    public string RightAscension { get => _ra; set => SetProperty(ref _ra, value); }
    public string Declination { get => _dec; set => SetProperty(ref _dec, value); }
    public IReadOnlyList<int> Channels { get => _channels; private set => SetProperty(ref _channels, value); }
    public IReadOnlyList<UtilityEntry> Overview { get => _overview; private set => SetProperty(ref _overview, value); }
    public IReadOnlyList<XisfChannelStatistics> Statistics { get => _statistics; private set => SetProperty(ref _statistics, value); }
    public IReadOnlyList<HistogramEntry> Histogram { get => _histogram; private set => SetProperty(ref _histogram, value); }
    public string HistogramSummary { get => _histogramSummary; private set => SetProperty(ref _histogramSummary, value); }
    public string Samples { get => _samples; private set => SetProperty(ref _samples, value); }
    public string WcsSummary { get => _wcsSummary; private set => SetProperty(ref _wcsSummary, value); }
    public string PixelResult { get => _pixelResult; private set => SetProperty(ref _pixelResult, value); }
    public string SkyResult { get => _skyResult; private set => SetProperty(ref _skyResult, value); }
    public IAsyncRelayCommand StatisticsCommand { get; }
    public IAsyncRelayCommand HistogramCommand { get; }
    public IAsyncRelayCommand RegionCommand { get; }
    public IAsyncRelayCommand ChannelCommand { get; }
    public IAsyncRelayCommand PixelToSkyCommand { get; }
    public IAsyncRelayCommand SkyToPixelCommand { get; }
    public void NotifyCommands()
    {
        foreach (var command in new[] { StatisticsCommand, HistogramCommand, RegionCommand, ChannelCommand, PixelToSkyCommand, SkyToPixelCommand }) command.NotifyCanExecuteChanged();
    }
    public void Refresh()
    {
        var image = _image(); _wcs = null;
        if (image is null) { Overview = []; Channels = []; WcsSummary = "Load an image to inspect its WCS."; }
        else
        {
            Overview = ViewerUtilityService.Overview(image); Channels = Enumerable.Range(0, image.ChannelCount).ToArray(); Channel = 0;
            OnPropertyChanged(nameof(Channel));
            int width = image.Dimensions[0], height = image.Dimensions.Count > 1 ? image.Dimensions[1] : 1;
            RegionWidth = Math.Min(width, 16).ToString(CultureInfo.InvariantCulture); RegionHeight = Math.Min(height, 16).ToString(CultureInfo.InvariantCulture);
            PixelX = ((width - 1) / 2d).ToString("R", CultureInfo.InvariantCulture); PixelY = ((height - 1) / 2d).ToString("R", CultureInfo.InvariantCulture);
            _wcs = CelestialWorldCoordinateSystem.TryCreate(image.FitsKeywords, out var wcs) ? wcs : null;
            WcsSummary = ViewerUtilityService.WcsSummary(_wcs);
        }
        Statistics = []; Histogram = []; HistogramSummary = Samples = PixelResult = SkyResult = ""; NotifyCommands();
    }
}
