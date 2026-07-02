using System.IO;
using System.Windows;
using System.Windows.Threading;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace SlapIA.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) => LogCrash(args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) => LogCrash(args.ExceptionObject as Exception);

        ApplicationThemeManager.Apply(ApplicationTheme.Light, WindowBackdropType.None, updateAccent: false);

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
