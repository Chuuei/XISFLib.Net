using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XISFLib.Viewer;

/// <summary>On-demand, cancellable image quality analysis serialized with workbench operations.</summary>
public sealed class ViewerQualityState : ObservableObject
{
    private IReadOnlyList<QualityEntry> _entries = [];
    private IReadOnlyList<XisfChannelStatistics> _statistics = [];
    private IReadOnlyList<int> _channels = [];
    private string _notes = "Load an image, choose a channel and select Analyze image. Results use original, unstretched samples.";
    private string _threshold = "5";
    private int _channel;
    private readonly Func<XisfImageData?> _image;

    public ViewerQualityState(Func<XisfImageData?> image, Func<string, Func<Task>, Task> run, Func<bool> canRun, Func<bool> canPublish)
    {
        _image = image;
        AnalyzeCommand = new AsyncRelayCommand(async cancellationToken =>
            await run("Analyzing image quality…", async () =>
            {
                Clear();
                var current = image()!;
                int channel = Channel;
                string thresholdText = DetectionThreshold;
                double threshold = ViewerUtilityService.Number(thresholdText);
                var report = await Task.Run(() => ViewerQualityService.Analyze(current, channel, threshold, cancellationToken), cancellationToken);
                if (!cancellationToken.IsCancellationRequested && canPublish() && ReferenceEquals(current, image())
                    && channel == Channel && thresholdText == DetectionThreshold)
                {
                    Entries = report.Entries; Statistics = report.Channels; Notes = report.Notes;
                }
            }), () => image() is not null && canRun());
        CancelCommand = new RelayCommand(() => AnalyzeCommand.Cancel());
    }

    public IAsyncRelayCommand AnalyzeCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IReadOnlyList<int> Channels { get => _channels; private set => SetProperty(ref _channels, value); }
    public int Channel { get => _channel; set { if (SetProperty(ref _channel, value)) Clear(); } }
    public string DetectionThreshold { get => _threshold; set { if (SetProperty(ref _threshold, value)) Clear(); } }
    public IReadOnlyList<QualityEntry> Entries { get => _entries; private set => SetProperty(ref _entries, value); }
    public IReadOnlyList<XisfChannelStatistics> Statistics { get => _statistics; private set => SetProperty(ref _statistics, value); }
    public string Notes { get => _notes; private set => SetProperty(ref _notes, value); }

    public void Refresh()
    {
        AnalyzeCommand.Cancel();
        var image = _image();
        Channels = image is null ? [] : Enumerable.Range(0, image.ChannelCount).ToArray();
        Channel = image?.ColorSpace == "RGB" && image.ChannelCount >= 3 ? 1 : 0;
        Clear();
        AnalyzeCommand.NotifyCanExecuteChanged();
    }

    private void Clear()
    {
        Entries = []; Statistics = [];
        Notes = "Choose a channel and select Analyze image. Values use stored sample units. RGB defaults to green; alpha is not selected automatically. Detection and shape measurements are estimates.";
    }
}
