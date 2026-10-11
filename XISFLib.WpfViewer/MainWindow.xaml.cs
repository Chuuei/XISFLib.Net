using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

using Microsoft.Win32;
using XISFLib.Viewer;

namespace XISFLib.WpfViewer;

public partial class MainWindow : Window
{
    private const double MinimumImageZoom = 1;
    private const double MaximumImageZoom = 64;
    private const double ImageZoomStep = 1.2;
    private XisfDocument? _document;
    private FitsDocument? _fitsDocument;
    private XisfImageData?[] _editableImages = [];
    private bool _isBusy;
    private bool _isPanning;
    private Point _panStart;
    private Vector _panOrigin;
    private double _imageZoom = 1;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        InitializeWorkbench();
        UpdateStretchControls();
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Title = "Select an XISF or FITS image",
            Filter = "Astronomy images (*.xisf;*.xish;*.fit;*.fits;*.fts)|*.xisf;*.xish;*.fit;*.fits;*.fts|XISF images (*.xisf;*.xish)|*.xisf;*.xish|FITS images (*.fit;*.fits;*.fts)|*.fit;*.fits;*.fts|All files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == true)
            FilePathTextBox.Text = dialog.FileName;
    }

    private async void LoadButton_Click(object sender, RoutedEventArgs e)
    {
        string path = FilePathTextBox.Text.Trim();
        if (path.Length == 0)
        {
            ShowError("Select an XISF or FITS file first.");
            return;
        }

        await RunBusyAsync("Loading astronomy image…", async () =>
        {
            LoadedFile loaded = await Task.Run(() => LoadFile(path));
            bool fits = loaded.Fits is not null;
            _document = loaded.Xisf;
            _fitsDocument = loaded.Fits;
            _editableImages = loaded.Images;
            bool monolithic = !fits && ViewerFileService.IsMonolithicXisf(path);
            _sourceXisfPath = monolithic ? Path.GetFullPath(path) : null;
            PreserveOriginalCheckBox.IsEnabled = monolithic;
            PreserveOriginalCheckBox.IsChecked = monolithic;
            ImageSelector.ItemsSource = Enumerable.Range(0, _editableImages.Length).Select(GetImageLabel).ToArray();
            ImageSelector.SelectedIndex = 0;
            _editableImage = _editableImages[0]!;
            _editorImageIndex = 0;
            ResetImageViewport();
            await RenderSelectedImageAsync();
            // Let WPF process its render queue before preparing the remaining tabs.
            await Dispatcher.Yield(DispatcherPriority.Background);
            StatusText.Text = "Image ready. Preparing metadata and validation…";
            var details = await Task.Run(() =>
            {
                IReadOnlyList<MetadataNode> metadata = loaded.Xisf is not null ? BuildMetadata(loaded.Xisf, path) : BuildFitsMetadata(loaded.Fits!, path);
                IReadOnlyList<XisfDiagnostic>? diagnostics = loaded.Xisf?.Validate();
                return (Metadata: metadata, Diagnostics: diagnostics);
            });
            if (_document is not null)
                PopulateLoadedDocumentEditors(_document);
            else
                ClearDocumentEditors();
            MetadataTree.ItemsSource = details.Metadata;
            MetadataTree.Visibility = Visibility.Visible;
            EmptyMetadataText.Visibility = Visibility.Collapsed;
            await LoadImageEditorAsync(0);
            RefreshUtilityOverview();
            if (details.Diagnostics is not null) ShowDiagnostics(details.Diagnostics);
            else ShowDiagnostics(loaded.Fits!.Diagnostics);
            StatusText.Text = $"Loaded {Path.GetFileName(path)} ({_editableImages.Length} image(s){(fits ? ", converted from FITS" : "")}).";
        });
    }

    private static bool IsFitsPath(string path) => Path.GetExtension(path).ToLowerInvariant() is ".fit" or ".fits" or ".fts";

    private static LoadedFile LoadFile(string path)
    {
        var session = XISFLib.Viewer.ViewerSession.Load(path);
        return new LoadedFile(session.Xisf, session.Fits, session.Images.ToArray());
    }

    private string GetImageLabel(int index) => $"{index + 1}: {(_document?.Images[index].Id ?? _editableImages[index]?.Id) ?? "unnamed image"}";

    private async void ImageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_editableImages.Length > 0 && !_isBusy && ImageSelector.SelectedIndex >= 0)
        {
            int index = ImageSelector.SelectedIndex;
            await RunBusyAsync("Rendering image…", async () =>
            {
                if (index != _editorImageIndex)
                {
                    await LoadImageEditorAsync(index);
                    RefreshUtilityOverview();
                    ResetImageViewport();
                }
                await RenderSelectedImageAsync();
            });
        }
    }

    private void ImageViewport_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (ImageView.Source is null)
            return;

        double factor = e.Delta > 0 ? ImageZoomStep : 1 / ImageZoomStep;
        double newZoom = Math.Clamp(_imageZoom * factor, MinimumImageZoom, MaximumImageZoom);
        if (Math.Abs(newZoom - _imageZoom) < double.Epsilon)
            return;

        Point pointer = e.GetPosition(ImageViewport);
        double ratio = newZoom / _imageZoom;
        ImageTranslateTransform.X = pointer.X - (pointer.X - ImageTranslateTransform.X) * ratio;
        ImageTranslateTransform.Y = pointer.Y - (pointer.Y - ImageTranslateTransform.Y) * ratio;
        _imageZoom = newZoom;
        ImageScaleTransform.ScaleX = newZoom;
        ImageScaleTransform.ScaleY = newZoom;
        e.Handled = true;
    }

    private void ImageViewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ImageView.Source is null)
            return;

        _isPanning = true;
        _panStart = e.GetPosition(ImageViewport);
        _panOrigin = new Vector(ImageTranslateTransform.X, ImageTranslateTransform.Y);
        ImageViewport.Cursor = Cursors.SizeAll;
        ImageViewport.CaptureMouse();
        e.Handled = true;
    }

    private void ImageViewport_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPanning || e.LeftButton != MouseButtonState.Pressed)
            return;

        Vector movement = e.GetPosition(ImageViewport) - _panStart;
        ImageTranslateTransform.X = _panOrigin.X + movement.X;
        ImageTranslateTransform.Y = _panOrigin.Y + movement.Y;
        e.Handled = true;
    }

    private void ImageViewport_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isPanning)
            return;

        ImageViewport.ReleaseMouseCapture();
        EndImagePan();
        e.Handled = true;
    }

    private void ImageViewport_LostMouseCapture(object sender, MouseEventArgs e) => EndImagePan();

    private void EndImagePan()
    {
        _isPanning = false;
        ImageViewport.Cursor = Cursors.Hand;
    }

    private void ResetImageViewport()
    {
        _imageZoom = 1;
        ImageScaleTransform.ScaleX = 1;
        ImageScaleTransform.ScaleY = 1;
        ImageTranslateTransform.X = 0;
        ImageTranslateTransform.Y = 0;
        EndImagePan();
    }

    private async void StretchSetting_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        UpdateStretchControls();
        if (_editableImage is not null && !_isBusy)
            await RunBusyAsync("Applying display settings…", RenderSelectedImageAsync);
    }

    private async void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_editableImage is not null)
            await RunBusyAsync("Applying display settings…", RenderSelectedImageAsync);
    }

    private async void StretchValue_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_editableImage is not null && AutoStretchCheckBox.IsChecked == true && !_isBusy)
            await RunBusyAsync("Applying display settings…", RenderSelectedImageAsync);
    }

    private void StretchValue_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            ApplyButton.Focus();
        }
    }

    private async Task RenderSelectedImageAsync()
    {
        if (_editableImage is null || ImageSelector.SelectedIndex < 0)
            return;

        XisfStretchOptions? stretch = CreateStretchOptions();
        XisfImageData image = _editableImage;
        ImageView.Source = await Task.Run(() => image.ToBitmapSource(stretch));
        EmptyViewText.Visibility = Visibility.Collapsed;
        ImageDetailsText.Text = string.Format(CultureInfo.CurrentCulture,
            "{0} × {1}, {2} channel(s), {3}, {4}",
            _editableImage.Dimensions[0], _editableImage.Dimensions[1], _editableImage.ChannelCount, _editableImage.SampleFormat,
            _editableImage.ColorSpace ?? "unspecified color space");
    }

    private XisfStretchOptions? CreateStretchOptions()
    {
        if (AutoStretchCheckBox.IsChecked != true)
            return null;

        double target = ParseNumber(TargetBackgroundTextBox.Text, "Target background");
        double clipping = ParseNumber(ShadowsClippingTextBox.Text, "Shadows clipping");
        if (target is <= 0 or >= 1)
            throw new ArgumentOutOfRangeException(nameof(target), "Target background must be between zero and one.");
        if (clipping > 0)
            throw new ArgumentOutOfRangeException(nameof(clipping), "Shadows clipping must be zero or negative.");

        return new XisfStretchOptions
        {
            TargetBackground = target,
            ShadowsClipping = clipping,
            LinkChannels = LinkChannelsCheckBox.IsChecked == true
        };
    }

    private static double ParseNumber(string text, string name)
    {
        const NumberStyles style = NumberStyles.Float;
        if (double.TryParse(text, style, CultureInfo.CurrentCulture, out double value) ||
            double.TryParse(text, style, CultureInfo.InvariantCulture, out value))
            return value;
        throw new FormatException($"{name} is not a valid number.");
    }

    private void UpdateStretchControls()
    {
        bool enabled = AutoStretchCheckBox.IsChecked == true;
        LinkChannelsCheckBox.IsEnabled = enabled;
        TargetBackgroundTextBox.IsEnabled = enabled;
        ShadowsClippingTextBox.IsEnabled = enabled;
    }

    private static IReadOnlyList<MetadataNode> BuildMetadata(XisfDocument document, string path) => ViewerMetadataBuilder.BuildMetadata(document, path);
    private static IReadOnlyList<MetadataNode> BuildFitsMetadata(FitsDocument document, string path) => ViewerMetadataBuilder.BuildFitsMetadata(document, path);

    private async Task RunBusyAsync(string status, Func<Task> action)
    {
        if (_isBusy)
            return;

        _isBusy = true;
        LoadButton.IsEnabled = false;
        UnloadButton.IsEnabled = false;
        ImportButton.IsEnabled = false;
        SaveButton.IsEnabled = false;
        ApplyButton.IsEnabled = false;
        ImageSelector.IsEnabled = false;
        ImageEditorTab.IsEnabled = false;
        WriterTab.IsEnabled = false;
        UtilitiesTab.IsEnabled = false;
        StatusText.Text = status;
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            Mouse.OverrideCursor = null;
            LoadButton.IsEnabled = true;
            UnloadButton.IsEnabled = true;
            ImportButton.IsEnabled = true;
            SaveButton.IsEnabled = true;
            ApplyButton.IsEnabled = true;
            ImageSelector.IsEnabled = _editableImages.Length > 1;
            ImageEditorTab.IsEnabled = true;
            WriterTab.IsEnabled = true;
            UtilitiesTab.IsEnabled = true;
            _isBusy = false;
        }
    }

    private void ShowError(string message)
    {
        StatusText.Text = message;
        MessageBox.Show(this, message, "XISF Library Workbench", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}

internal sealed record LoadedFile(XisfDocument? Xisf, FitsDocument? Fits, XisfImageData?[] Images);
