using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using XISFLib.Viewer;

namespace XISFLib.WpfViewer;

public partial class MainWindow : Window
{
    private readonly WorkbenchViewModel _viewModel;
    private readonly ViewerViewportState _viewport = new();
    private bool _closed;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new(PresentAsync, async () => await Dispatcher.Yield(DispatcherPriority.Background));
        _viewModel.DocumentEditor.CreatorApplication = "XISFLib.WpfViewer";
        DataContext = _viewModel;
        InitializeWorkbench();
        _viewModel.PropertyChanged += (_, _) => UpdateViewState();
        _viewModel.Diagnostics.CollectionChanged += (_, _) => UpdateViewState();
        Closed += (_, _) => { _closed = true; _viewModel.Close(); ImageView.Source = null; };
        UpdateViewState();
    }

    private async Task PresentAsync(XisfBgra32Image? pixels)
    {
        BitmapSource? bitmap = pixels is null ? null : await Task.Run(() =>
        {
            var result = BitmapSource.Create(pixels.Width, pixels.Height, pixels.DpiX, pixels.DpiY,
                PixelFormats.Bgra32, null, pixels.Pixels, pixels.Stride);
            result.Freeze(); return result;
        });
        if (_closed) return;
        ImageView.Source = bitmap;
        EmptyViewText.Visibility = bitmap is null ? Visibility.Visible : Visibility.Collapsed;
        if (_viewModel.ResetViewportOnPresentation) ResetImageViewport();
    }
    private void UpdateViewState()
    {
        if (_closed) return;
        ImageSelector.IsEnabled = _viewModel.CanInteract && _viewModel.ImageLabels.Count > 1;
        ImageEditorTab.IsEnabled = WriterTab.IsEnabled = UtilitiesTab.IsEnabled = _viewModel.CanEditMetadata;
        MetadataTree.Visibility = _viewModel.HasMetadata ? Visibility.Visible : Visibility.Collapsed;
        EmptyMetadataText.Visibility = _viewModel.HasMetadata ? Visibility.Collapsed : Visibility.Visible;
        DiagnosticsGrid.Visibility = _viewModel.Diagnostics.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyDiagnosticsText.Visibility = _viewModel.Diagnostics.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        Cursor = _viewModel.IsBusy ? Cursors.Wait : null;
        UpdateStretchControls();
    }
    private void Window_Loaded(object sender, RoutedEventArgs e) => UpdateViewState();
    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog { Title = "Select an XISF or FITS image",
            Filter = "Astronomy images|*.xisf;*.xish;*.fit;*.fits;*.fts|All files|*.*", CheckFileExists = true };
        if (picker.ShowDialog(this) == true) _viewModel.FilePath = picker.FileName;
    }
    private async void LoadButton_Click(object sender, RoutedEventArgs e)
    { if (_viewModel.LoadCommand.CanExecute(null)) await _viewModel.LoadCommand.ExecuteAsync(null); }
    private async void ImageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is WorkbenchViewModel vm && vm.SelectedImageIndex >= 0 && vm.SelectImageCommand.CanExecute(null))
            await vm.SelectImageCommand.ExecuteAsync(null);
    }
    private void ImageViewport_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (ImageView.Source is null) return;
        var position = e.GetPosition(ImageViewport); _viewport.Wheel(e.Delta, position.X, position.Y);
        UpdateViewport(); e.Handled = true;
    }
    private void ImageViewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ImageView.Source is null) return;
        var position = e.GetPosition(ImageViewport); _viewport.BeginPan(position.X, position.Y);
        ImageViewport.Cursor = Cursors.SizeAll; ImageViewport.CaptureMouse(); e.Handled = true;
    }
    private void ImageViewport_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_viewport.IsPanning || e.LeftButton != MouseButtonState.Pressed) return;
        var position = e.GetPosition(ImageViewport); _viewport.Move(position.X, position.Y); UpdateViewport(); e.Handled = true;
    }
    private void ImageViewport_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    { if (!_viewport.IsPanning) return; ImageViewport.ReleaseMouseCapture(); EndImagePan(); e.Handled = true; }
    private void ImageViewport_LostMouseCapture(object sender, MouseEventArgs e) => EndImagePan();
    private void EndImagePan() { _viewport.EndPan(); ImageViewport.Cursor = Cursors.Hand; }
    private void ResetImageViewport() { _viewport.Reset(); ImageViewport.ReleaseMouseCapture(); UpdateViewport(); EndImagePan(); }
    private void UpdateViewport()
    {
        ImageScaleTransform.ScaleX = ImageScaleTransform.ScaleY = _viewport.Zoom;
        ImageTranslateTransform.X = _viewport.TranslationX; ImageTranslateTransform.Y = _viewport.TranslationY;
    }
    private async void StretchSetting_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        await Dispatcher.Yield(DispatcherPriority.Background);
        if (_viewModel.ApplyCommand.CanExecute(null)) await _viewModel.ApplyCommand.ExecuteAsync(null);
    }
    private async void ApplyButton_Click(object sender, RoutedEventArgs e)
    { if (_viewModel.ApplyCommand.CanExecute(null)) await _viewModel.ApplyCommand.ExecuteAsync(null); }
    private async void StretchValue_LostFocus(object sender, RoutedEventArgs e)
    { if (_viewModel.AutoStretch && _viewModel.ApplyCommand.CanExecute(null)) await _viewModel.ApplyCommand.ExecuteAsync(null); }
    private void StretchValue_KeyDown(object sender, KeyEventArgs e)
    { if (e.Key != Key.Enter) return; e.Handled = true; ApplyButton.Focus(); }
    private void UpdateStretchControls()
    {
        bool enabled = _viewModel.CanInteract && _viewModel.AutoStretch;
        LinkChannelsCheckBox.IsEnabled = TargetBackgroundTextBox.IsEnabled = ShadowsClippingTextBox.IsEnabled = enabled;
    }
    private void ShowError(string message) => MessageBox.Show(this, message, "XISF Library Workbench", MessageBoxButton.OK, MessageBoxImage.Error);
}