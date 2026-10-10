using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using XISFLib.Viewer;

[assembly: AvaloniaTestApplication(typeof(XISFLib.AvaloniaViewer.Tests.TestAppBuilder))]

namespace XISFLib.AvaloniaViewer.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UseSkia().UseHarfBuzz()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

public class ViewerTests
{
    [AvaloniaFact]
    public void FramebufferUploadPreservesStraightAlphaBgraDpiAndOrientation()
    {
        var image = XisfImageData.FromSamples(new byte[] { 10, 20, 30, 64, 40, 50, 60, 128 }, [2, 1], 4, XisfPixelLayout.Interleaved);
        image.ColorSpace = "RGB"; image.Orientation = "90"; image.Resolution = new(100, 150, "inch");
        var rendered = XisfDisplayRenderer.Render(image);
        using var bitmap = AvaloniaBitmapAdapter.Create(rendered);
        Assert.Equal(new PixelSize(1, 2), bitmap.PixelSize);
        Assert.Equal(new Vector(100, 150), bitmap.Dpi);
        Assert.Equal(AlphaFormat.Unpremul, bitmap.AlphaFormat);
        using var framebuffer = bitmap.Lock();
        var bytes = new byte[rendered.Pixels.Length];
        for (int row = 0; row < rendered.Height; row++)
            Marshal.Copy(framebuffer.Address + row * framebuffer.RowBytes, bytes, row * rendered.Stride, rendered.Stride);
        Assert.Equal(rendered.Pixels, bytes);
    }

    [AvaloniaFact]
    public async Task WindowLoadsXisfThroughSharedCommandsAndClearsPreviewOnUnload()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xisf");
        try
        {
            XisfWriter.Write(path, [XisfImageData.FromSamples(new byte[] { 127 }, [1, 1], 1)]);
            var window = new MainWindow();
            window.Show();
            try
            {
                var vm = Assert.IsType<WorkbenchViewModel>(window.DataContext);
                vm.FilePath = path;
                await vm.LoadCommand.ExecuteAsync(null);
                Assert.StartsWith("Loaded ", vm.Status);
                Assert.IsType<WriteableBitmap>(window.FindControl<Image>("ImageView")!.Source);
                Assert.False(vm.IsBusy);
                await vm.UnloadCommand.ExecuteAsync(null);
                Assert.Null(window.FindControl<Image>("ImageView")!.Source);
            }
            finally { window.Close(); }
        }
        finally { File.Delete(path); }
    }
}
