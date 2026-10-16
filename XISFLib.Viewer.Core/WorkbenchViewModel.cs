using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XISFLib.Viewer;

/// <summary>Desktop workbench state and orchestration independent of native Avalonia controls.</summary>
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
    private IReadOnlyList<MetadataNode> _metadata = [];
    private ImageEditorFields _imageEditor = new();
    private TableEditorFields _tableEditor = new();
    private bool _busy;
    private int _selectedIndex = -1;
    private int _presentedIndex = -1;
    private bool _autoStretch = true;
    private bool _linkChannels = true;
    private string _target = "0.25";
    private string _clipping = "-2.8";
    private byte[]? _iccProfile;
    private XisfImageData? _thumbnail;
    private bool _canCopyOriginal;
    private bool _preserveOriginal;
    private string? _bayerPattern;
    private string _bayerDescription = "Load a Bayer mosaic to debayer it.";

    public WorkbenchViewModel(Func<XisfBgra32Image?, Task> present, Func<Task>? yieldPresentation = null)
    {
        _present = present;
        _yieldPresentation = yieldPresentation ?? (() => Task.CompletedTask);
        LoadCommand = new AsyncRelayCommand(LoadAsync, () => !IsBusy && !_closed);
        UnloadCommand = new AsyncRelayCommand(UnloadAsync, () => !IsBusy && !_closed);
        ApplyCommand = new AsyncRelayCommand(RenderAsync, () => !IsBusy && !_closed && _session is not null);
        SelectImageCommand = new AsyncRelayCommand(SelectImageAsync, () => !IsBusy && !_closed && _session is not null);
        ValidateCommand = new AsyncRelayCommand(ValidateAsync, () => !IsBusy && !_closed && _session is not null);
        ApplyMetadataCommand = new AsyncRelayCommand(ApplyMetadataAsync, () => CanInteract && SelectedImage is not null);
        ApplyDocumentCommand = new AsyncRelayCommand(ApplyDocumentAsync, () => CanEditMetadata);
        DebayerCommand = new AsyncRelayCommand(DebayerAsync, () => CanDebayer && BayerPattern is not null);
        CancelDebayerCommand = DebayerCommand.CreateCancelCommand();
        Utilities = new(() => SelectedImage, (status, action) => RunAsync(status, _ => action()), () => CanEditMetadata) { CanPublish = () => !_closed };
        Quality = new(() => SelectedImage, (status, action) => RunAsync(status, async generation =>
        {
            await action();
            if (IsCurrent(generation)) Status = "Image quality analysis complete.";
        }), () => CanEditMetadata, () => !_closed);
        Finish = new(() => SelectedImage, () => _session is { Xisf: null, Fits: null },
            (status, action) => RunAsync(status, async generation =>
            {
                await action();
                if (IsCurrent(generation)) Status = status.StartsWith("Saving", StringComparison.Ordinal)
                    ? "Finished image saved. The original image is unchanged."
                    : "Finished image ready. Save it as PNG or JPEG.";
            }), () => CanEditMetadata, () => !_closed);
    }

    public string FilePath { get => _filePath; set => SetProperty(ref _filePath, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string ImageDetails { get => _details; private set => SetProperty(ref _details, value); }
    public IReadOnlyList<MetadataNode> Metadata
    {
        get => _metadata;
        private set
        {
            if (SetProperty(ref _metadata, value)) OnPropertyChanged(nameof(HasMetadata));
        }
    }
    public bool HasMetadata => Metadata.Count > 0;
    public bool IsBusy
    {
        get => _busy;
        private set
        {
            if (!SetProperty(ref _busy, value)) return;
            OnPropertyChanged(nameof(CanInteract));
            LoadCommand.NotifyCanExecuteChanged(); UnloadCommand.NotifyCanExecuteChanged();
            ApplyCommand.NotifyCanExecuteChanged(); SelectImageCommand.NotifyCanExecuteChanged();
            ValidateCommand.NotifyCanExecuteChanged();
            ApplyMetadataCommand.NotifyCanExecuteChanged();
            ApplyDocumentCommand.NotifyCanExecuteChanged();
            DebayerCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(CanDebayer));
            OnPropertyChanged(nameof(CanEditMetadata));
            Utilities.NotifyCommands();
            Quality.AnalyzeCommand.NotifyCanExecuteChanged();
            Finish.NotifyCommands();
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
    public ObservableCollection<PropertyEditorRow> ImageProperties { get; } = [];
    public ObservableCollection<FitsKeywordEditorRow> FitsKeywords { get; } = [];
    public XisfImageData? SelectedImage { get; private set; }
    public ImageEditorFields ImageEditor { get => _imageEditor; private set => SetProperty(ref _imageEditor, value); }
    public TableEditorFields TableEditor { get => _tableEditor; private set => SetProperty(ref _tableEditor, value); }
    public DocumentEditorState DocumentEditor { get; } = new();
    public ViewerUtilityState Utilities { get; }
    public ViewerQualityState Quality { get; }
    public ViewerFinishState Finish { get; }
    public IReadOnlyList<string> BayerPatterns => ViewerDebayerService.Patterns;
    public string? BayerPattern
    {
        get => _bayerPattern;
        set { if (SetProperty(ref _bayerPattern, value)) DebayerCommand.NotifyCanExecuteChanged(); }
    }
    public string BayerDescription { get => _bayerDescription; private set => SetProperty(ref _bayerDescription, value); }
    public bool CanDebayer => CanInteract && ViewerDebayerService.IsSupported(SelectedImage);
    public IAsyncRelayCommand DebayerCommand { get; }
    public ICommand CancelDebayerCommand { get; }
    public IReadOnlyList<XisfCompression> CompressionChoices { get; } = Enum.GetValues<XisfCompression>();
    public IReadOnlyList<XisfChecksum> ChecksumChoices { get; } = Enum.GetValues<XisfChecksum>();
    public XisfCompression Compression { get; set; } = XisfCompression.Zlib;
    public XisfChecksum Checksum { get; set; } = XisfChecksum.Sha256;
    public bool Shuffle { get; set; } = true;
    public bool Distributed { get; set; }
    public bool CanCopyOriginal { get => _canCopyOriginal; private set => SetProperty(ref _canCopyOriginal, value); }
    public bool PreserveOriginal { get => _preserveOriginal; set => SetProperty(ref _preserveOriginal, value); }
    public string IccProfileDescription => _iccProfile is null ? "None" : $"{_iccProfile.Length:N0} bytes";
    public string ThumbnailDescription => _thumbnail is null ? "None" : string.Join(" × ", _thumbnail.Dimensions);
    public bool CanEditMetadata => CanInteract && SelectedImage is not null;
    public bool ShouldCopyOriginal(string outputPath) => !ViewerSession.IsFitsPath(outputPath) && !Distributed && CanCopyOriginal && PreserveOriginal;
    public IAsyncRelayCommand LoadCommand { get; }
    public IAsyncRelayCommand UnloadCommand { get; }
    public IAsyncRelayCommand ApplyCommand { get; }
    public IAsyncRelayCommand SelectImageCommand { get; }
    public IAsyncRelayCommand ValidateCommand { get; }
    public IAsyncRelayCommand ApplyMetadataCommand { get; }
    public IAsyncRelayCommand ApplyDocumentCommand { get; }
    public bool ResetViewportOnPresentation { get; private set; }
    public ViewerLoadTiming? LastLoadTiming { get; private set; }

    public void Close() { _closed = true; _generation++; Quality.AnalyzeCommand.Cancel(); DebayerCommand.Cancel(); Finish.Refresh(); }

    public Task SaveFinishedAsync(string path) => Finish.SaveAsync(path, _session?.SourcePath);

    private Task LoadAsync() => RunAsync("Loading astronomy image…", async generation =>
    {
        var path = FilePath.Trim();
        LastLoadTiming = null;
        var timer = System.Diagnostics.Stopwatch.StartNew(); long allocated = GC.GetTotalAllocatedBytes();
        var stretch = CreateStretch();
        var session = await Task.Run(() => ViewerSession.Load(path));
        double beforeRender = timer.Elapsed.TotalMilliseconds;
        // A failed load must not replace the previously displayed session.
        var rendered = await Task.Run(() => XisfDisplayRenderer.Render(session.GetImage(0), stretch));
        double afterRender = timer.Elapsed.TotalMilliseconds;
        if (!IsCurrent(generation)) return;
        ResetViewportOnPresentation = true;
        await _present(rendered);
        double assigned = timer.Elapsed.TotalMilliseconds;
        if (!IsCurrent(generation)) return;
        _presentedIndex = 0;
        _session = session;
        Finish.Refresh();
        CanCopyOriginal = session.CanCopyOriginal; PreserveOriginal = CanCopyOriginal;
        ImageLabels.Clear();
        for (int i = 0; i < session.Images.Count; i++) ImageLabels.Add(session.GetImageLabel(i));
        SelectedImageIndex = 0;
        SetDetails(session.GetImage(0));
        Metadata = [];
        Diagnostics.Clear();
        Status = "Image ready. Preparing metadata and validation…";
        await _yieldPresentation();
        if (!IsCurrent(generation)) return;
        var details = await Task.Run(() => (
            Metadata: session.Xisf is { } xisf ? ViewerMetadataBuilder.BuildMetadata(xisf, session.SourcePath)
                : ViewerMetadataBuilder.BuildFitsMetadata(session.Fits!, session.SourcePath),
            Findings: GetDiagnostics(session)));
        if (!IsCurrent(generation)) return;
        Metadata = details.Metadata;
        SetDiagnostics(details.Findings);
        PopulateMetadataEditor(session.GetImage(0));
        DocumentEditor.Load(session.Xisf);
        LastLoadTiming = new(session.ParseMilliseconds, session.MaterializeMilliseconds, afterRender - beforeRender,
            assigned - afterRender, assigned, timer.Elapsed.TotalMilliseconds, GC.GetTotalAllocatedBytes() - allocated);
        Status = $"Loaded {Path.GetFileName(path)} ({session.Images.Count} image(s){(session.Fits is null ? "" : ", converted from FITS")}).";
    });

    private Task SelectImageAsync() => RenderAsync();

    private Task DebayerAsync(CancellationToken cancellationToken) => RunAsync("Debayering image with bilinear interpolation…", async generation =>
    {
        if (_session is not { } session || SelectedImage is not { } source || BayerPattern is not { } pattern) return;
        var stretch = CreateStretch();
        // Work from the applied image; editor drafts remain attached to the original until selection changes.
        var result = await Task.Run(() =>
        {
            var image = ViewerDebayerService.Create(source, pattern, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var rendered = XisfDisplayRenderer.Render(image, stretch);
            cancellationToken.ThrowIfCancellationRequested();
            return (Image: image, Rendered: rendered);
        }, cancellationToken);
        if (!IsCurrent(generation)) return;
        ResetViewportOnPresentation = true;
        await _present(result.Rendered);
        if (!IsCurrent(generation)) return;
        int index = session.AddDerivedImage(result.Image);
        ImageLabels.Add(session.GetImageLabel(index));
        SelectedImageIndex = _presentedIndex = index;
        CanCopyOriginal = PreserveOriginal = false;
        PopulateMetadataEditor(result.Image);
        SetDetails(result.Image);
        Status = $"Debayered {pattern} to RGB. Original mosaic remains in Selected image. Save re-encodes the RGB image; unapplied image drafts were discarded.";
    });

    private Task ApplyDocumentAsync() => RunAsync("Applying document edits…", _ =>
    {
        DocumentEditor.Apply();
        Status = "Document metadata applied to the working document.";
        return Task.CompletedTask;
    });

    private Task ApplyMetadataAsync() => RunAsync("Applying metadata edits…", async generation =>
    {
        if (SelectedImage is { } image)
        {
            var stretch = CreateStretch();
            ApplyImageDraft(image);
            TableEditor = TableEditorFields.FromTable(image.Tables.FirstOrDefault());
            ImageEditor = ImageEditorFields.FromImage(image);
            if (_presentedIndex >= 0) ImageLabels[_presentedIndex] = $"{_presentedIndex + 1}: {image.Id ?? "unnamed image"}";
            SetDetails(image);
            Utilities.Refresh();
            Quality.Refresh();
            Finish.Refresh();
            RefreshDebayer();
            var rendered = await Task.Run(() => XisfDisplayRenderer.Render(image, stretch));
            if (!IsCurrent(generation)) return;
            ResetViewportOnPresentation = true;
            await _present(rendered);
        }
        Status = "Metadata applied to the working image.";
    });

    private void ApplyImageDraft(XisfImageData image)
    {
        var attributes = ImageEditor.Parse(image);
        var table = TableEditor.Parse();
        ViewerEditorCodec.ApplyMetadata(image, ImageProperties, FitsKeywords);
        attributes.Apply(image);
        image.IccProfile = _iccProfile; image.Thumbnail = _thumbnail;
        if (table is not null) { if (image.Tables.Count == 0) image.Tables.Add(table); else image.Tables[0] = table; }
    }

    public Task AttachIccAsync(string path) => RunAsync("Reading ICC profile…", async generation =>
    {
        var bytes = await Task.Run(() => File.ReadAllBytes(path));
        if (!IsCurrent(generation)) return;
        _iccProfile = bytes; OnPropertyChanged(nameof(IccProfileDescription));
        Status = "ICC profile staged. Apply metadata edits or save to attach it.";
    });
    public Task AttachThumbnailAsync(string path) => RunAsync("Reading thumbnail…", async generation =>
    {
        var image = await Task.Run(() => ViewerRasterDecoder.Load(path, true));
        if (!IsCurrent(generation)) return;
        _thumbnail = image; OnPropertyChanged(nameof(ThumbnailDescription));
        Status = "Thumbnail staged. Apply metadata edits or save to attach it.";
    });
    public Task ImportAsync(string path) => RunAsync("Importing raster image…", async generation =>
    {
        var image = await Task.Run(() => ViewerRasterDecoder.Load(path));
        var pixels = await Task.Run(() => XisfDisplayRenderer.Render(image));
        if (!IsCurrent(generation)) return;
        _session = ViewerSession.FromRaster(path, image); _presentedIndex = 0;
        FilePath = path; CanCopyOriginal = PreserveOriginal = false;
        ImageLabels.Clear(); ImageLabels.Add(_session.GetImageLabel(0)); SelectedImageIndex = 0;
        Metadata = []; Diagnostics.Clear(); DocumentEditor.Load(null); PopulateMetadataEditor(image); SetDetails(image);
        ResetViewportOnPresentation = true; await _present(pixels);
        Status = $"Imported {Path.GetFileName(path)}. Configure metadata and save.";
    });

    public Task SaveAsync(string path) => RunAsync("Writing and validating astronomy image…", async generation =>
    {
        var image = SelectedImage ?? throw new InvalidOperationException("Load or import an image first.");
        bool fits = ViewerSession.IsFitsPath(path), distributed = !fits && Distributed;
        string? copySource = ShouldCopyOriginal(path) ? _session!.SourcePath : null;
        DocumentEditorSnapshot document;
        XisfWriterOptions options;
        if (copySource is not null)
        {
            document = new(new Dictionary<string, object?>(), new Dictionary<string, object?>(), ""); options = new();
        }
        else
        {
            // Stage document rows before image mutation, including invalid late table/attribute fields.
            document = DocumentEditorState.Parse(DocumentEditor.Metadata, DocumentEditor.Properties, DocumentEditor.CreatorApplication);
            options = new() { Compression = Compression, Checksum = Checksum, Shuffle = Shuffle, CreatorApplication = document.CreatorApplication };
            ApplyImageDraft(image);
            Finish.Refresh(); Quality.Refresh();
        }
        var findings = await Task.Run(() => ViewerFileService.Save(path, image, document, options, distributed, copySource));
        var session = await Task.Run(() => ViewerSession.Load(path));
        var pixels = await Task.Run(() => XisfDisplayRenderer.Render(session.GetImage(0), CreateStretch()));
        if (!IsCurrent(generation)) return;
        _session = session; FilePath = path; _presentedIndex = 0;
        CanCopyOriginal = session.CanCopyOriginal; PreserveOriginal = CanCopyOriginal;
        ImageLabels.Clear(); for (int i = 0; i < session.Images.Count; i++) ImageLabels.Add(session.GetImageLabel(i)); SelectedImageIndex = 0;
        ResetViewportOnPresentation = true; await _present(pixels); await _yieldPresentation();
        var details = await Task.Run(() => (Metadata: session.Xisf is { } xisf ? ViewerMetadataBuilder.BuildMetadata(xisf, path)
            : ViewerMetadataBuilder.BuildFitsMetadata(session.Fits!, path), Findings: GetDiagnostics(session)));
        if (!IsCurrent(generation)) return;
        Metadata = details.Metadata; SetDiagnostics(details.Findings.Concat(findings.Where(f => f.Effect == ConversionEffect.Omitted)
            .Select(f => new ViewerDiagnostic("Conversion", f.Code, f.Message))));
        PopulateMetadataEditor(session.GetImage(0)); DocumentEditor.Load(session.Xisf); SetDetails(session.GetImage(0));
        Status = copySource is not null ? "Saved a byte-identical copy and reloaded it. Editor changes were not applied."
            : $"Saved, reloaded and validated {Path.GetFileName(path)}.";
    });

    private void PopulateMetadataEditor(XisfImageData? image)
    {
        SelectedImage = image;
        ImageEditor = image is null ? new() : ImageEditorFields.FromImage(image);
        TableEditor = TableEditorFields.FromTable(image?.Tables.FirstOrDefault());
        _iccProfile = image?.IccProfile; _thumbnail = image?.Thumbnail;
        OnPropertyChanged(nameof(IccProfileDescription)); OnPropertyChanged(nameof(ThumbnailDescription));
        Utilities.Refresh();
        Quality.Refresh();
        Finish.Refresh();
        ImageProperties.Clear(); FitsKeywords.Clear();
        RefreshDebayer();
        if (image is not null)
        {
            foreach (var pair in image.Properties) ImageProperties.Add(ViewerEditorCodec.ToEditorRow(pair.Key, pair.Value));
            foreach (var keyword in image.FitsKeywords) FitsKeywords.Add(new() { Name = keyword.Name, Value = keyword.Value, Comment = keyword.Comment ?? "" });
        }
        OnPropertyChanged(nameof(SelectedImage)); OnPropertyChanged(nameof(CanEditMetadata));
        ApplyMetadataCommand.NotifyCanExecuteChanged();
        ApplyDocumentCommand.NotifyCanExecuteChanged();
    }

    private void RefreshDebayer()
    {
        var detected = ViewerDebayerService.Detect(SelectedImage);
        BayerPattern = detected.Pattern;
        BayerDescription = detected.Description;
        OnPropertyChanged(nameof(CanDebayer));
        DebayerCommand.NotifyCanExecuteChanged();
    }

    private Task ValidateAsync() => RunAsync("Validating document…", async generation =>
    {
        if (_session is not { } session) return;
        var findings = await Task.Run(() => GetDiagnostics(session));
        if (!IsCurrent(generation)) return;
        SetDiagnostics(findings);
        Status = $"Validation complete ({findings.Length} finding(s)).";
    });

    private static ViewerDiagnostic[] GetDiagnostics(ViewerSession session) => session.Xisf is { } xisf
        ? xisf.Validate().Select(d => new ViewerDiagnostic(d.Severity.ToString(), d.Code, d.Message)).ToArray()
        : session.Fits?.Diagnostics.Select(d => new ViewerDiagnostic(d.Severity.ToString(), d.Code, d.Message)).ToArray() ?? [];

    private void SetDiagnostics(IEnumerable<ViewerDiagnostic> findings)
    {
        Diagnostics.Clear();
        foreach (var diagnostic in findings) Diagnostics.Add(diagnostic);
    }

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
        if (index != _presentedIndex)
        {
            PopulateMetadataEditor(result.Image);
            CanCopyOriginal = session.CanCopyOriginal && index < session.OriginalImageCount;
            PreserveOriginal = CanCopyOriginal;
        }
        _presentedIndex = index;
        SetDetails(result.Image);
        Status = "Image ready.";
    });

    private async Task UnloadAsync()
    {
        if (!CanInteract) return;
        _generation++;
        _session = null;
        CanCopyOriginal = PreserveOriginal = false;
        PopulateMetadataEditor(null);
        DocumentEditor.Load(null);
        ImageLabels.Clear(); Diagnostics.Clear(); Metadata = []; SelectedImageIndex = -1;
        _presentedIndex = -1; ResetViewportOnPresentation = true;
        FilePath = ""; ImageDetails = ""; Status = "No image loaded.";
        await _present(null);
        ApplyCommand.NotifyCanExecuteChanged(); SelectImageCommand.NotifyCanExecuteChanged();
        ValidateCommand.NotifyCanExecuteChanged();
    }

    private async Task RunAsync(string status, Func<int, Task> action)
    {
        if (!CanInteract) return;
        int generation = ++_generation;
        IsBusy = true; Status = status;
        try { await action(generation); }
        catch (OperationCanceledException) { if (IsCurrent(generation)) Status = "Operation cancelled."; }
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
/// <summary>Managed staged timings; bitmap assignment is not physical screen presentation.</summary>
public sealed record ViewerLoadTiming(double ParseMilliseconds, double MaterializeMilliseconds, double RenderMilliseconds,
    double BitmapTransferMilliseconds, double BitmapAssignedMilliseconds, double AllTabsReadyMilliseconds, long AllocatedBytes);
