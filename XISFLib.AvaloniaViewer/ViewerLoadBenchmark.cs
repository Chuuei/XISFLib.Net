using System.Text.Json;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Media.Imaging;
using Avalonia;
using XISFLib.Viewer;

namespace XISFLib.AvaloniaViewer;

internal static class ViewerLoadBenchmark
{
    public static async Task<int> RunAsync(MainWindow window, int iterations, string output, string examples)
    {
        try
        {
            if (iterations < 1) throw new ArgumentOutOfRangeException(nameof(iterations));
            var vm = (WorkbenchViewModel)window.DataContext!;
            var rows = new List<object>();
            var slow = Path.Combine(examples, "slow.xisf");
            var files = File.Exists(slow) ? new[] { slow } : Directory.GetFiles(examples, "*.xisf").Order().ToArray();
            foreach (var path in files.Concat(Directory.GetFiles(examples, "*.fits")))
            {
                vm.FilePath = path; await vm.LoadCommand.ExecuteAsync(null); // warm runtime/codec/file cache
                for (int i = 0; i < iterations; i++)
                {
                    await vm.LoadCommand.ExecuteAsync(null);
                    if (!vm.Status.StartsWith("Loaded ", StringComparison.Ordinal)) throw new InvalidOperationException(vm.Status);
                    var metadataTimer = Stopwatch.StartNew();
                    window.FindControl<TabControl>("WorkbenchTabs")!.SelectedIndex = 3;
                    await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                    window.UpdateLayout();
                    double metadataLayoutMilliseconds = metadataTimer.Elapsed.TotalMilliseconds;
                    var grid = window.FindControl<DataGrid>("ImagePropertiesGrid")!;
                    var largeRow = vm.ImageProperties.FirstOrDefault(r => r.Type == "String" && r.RequiresValueDialog) ?? vm.ImageProperties.LastOrDefault();
                    metadataTimer.Restart();
                    for (int scroll = 0; scroll < 20 && vm.ImageProperties.Count > 0; scroll++)
                    {
                        grid.ScrollIntoView(scroll % 2 == 0 ? vm.ImageProperties.First() : largeRow, grid.Columns[2]);
                        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                        window.UpdateLayout();
                    }
                    double scrollingMilliseconds = metadataTimer.Elapsed.TotalMilliseconds;
                    int longestDisplayedText = grid.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text?.Length ?? 0).DefaultIfEmpty().Max();
                    rows.Add(new { File = path, Iteration = i, Timing = vm.LastLoadTiming, MetadataLayoutMilliseconds = metadataLayoutMilliseconds,
                        TwentyScrollsMilliseconds = scrollingMilliseconds, LongestDisplayedText = longestDisplayedText });
                    if (i == 0)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
                        using var screenshot = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height), new Vector(96, 96));
                        screenshot.Render(window); screenshot.Save(output + "." + Path.GetFileNameWithoutExtension(path) + ".metadata.png", PngBitmapEncoderOptions.Default);
                    }
                    await vm.UnloadCommand.ExecuteAsync(null); vm.FilePath = path;
                }
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
            File.WriteAllText(output, JsonSerializer.Serialize(new { Method = "Warm cache; actual Avalonia native bitmap transfer. Allocation includes all process threads. Screen presentation and native allocation are not measured.", Rows = rows }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        catch (Exception error)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!); File.WriteAllText(output, error.ToString()); return 1;
        }
    }
}
