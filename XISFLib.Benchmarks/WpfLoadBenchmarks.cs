using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using XISFLib.WpfViewer;

internal static class WpfLoadBenchmarks
{
    public static void Run(string[] args)
    {
        int iterations = args.Length > 0 ? int.Parse(args[0]) : 5;
        if (iterations < 1) throw new ArgumentOutOfRangeException(nameof(args));
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { RunOnDispatcher(iterations, args.Length > 1 ? args[1] : null); }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start(); thread.Join();
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static void RunOnDispatcher(int iterations, string? output)
    {
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        var results = new List<object>();
        foreach (string path in new[] { "Examples/slow.xisf" }.Where(File.Exists)
            .Concat(Directory.EnumerateFiles("Examples", "*.fits", SearchOption.AllDirectories).Order()))
        {
            var first = new List<double>();
            var complete = new List<double>();
            for (int iteration = -1; iteration < iterations; iteration++)
            {
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                var window = new MainWindow();
                // Exercise the actual click handler and dispatcher without opening a window.
                window.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                ((TextBox)window.FindName("FilePathTextBox")).Text = Path.GetFullPath(path);
                var view = (Image)window.FindName("ImageView");
                var status = (TextBlock)window.FindName("StatusText");
                var busy = typeof(MainWindow).GetField("_isBusy", BindingFlags.NonPublic | BindingFlags.Instance)!;
                var frame = new DispatcherFrame();
                double? ready = null;
                var watch = Stopwatch.StartNew();
                var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(1) };
                timer.Tick += (_, _) =>
                {
                    if (view.Source is not null) ready ??= watch.Elapsed.TotalMilliseconds;
                    if (!(bool)busy.GetValue(window)!) frame.Continue = false;
                    if (watch.Elapsed.TotalSeconds > 60) { timer.Stop(); throw new TimeoutException("WPF load timed out."); }
                };
                timer.Start();
                ((Button)window.FindName("LoadButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Dispatcher.PushFrame(frame);
                watch.Stop(); timer.Stop(); window.Close();
                if (ready is null || !status.Text.StartsWith("Loaded ", StringComparison.Ordinal))
                    throw new InvalidOperationException($"Load failed: {status.Text}");
                if (iteration >= 0) { first.Add(ready.Value); complete.Add(watch.Elapsed.TotalMilliseconds); }
            }
            first.Sort(); complete.Sort();
            double sourceMilliseconds = first[iterations / 2], completeMilliseconds = complete[iterations / 2];
            Console.WriteLine($"{path}: bitmap assigned {sourceMilliseconds:F2} ms; all tabs ready {completeMilliseconds:F2} ms");
            results.Add(new { Path = path, BitmapAssignedMilliseconds = sourceMilliseconds, AllTabsReadyMilliseconds = completeMilliseconds });
        }
        if (output is not null) File.WriteAllText(output, JsonSerializer.Serialize(new
        {
            Runtime = Environment.Version.ToString(), Environment.ProcessorCount, Iterations = iterations,
            Measurement = "Hidden window; time to Image.Source assignment and load completion. Excludes process startup and physical screen presentation.",
            Results = results
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
