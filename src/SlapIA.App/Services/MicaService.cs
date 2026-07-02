using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace SlapIA.App.Services;

/// <summary>
/// Enables the Windows 11 Mica backdrop on a window directly via DWM (no dependency on
/// Wpf.Ui's FluentWindow, which was found to render blank in this environment). When it
/// succeeds, the app/sidebar background brushes are given a little transparency so the
/// backdrop material shows through, matching the "Company Portal" look.
/// </summary>
public static class MicaService
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const int DWMSBT_MAINWINDOW = 2; // Mica

    private static bool _isEnabled;

    /// <summary>Call once the window's handle exists (e.g. in a Loaded/SourceInitialized handler).</summary>
    public static bool Apply(Window window, bool darkTitleBar)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).EnsureHandle();

            int useDark = darkTitleBar ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));

            int backdrop = DWMSBT_MAINWINDOW;
            var hr = DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));

            _isEnabled = hr == 0;
            if (_isEnabled)
                ReapplyBackgroundTransparency();
            return _isEnabled;
        }
        catch
        {
            _isEnabled = false;
            return false;
        }
    }

    public static void SetTitleBarTheme(Window window, bool dark)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).EnsureHandle();
            int useDark = dark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
        }
        catch
        {
            // Best-effort only.
        }
    }

    /// <summary>
    /// Re-applies the background transparency to whatever theme dictionary is currently
    /// merged (called after a theme switch swaps in a fresh, fully-opaque dictionary).
    /// </summary>
    public static void ReapplyBackgroundTransparency()
    {
        if (!_isEnabled)
            return;

        SetBrushAlpha("AppBackgroundBrush", 0xF0);
        SetBrushAlpha("SidebarBrush", 0xE6);
    }

    private static void SetBrushAlpha(string key, byte alpha)
    {
        var app = Application.Current;
        if (app is null || app.Resources.MergedDictionaries.Count == 0)
            return;

        // Mutate the brush inside the merged theme dictionary itself (not the top-level
        // Application.Resources) so a later theme switch - which replaces the merged
        // dictionary wholesale with a pristine, fully-opaque copy - isn't shadowed by a
        // stale override left behind from the previous theme.
        var themeDict = app.Resources.MergedDictionaries[0];
        if (themeDict[key] is not SolidColorBrush brush)
            return;

        var color = brush.Color;
        color.A = alpha;
        themeDict[key] = new SolidColorBrush(color);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);
}
