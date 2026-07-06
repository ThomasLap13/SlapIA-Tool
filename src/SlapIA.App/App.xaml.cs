using System.IO;
using System.Windows;
using System.Windows.Threading;
using InstallPilot;
using SlapIA.App.Services;
using Velopack;

namespace SlapIA.App;

public partial class App : Application
{
    public static ThemeService ThemeService { get; } = new();

    /// <summary>Set by MainWindow once its TrayIconService exists, so ViewModels (e.g. for
    /// threshold alerts) can show tray balloon notifications without a direct window reference.</summary>
    public static TrayIconService? TrayIcon { get; set; }

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

        // InstallPilot's own brushes (bgBrush, surfaceBrush, ...) must exist before ANY window
        // that uses them can open - including the global Preferences button, which can be
        // clicked before the InstallPilot tab is ever visited.
        I18n.LoadSettings();
        I18n.lang_code = LocalizationService.Instance.CurrentLanguage;
        I18n.theme = ThemeService.CurrentTheme == AppTheme.Dark ? "dark" : "light";
        I18n.ApplyTheme(this);

        // If SlapIA's theme later changes for a reason InstallPilot's own combo didn't trigger
        // (the OS live-switch), keep I18n's brushes/state in sync too.
        ThemeService.ThemeChanged += theme => I18n.SetTheme(theme == AppTheme.Dark ? "dark" : "light");

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
