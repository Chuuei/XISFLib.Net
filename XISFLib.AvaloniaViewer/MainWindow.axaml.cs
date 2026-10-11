using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using XISFLib.Viewer;

namespace XISFLib.AvaloniaViewer;

public partial class MainWindow : Window
{
    private readonly WorkbenchViewModel _viewModel;
    private WriteableBitmap? _bitmap;
    private bool _closed;
    private double _zoom = 1;
    private Point? _panStart;
    private Vector _panOrigin;
    private ScaleTransform ImageScale => (ScaleTransform)((TransformGroup)ImageView.RenderTransform!).Children[0];
    private TranslateTransform ImageTranslation => (TranslateTransform)((TransformGroup)ImageView.RenderTransform!).Children[1];

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new(PresentAsync, async () => await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background));
        DataContext = _viewModel;
        Closed += (_, _) => { _closed = true; _viewModel.Close(); ImageView.Source = null; _bitmap?.Dispose(); _bitmap = null; };
    }

    private Task PresentAsync(XisfBgra32Image? pixels)
    {
        if (_closed) return Task.CompletedTask;
        var bitmap = pixels is null ? null : AvaloniaBitmapAdapter.Create(pixels);
        var old = _bitmap;
        _bitmap = bitmap; ImageView.Source = bitmap; EmptyView.IsVisible = bitmap is null;
        old?.Dispose();
        if (_viewModel.ResetViewportOnPresentation) ResetViewport();
        return Task.CompletedTask;
    }

    private async void Browse_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new()
            {
                Title = "Select an XISF or FITS image", AllowMultiple = false,
                FileTypeFilter = [new("Astronomy images") { Patterns = ["*.xisf", "*.xish", "*.fit", "*.fits", "*.fts"] }, FilePickerFileTypes.All]
            });
            if (files.Count == 0) return;
            var path = files[0].TryGetLocalPath();
            if (path is null) throw new NotSupportedException("Select a local file so attached and distributed resources remain available.");
            _viewModel.FilePath = path;
        }
        catch (Exception exception)
        {
            var dialog = new Window { Title = "Unable to select file", Width = 500, Height = 180,
                Content = new TextBlock { Text = exception.Message, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Margin = new Thickness(20) } };
            await dialog.ShowDialog(this);
        }
    }

    private async void ImageSelection_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is WorkbenchViewModel vm && vm.SelectedImageIndex >= 0 && vm.SelectImageCommand.CanExecute(null))
            await vm.SelectImageCommand.ExecuteAsync(null);
    }

    private void Properties_BeginningEdit(object? sender, DataGridBeginningEditEventArgs e)
    {
        if (e.Row.DataContext is PropertyEditorRow row && (row.IsReadOnly || (Equals(e.Column.Header, "Value") && row.RequiresValueDialog))) e.Cancel = true;
    }

    private async void OpenPropertyValue_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Control { DataContext: PropertyEditorRow row }) await OpenPropertyValueAsync(row);
    }
    private async void PropertyValue_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (sender is DataGrid { SelectedItem: PropertyEditorRow { RequiresValueDialog: true } row } grid && Equals(grid.CurrentColumn?.Header, "Value"))
            await OpenPropertyValueAsync(row);
    }
    private async Task OpenPropertyValueAsync(PropertyEditorRow row)
    {
        if (_viewModel.CanEditMetadata && CommitMetadataGrids() && CommitDocumentGrids())
            await PropertyValueWindow.OpenAsync(this, row);
    }

    private static FilePickerFileType RasterType => new("Raster images") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.tif", "*.tiff", "*.bmp", "*.gif"] };
    private async Task<string?> PickLocalAsync(string title, FilePickerFileType type)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new() { Title = title, AllowMultiple = false, FileTypeFilter = [type, FilePickerFileTypes.All] });
        if (files.Count == 0) return null;
        return files[0].TryGetLocalPath() ?? throw new NotSupportedException("This operation requires a local file path.");
    }
    private async Task DialogActionAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception exception)
        {
            var dialog = new Window { Title = "Operation failed", Width = 500, Height = 200,
                Content = new TextBlock { Text = exception.Message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(20) } };
            if (!_closed) await dialog.ShowDialog(this);
        }
    }
    private async void Import_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await DialogActionAsync(async () =>
    {
        if (!_viewModel.CanInteract) return;
        var path = await PickLocalAsync("Import a standard 2D image", RasterType);
        if (path is not null) await _viewModel.ImportAsync(path);
    });
    private async void Icc_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await DialogActionAsync(async () =>
    {
        if (!_viewModel.CanEditMetadata) return;
        var path = await PickLocalAsync("Select an ICC profile", new("ICC profiles") { Patterns = ["*.icc", "*.icm"] });
        if (path is not null) await _viewModel.AttachIccAsync(path);
    });
    private async void Thumbnail_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await DialogActionAsync(async () =>
    {
        if (!_viewModel.CanEditMetadata) return;
        var path = await PickLocalAsync("Select a thumbnail image", RasterType);
        if (path is not null) await _viewModel.AttachThumbnailAsync(path);
    });
    private async void Save_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await DialogActionAsync(async () =>
    {
        if (!_viewModel.CanEditMetadata) return;
        var file = await StorageProvider.SaveFilePickerAsync(new()
        {
            Title = "Save astronomy image", DefaultExtension = _viewModel.Distributed ? "xish" : "xisf", ShowOverwritePrompt = true,
            FileTypeChoices = _viewModel.Distributed ? [new("Distributed XISF") { Patterns = ["*.xish"] }]
                : [new("XISF") { Patterns = ["*.xisf"] }, new("FITS") { Patterns = ["*.fits"] }]
        });
        if (file is null) return;
        var path = file.TryGetLocalPath() ?? throw new NotSupportedException("Saving requires a local path for attachment and atomic-write semantics.");
        if (string.IsNullOrEmpty(Path.GetExtension(path))) path += _viewModel.Distributed ? ".xish" : ".xisf";
        await SaveToPathAsync(path);
    });
    public Task SaveToPathAsync(string path)
    {
        if (!_viewModel.CanEditMetadata) return Task.CompletedTask;
        bool copy = !ViewerSession.IsFitsPath(path) && !_viewModel.Distributed && _viewModel.CanCopyOriginal && _viewModel.PreserveOriginal;
        if (!copy && (!CommitMetadataGrids() || !CommitDocumentGrids())) throw new FormatException("Complete or correct the active grid edit first.");
        return _viewModel.SaveAsync(path);
    }
    private async void Display_LostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel.AutoStretch && _viewModel.ApplyCommand.CanExecute(null)) await _viewModel.ApplyCommand.ExecuteAsync(null);
    }
    private async void DisplayToggle_Changed(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        if (DataContext is WorkbenchViewModel vm && vm.ApplyCommand.CanExecute(null)) await vm.ApplyCommand.ExecuteAsync(null);
    }
    private void Display_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true; ApplyDisplayButton.Focus();
    }
    private async void Notices_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var assembly = typeof(MainWindow).Assembly;
        var notices = ViewerNotices.Read() + string.Join(Environment.NewLine, assembly.GetManifestResourceNames().Where(n => n.Contains(".Notices.")).Select(name =>
        {
            using var stream = assembly.GetManifestResourceStream(name)!; using var reader = new StreamReader(stream); return reader.ReadToEnd();
        }));
        var dialog = new Window { Title = "Third-party notices", Width = 850, Height = 600,
            Content = new TextBox { Text = notices, IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12) } };
        await dialog.ShowDialog(this);
    }

    private bool CommitMetadataGrids() =>
        ImagePropertiesGrid.CommitEdit(DataGridEditingUnit.Cell, true) && ImagePropertiesGrid.CommitEdit(DataGridEditingUnit.Row, true) &&
        FitsKeywordsGrid.CommitEdit(DataGridEditingUnit.Cell, true) && FitsKeywordsGrid.CommitEdit(DataGridEditingUnit.Row, true);

    private void AddProperty_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    { if (_viewModel.CanEditMetadata && CommitMetadataGrids()) _viewModel.ImageProperties.Add(new()); }
    private void RemoveProperty_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    { if (_viewModel.CanEditMetadata && CommitMetadataGrids() && ImagePropertiesGrid.SelectedItem is PropertyEditorRow row) _viewModel.ImageProperties.Remove(row); }
    private void AddKeyword_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    { if (_viewModel.CanEditMetadata && CommitMetadataGrids()) _viewModel.FitsKeywords.Add(new()); }
    private void RemoveKeyword_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    { if (_viewModel.CanEditMetadata && CommitMetadataGrids() && FitsKeywordsGrid.SelectedItem is FitsKeywordEditorRow row) _viewModel.FitsKeywords.Remove(row); }
    private async void ApplyMetadata_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel.ApplyMetadataCommand.CanExecute(null) && CommitMetadataGrids())
            await _viewModel.ApplyMetadataCommand.ExecuteAsync(null);
    }

    private bool CommitDocumentGrids() =>
        DocumentMetadataGrid.CommitEdit(DataGridEditingUnit.Cell, true) && DocumentMetadataGrid.CommitEdit(DataGridEditingUnit.Row, true) &&
        DocumentPropertiesGrid.CommitEdit(DataGridEditingUnit.Cell, true) && DocumentPropertiesGrid.CommitEdit(DataGridEditingUnit.Row, true);
    private void AddDocumentMetadata_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    { if (_viewModel.CanEditMetadata && CommitDocumentGrids()) _viewModel.DocumentEditor.Metadata.Add(new()); }
    private void RemoveDocumentMetadata_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    { if (_viewModel.CanEditMetadata && CommitDocumentGrids() && DocumentMetadataGrid.SelectedItem is PropertyEditorRow row) _viewModel.DocumentEditor.Metadata.Remove(row); }
    private void AddDocumentProperty_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    { if (_viewModel.CanEditMetadata && CommitDocumentGrids()) _viewModel.DocumentEditor.Properties.Add(new()); }
    private void RemoveDocumentProperty_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    { if (_viewModel.CanEditMetadata && CommitDocumentGrids() && DocumentPropertiesGrid.SelectedItem is PropertyEditorRow row) _viewModel.DocumentEditor.Properties.Remove(row); }
    private async void ApplyDocument_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel.ApplyDocumentCommand.CanExecute(null) && CommitDocumentGrids())
            await _viewModel.ApplyDocumentCommand.ExecuteAsync(null);
    }

    private void ResetViewport()
    {
        _zoom = 1; ImageScale.ScaleX = ImageScale.ScaleY = 1;
        ImageTranslation.X = ImageTranslation.Y = 0; _panStart = null;
    }

    private void Viewport_Wheel(object? sender, PointerWheelEventArgs e)
    {
        if (_bitmap is null) return;
        var zoom = Math.Clamp(_zoom * (e.Delta.Y > 0 ? 1.2 : 1 / 1.2), 1, 64);
        var pointer = e.GetPosition(ImageViewport); var ratio = zoom / _zoom;
        ImageTranslation.X = pointer.X - (pointer.X - ImageTranslation.X) * ratio;
        ImageTranslation.Y = pointer.Y - (pointer.Y - ImageTranslation.Y) * ratio;
        _zoom = zoom; ImageScale.ScaleX = ImageScale.ScaleY = zoom; e.Handled = true;
    }

    private void Viewport_Pressed(object? sender, PointerPressedEventArgs e)
    {
        if (_bitmap is null || !e.GetCurrentPoint(ImageViewport).Properties.IsLeftButtonPressed) return;
        _panStart = e.GetPosition(ImageViewport); _panOrigin = new(ImageTranslation.X, ImageTranslation.Y);
        e.Pointer.Capture(ImageViewport); e.Handled = true;
    }

    private void Viewport_Moved(object? sender, PointerEventArgs e)
    {
        if (_panStart is not { } start || !e.GetCurrentPoint(ImageViewport).Properties.IsLeftButtonPressed) return;
        var movement = e.GetPosition(ImageViewport) - start;
        ImageTranslation.X = _panOrigin.X + movement.X; ImageTranslation.Y = _panOrigin.Y + movement.Y;
    }

    private void Viewport_Released(object? sender, PointerReleasedEventArgs e) { _panStart = null; e.Pointer.Capture(null); }
    private void Viewport_CaptureLost(object? sender, PointerCaptureLostEventArgs e) => _panStart = null;
}
