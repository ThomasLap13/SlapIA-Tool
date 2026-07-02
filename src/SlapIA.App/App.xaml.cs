using System.IO;
using System.Windows;
using System.Windows.Threading;
using SlapIA.App.Services;
using Velopack;

namespace SlapIA.App;

public partial class App : Application
{
    public static ThemeService ThemeService { get; } = new();

    public App()
    {
        // Must run before anything else: handles install/uninstall/update hooks when this
        // process was launched by the Velopack installer with special arguments.
        VelopackApp.Build().Run();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) => LogCrash(args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) => LogCrash(args.ExceptionObject as Exception);

        // Load the correct Light/Dark dictionary before any window is created, so there's no
        // flash of the wrong theme, then start watching for live OS theme changes.
        ThemeService.Start();

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    private static void LogCrash(Exception? ex)
    {
        if (ex is null)
            return;
        var logPath = Path.Combine(Path.GetTempPath(), "SlapIA.Tool", "crash.log");
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        File.WriteAllText(logPath, $"{DateTime.Now:O}{Environment.NewLine}{ex}");
    }
}
