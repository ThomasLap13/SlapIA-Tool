using System.Windows;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace SlapIA.App.Services;

public enum AppTheme
{
    Light,
    Dark,
}

/// <summary>
/// Applies the Light/Dark resource dictionary and keeps it in sync with the Windows
/// "Choisir votre mode" setting, live, with a short cross-fade so the switch isn't jarring.
/// </summary>
public class ThemeService : IDisposable
{
    public AppTheme CurrentTheme { get; private set; }

    public ThemeService()
    {
        CurrentTheme = DetectSystemTheme();
    }

    /// <summary>Applies the current theme immediately (no animation) and starts watching for OS changes.</summary>
    public void Start()
    {
        ApplyResources(CurrentTheme);
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        GC.SuppressFinalize(this);
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General)
            return;

        var detected = DetectSystemTheme();
        if (detected == CurrentTheme)
            return;

        CurrentTheme = detected;
        Application.Current?.Dispatcher.Invoke(() => AnimateThemeChange(detected));
    }

    private static void AnimateThemeChange(AppTheme theme)
    {
        var window = Application.Current?.MainWindow;
        if (window is null)
        {
            ApplyResources(theme);
            return;
        }

        var fadeOut = new DoubleAnimation(window.Opacity, 0.15, TimeSpan.FromMilliseconds(160))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
        };
        fadeOut.Completed += (_, _) =>
        {
            ApplyResources(theme);
            MicaService.SetTitleBarTheme(window, theme == AppTheme.Dark);
            var fadeIn = new DoubleAnimation(0.15, 1.0, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };
            window.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        };
        window.BeginAnimation(UIElement.OpacityProperty, fadeOut);
    }

    private static void ApplyResources(AppTheme theme)
    {
        var app = Application.Current;
        if (app is null)
            return;

        var uri = new Uri(theme == AppTheme.Dark ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative);
        app.Resources.MergedDictionaries[0] = new ResourceDictionary { Source = uri };
        MicaService.ReapplyBackgroundTransparency();

        // Keep WPF-UI's own controls (ui:Button, ui:TextBox, ...) in sync.
        ApplicationThemeManager.Apply(
            theme == AppTheme.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light,
            WindowBackdropType.None,
            updateAccent: false);

        // ApplicationThemeManager.Apply() re-injects WPF-UI's own (barely-rounded) default,
        // so re-assert ours afterwards to get the more pronounced Windows 11 rounding on
        // ui:Button / ui:TextBox / etc.
        app.Resources["ControlCornerRadius"] = new CornerRadius(8);
    }

    public static AppTheme DetectSystemTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int value)
                return value == 0 ? AppTheme.Dark : AppTheme.Light;
        }
        catch
        {
            // Registry key can be absent on locked-down systems; default to light.
        }
        return AppTheme.Light;
    }
}
