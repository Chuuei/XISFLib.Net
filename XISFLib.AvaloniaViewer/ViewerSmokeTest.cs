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
            const int qualityWidth = 256, qualityHeight = 192;
            var random = new Random(42);
            var qualitySamples = new float[qualityWidth * qualityHeight];
            for (int y = 0; y < qualityHeight; y++)
            for (int x = 0; x < qualityWidth; x++)
            {
                double noise = 0.001 * Math.Sqrt(-2 * Math.Log(1 - random.NextDouble())) * Math.Cos(2 * Math.PI * random.NextDouble());
                double stars = 0;
                foreach (var (sx, sy) in new[] { (40, 40), (127, 90), (200, 140) })
                    stars += 0.3 * Math.Exp(-((x - sx) * (x - sx) + (y - sy) * (y - sy)) / 4.5);
                qualitySamples[y * qualityWidth + x] = (float)(0.1 + noise + stars);
            }
            string qualitySource = Path.Combine(scratch, "quality.xisf");
            XisfWriter.Write(qualitySource, [XisfImageData.FromSamples(qualitySamples, [qualityWidth, qualityHeight], 1)]);
            vm.FilePath = qualitySource; await vm.LoadCommand.ExecuteAsync(null);
            await vm.Quality.AnalyzeCommand.ExecuteAsync(null);
            if (vm.Quality.Entries.Single(e => e.Name == "Detected star candidates").Value != "3") throw new InvalidOperationException("Native quality analysis failed to detect synthetic stars.");
            window.FindControl<TabControl>("WorkbenchTabs")!.SelectedItem = window.FindControl<TabItem>("FinishTab");
            int progressViews = 0;
            void CaptureProgress(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
            {
                if (e.PropertyName != nameof(ViewerFinishState.IsProcessing) || !vm.Finish.IsProcessing) return;
                window.UpdateLayout();
                if (!window.FindControl<StackPanel>("FinishProgressPanel")!.IsVisible)
                    throw new InvalidOperationException("Native finish progress is not visible.");
                if (!window.FindControl<Button>("CancelFinishedButton")!.IsEffectivelyEnabled)
                    throw new InvalidOperationException("Native finish cancellation is not available.");
                using var screenshot = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height), new Vector(96, 96));
                screenshot.Render(window); screenshot.Save(reportPath + $".progress{progressViews++}.png", PngBitmapEncoderOptions.Default);
            }
            vm.Finish.PropertyChanged += CaptureProgress;
            await vm.Finish.CreateCommand.ExecuteAsync(null);
            if (!vm.Finish.HasPicture) throw new InvalidOperationException(vm.Status);
            await vm.SaveFinishedAsync(Path.Combine(scratch, "finished.png"));
            await vm.SaveFinishedAsync(Path.Combine(scratch, "finished.jpg"));
            vm.Finish.PropertyChanged -= CaptureProgress;
            if (progressViews != 3 || vm.Finish.IsProcessing) throw new InvalidOperationException("Unexpected finish progress lifecycle.");
            using (var finished = new ImageMagick.MagickImage(Path.Combine(scratch, "finished.png")))
                if (finished.Width != qualityWidth - 4 || finished.Height != qualityHeight) throw new InvalidOperationException("Unexpected finished picture crop.");
            for (int tab = 0; tab < window.FindControl<TabControl>("WorkbenchTabs")!.ItemCount; tab++)
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
