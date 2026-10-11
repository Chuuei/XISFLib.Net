using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using XISFLib.Viewer;

namespace XISFLib.AvaloniaViewer;

/// <summary>Explicit command-line native-backend publication probe; normal startup never runs it.</summary>
internal static class ViewerSmokeTest
{
    public static async Task<int> RunAsync(MainWindow window, string reportPath)
    {
        string scratch = Path.Combine(Path.GetTempPath(), "xisf-native-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch); Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        var timer = Stopwatch.StartNew(); long allocated = GC.GetTotalAllocatedBytes();
        try
        {
            var vm = (WorkbenchViewModel)window.DataContext!;
            var image = XisfImageData.FromSamples(new byte[] { 10, 20, 30, 40, 50, 60 }, [2, 1], 3, XisfPixelLayout.Interleaved); image.ColorSpace = "RGB";
            image.Properties["Instrument:ExposureTime"] = 30f;
            string source = Path.Combine(scratch, "input.xisf"); XisfWriter.Write(source, [image]);
            vm.FilePath = source; vm.AutoStretch = false; await vm.LoadCommand.ExecuteAsync(null);
            if (vm.SelectedImage is null) throw new InvalidOperationException(vm.Status);
            await vm.Utilities.StatisticsCommand.ExecuteAsync(null); await vm.Utilities.HistogramCommand.ExecuteAsync(null);
            await vm.Utilities.RegionCommand.ExecuteAsync(null);
            vm.PreserveOriginal = false; await window.SaveToPathAsync(Path.Combine(scratch, "output.xisf"));
            await window.SaveToPathAsync(Path.Combine(scratch, "output.fits"));
            if (!File.Exists(Path.Combine(scratch, "output.fits"))) throw new InvalidOperationException(vm.Status);
            for (int tab = 0; tab < 6; tab++)
            {
                window.FindControl<TabControl>("WorkbenchTabs")!.SelectedIndex = tab;
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                window.UpdateLayout();
                using var screenshot = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height), new Vector(96, 96));
                screenshot.Render(window); screenshot.Save(reportPath + $".tab{tab}.png", PngBitmapEncoderOptions.Default);
            }
            File.WriteAllText(reportPath, JsonSerializer.Serialize(new { Success = true, Runtime = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier,
                ElapsedMilliseconds = timer.Elapsed.TotalMilliseconds, AllocatedBytes = GC.GetTotalAllocatedBytes() - allocated,
                Width = window.ClientSize.Width, Height = window.ClientSize.Height, vm.Status }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        catch (Exception error)
        {
            File.WriteAllText(reportPath, JsonSerializer.Serialize(new { Success = false, Error = error.ToString() })); return 1;
        }
        finally { foreach (var file in Directory.GetFiles(scratch)) File.Delete(file); Directory.Delete(scratch); }
    }
}
