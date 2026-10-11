using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace XISFLib.AvaloniaViewer;

public sealed partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow(); desktop.MainWindow = window;
            if (desktop.Args is { Length: 2 } args && args[0] == "--smoke-test")
            {
                desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnExplicitShutdown;
                window.Opened += async (_, _) => desktop.Shutdown(await ViewerSmokeTest.RunAsync(window, args[1]));
            }
            else if (desktop.Args is { Length: 4 } benchmark && benchmark[0] == "--benchmark")
            {
                desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnExplicitShutdown;
                window.Opened += async (_, _) => desktop.Shutdown(await ViewerLoadBenchmark.RunAsync(window,
                    int.TryParse(benchmark[1], out int count) ? count : 0, benchmark[2], benchmark[3]));
            }
        }
        base.OnFrameworkInitializationCompleted();
    }
}
