using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XISFLib.Viewer;

/// <summary>Initial portable loading/preview state; editor and writer commands are migrated in later phases.</summary>
public sealed class WorkbenchViewModel : ObservableObject
{
    private readonly Func<XisfBgra32Image?, Task> _present;
    private readonly Func<Task> _yieldPresentation;
    private ViewerSession? _session;
    private int _generation;
    private bool _closed;
    private string _filePath = "";
    private string _status = "Open an XISF or FITS file to begin.";
    private string _details = "";
    private bool _busy;
    private int _selectedIndex = -1;
    private int _presentedIndex = -1;
    private bool _autoStretch = true;
    private bool _linkChannels = true;
    private string _target = "0.25";
    private string _clipping = "-2.8";

    public WorkbenchViewModel(Func<XisfBgra32Image?, Task> present, Func<Task>? yieldPresentation = null)
    {
        _present = present;
        _yieldPresentation = yieldPresentation ?? (() => Task.CompletedTask);
        LoadCommand = new AsyncRelayCommand(LoadAsync, () => !IsBusy && !_closed);
        UnloadCommand = new AsyncRelayCommand(UnloadAsync, () => !IsBusy && !_closed);
        ApplyCommand = new AsyncRelayCommand(RenderAsync, () => !IsBusy && !_closed && _session is not null);
        SelectImageCommand = new AsyncRelayCommand(SelectImageAsync, () => !IsBusy && !_closed && _session is not null);
    }

    public string FilePath { get => _filePath; set => SetProperty(ref _filePath, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string ImageDetails { get => _details; private set => SetProperty(ref _details, value); }
    public bool IsBusy
    {
        get => _busy;
        private set
        {
            if (!SetProperty(ref _busy, value)) return;
            OnPropertyChanged(nameof(CanInteract));
            LoadCommand.NotifyCanExecuteChanged(); UnloadCommand.NotifyCanExecuteChanged();
            ApplyCommand.NotifyCanExecuteChanged(); SelectImageCommand.NotifyCanExecuteChanged();
        }
    }
    public bool CanInteract => !IsBusy && !_closed;
    public int SelectedImageIndex { get => _selectedIndex; set => SetProperty(ref _selectedIndex, value); }
    public bool AutoStretch { get => _autoStretch; set => SetProperty(ref _autoStretch, value); }
    public bool LinkChannels { get => _linkChannels; set => SetProperty(ref _linkChannels, value); }
    public string TargetBackground { get => _target; set => SetProperty(ref _target, value); }
    public string ShadowsClipping { get => _clipping; set => SetProperty(ref _clipping, value); }
    public ObservableCollection<string> ImageLabels { get; } = [];
    public ObservableCollection<ViewerDiagnostic> Diagnostics { get; } = [];
    public IAsyncRelayCommand LoadCommand { get; }
    public IAsyncRelayCommand UnloadCommand { get; }
    public IAsyncRelayCommand ApplyCommand { get; }
    public IAsyncRelayCommand SelectImageCommand { get; }
    public bool ResetViewportOnPresentation { get; private set; }

    public void Close() { _closed = true; _generation++; }

    private Task LoadAsync() => RunAsync("Loading astronomy image…", async generation =>
    {
        var path = FilePath.Trim();
        var stretch = CreateStretch();
        var session = await Task.Run(() => ViewerSession.Load(path));
        // A failed load must not replace the previously displayed session.
        var rendered = await Task.Run(() => XisfDisplayRenderer.Render(session.GetImage(0), stretch));
        if (!IsCurrent(generation)) return;
        ResetViewportOnPresentation = true;
        await _present(rendered);
        if (!IsCurrent(generation)) return;
        _presentedIndex = 0;
        _session = session;
        ImageLabels.Clear();
        for (int i = 0; i < session.Images.Count; i++) ImageLabels.Add(session.GetImageLabel(i));
        SelectedImageIndex = 0;
        SetDetails(session.GetImage(0));
        Status = "Image ready. Preparing validation…";
        await _yieldPresentation();
        var findings = await Task.Run(() => session.Xisf is { } xisf
            ? xisf.Validate().Select(d => new ViewerDiagnostic(d.Severity.ToString(), d.Code, d.Message)).ToArray()
            : session.Fits!.Diagnostics.Select(d => new ViewerDiagnostic(d.Severity.ToString(), d.Code, d.Message)).ToArray());
        if (!IsCurrent(generation)) return;
        Diagnostics.Clear();
        foreach (var diagnostic in findings) Diagnostics.Add(diagnostic);
        Status = $"Loaded {Path.GetFileName(path)} ({session.Images.Count} image(s){(session.Fits is null ? "" : ", converted from FITS")}).";
    });

    private Task SelectImageAsync() => RenderAsync();

    private Task RenderAsync() => RunAsync("Rendering image…", async generation =>
    {
        if (_session is not { } session || SelectedImageIndex < 0) return;
        int index = SelectedImageIndex;
        var stretch = CreateStretch();
        var result = await Task.Run(() =>
        {
            var image = session.GetImage(index);
            return (Image: image, Rendered: XisfDisplayRenderer.Render(image, stretch));
        });
        if (!IsCurrent(generation)) return;
        ResetViewportOnPresentation = index != _presentedIndex;
        await _present(result.Rendered);
        if (!IsCurrent(generation)) return;
        _presentedIndex = index;
        SetDetails(result.Image);
        Status = "Image ready.";
    });

    private async Task UnloadAsync()
    {
        if (!CanInteract) return;
        _generation++;
        _session = null;
        ImageLabels.Clear(); Diagnostics.Clear(); SelectedImageIndex = -1;
        _presentedIndex = -1; ResetViewportOnPresentation = true;
        FilePath = ""; ImageDetails = ""; Status = "No image loaded.";
        await _present(null);
        ApplyCommand.NotifyCanExecuteChanged(); SelectImageCommand.NotifyCanExecuteChanged();
    }

    private async Task RunAsync(string status, Func<int, Task> action)
    {
        if (!CanInteract) return;
        int generation = ++_generation;
        IsBusy = true; Status = status;
        try { await action(generation); }
        catch (Exception exception) { if (IsCurrent(generation)) Status = exception.Message; }
        finally { IsBusy = false; }
    }

    private bool IsCurrent(int generation) => !_closed && generation == _generation;
    private void SetDetails(XisfImageData image) => ImageDetails =
        $"{string.Join(" × ", image.Dimensions)}, {image.ChannelCount} channel(s), {image.SampleFormat}, {image.ColorSpace ?? "unspecified color space"}";

    private XisfStretchOptions? CreateStretch()
    {
        if (!AutoStretch) return null;
        return new() { TargetBackground = ParseNumber(TargetBackground), ShadowsClipping = ParseNumber(ShadowsClipping), LinkChannels = LinkChannels };
    }

    private static double ParseNumber(string text)
    {
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) ||
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return value;
        throw new FormatException($"'{text}' is not a valid number.");
    }
}

public sealed record ViewerDiagnostic(string Severity, string Code, string Message);
