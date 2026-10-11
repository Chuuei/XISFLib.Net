using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XISFLib.Viewer;

/// <summary>Novice finishing workflow with one explicit reviewable preview and serialized export.</summary>
public sealed class ViewerFinishState : ObservableObject
{
    private readonly Func<XisfImageData?> _image;
    private readonly Func<string, Func<Task>, Task> _run;
    private readonly Func<bool> _canRun, _canPublish;
    private FinishedPicture? _picture;
    private string _summary = "Create a finished picture to preview a darker sky, automatic background cleanup and faint detail.";
    private int _revision;
    private CancellationTokenSource? _saveCancellation;
    private bool _processing, _indeterminate = true, _cancelRequested;
    private double _progressValue;
    private string _progressText = "";
    private int _operation;
    private bool _sharpenStars = true, _reduceStarSpikes = true;

    public ViewerFinishState(Func<XisfImageData?> image, Func<bool> isRasterImport, Func<string, Func<Task>, Task> run, Func<bool> canRun, Func<bool> canPublish)
    {
        _image = image; _run = run; _canRun = canRun; _canPublish = canPublish;
        CreateCommand = new AsyncRelayCommand(token => run("Creating a finished image…", async () =>
        {
            var current = image()!; int revision = _revision; bool raster = isRasterImport();
            var options = new FinishOptions(SharpenStars, ReduceStarSpikes);
            var progress = BeginOperation("Preparing finished image…");
            try
            {
                var picture = await Task.Run(() => ViewerFinishService.Create(current, raster, token, progress, options), token);
                token.ThrowIfCancellationRequested();
                if (!canPublish() || revision != _revision || !ReferenceEquals(current, image())) return;
                ProgressText = "Displaying finished preview…"; IsIndeterminate = true;
                Present(picture);
                _picture = picture; Summary = picture.Summary;
                OnPropertyChanged(nameof(HasPicture)); OnPropertyChanged(nameof(CanSave));
            }
            finally { EndOperation(); }
        }), () => image() is not null && canRun());
        CancelCommand = new RelayCommand(() =>
        {
            _cancelRequested = true; ProgressText = "Cancelling… Waiting for the current processing step to stop.";
            IsIndeterminate = true;
            CreateCommand.Cancel(); _saveCancellation?.Cancel();
        }, () => IsProcessing);
    }

    /// <summary>UI-thread preview publication callback. The frontend owns and disposes its native bitmap.</summary>
    public Action<FinishedPicture?> Present { get; set; } = _ => { };
    public IAsyncRelayCommand CreateCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public bool HasPicture => _picture is not null;
    public bool CanSave => HasPicture && !IsProcessing && _canRun() && _canPublish();
    public string Summary { get => _summary; private set => SetProperty(ref _summary, value); }
    public bool SharpenStars { get => _sharpenStars; set { if (SetProperty(ref _sharpenStars, value)) Refresh(); } }
    public bool ReduceStarSpikes { get => _reduceStarSpikes; set { if (SetProperty(ref _reduceStarSpikes, value)) Refresh(); } }
    public bool IsProcessing
    {
        get => _processing;
        private set
        {
            if (_processing == value) return;
            _processing = value;
            CancelCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(IsProcessing)); OnPropertyChanged(nameof(CanSave));
        }
    }
    public bool IsIndeterminate { get => _indeterminate; private set => SetProperty(ref _indeterminate, value); }
    public double ProgressValue { get => _progressValue; private set => SetProperty(ref _progressValue, value); }
    public string ProgressText { get => _progressText; private set => SetProperty(ref _progressText, value); }

    private IProgress<FinishProgress> BeginOperation(string text)
    {
        int operation = ++_operation;
        _cancelRequested = false; ProgressText = text; ProgressValue = 0; IsIndeterminate = true; IsProcessing = true;
        // Progress captures the caller's UI context. Suppress callbacks queued after completion, cancellation or closure.
        return new Progress<FinishProgress>(update =>
        {
            if (operation != _operation || !IsProcessing || !_canPublish() || _cancelRequested) return;
            ProgressText = update.Stage; IsIndeterminate = update.Percentage is null;
            ProgressValue = update.Percentage ?? 0;
        });
    }
    private void EndOperation() { ++_operation; IsProcessing = false; }

    public Task SaveAsync(string path, string? sourcePath) => _run("Saving finished image…", async () =>
    {
        var picture = _picture ?? throw new InvalidOperationException("Create a finished image first.");
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (sourcePath is not null && string.Equals(Path.GetFullPath(path), Path.GetFullPath(sourcePath), comparison))
            throw new InvalidOperationException("Choose a different filename to keep the original image intact.");
        using var cancellation = new CancellationTokenSource();
        _saveCancellation = cancellation;
        var progress = BeginOperation(Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase) ? "Preparing PNG export…" : "Preparing JPEG export…");
        try { await Task.Run(() => ViewerFinishService.Save(path, picture, cancellation.Token, progress)); }
        finally { _saveCancellation = null; EndOperation(); }
    });

    public void NotifyCommands()
    {
        CreateCommand.NotifyCanExecuteChanged(); OnPropertyChanged(nameof(CanSave));
    }
    public void Refresh()
    {
        _revision++; CreateCommand.Cancel(); _saveCancellation?.Cancel(); _picture = null;
        EndOperation();
        Present(null);
        Summary = "Create a finished picture to preview a darker sky, automatic background cleanup and faint detail.";
        OnPropertyChanged(nameof(HasPicture)); NotifyCommands();
    }
}
