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
