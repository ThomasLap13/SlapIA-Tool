using System.Windows;
using Forms = System.Windows.Forms;

namespace SlapIA.App.Services;

/// <summary>
/// Puts SlapIA Tool in the Windows notification area. The main window hides here instead of
/// closing (wired up by the caller's Closing handler); only the tray menu's "Quitter" (routed
/// through <paramref name="onExitRequested"/> passed to the constructor) actually ends the process.
/// </summary>
public class TrayIconService : IDisposable
{
    private readonly Window _window;
    private readonly Forms.NotifyIcon _notifyIcon;

    public TrayIconService(Window window, Action onExitRequested)
    {
        _window = window;

        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"))!.Stream;
        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = new System.Drawing.Icon(stream),
            Text = "SlapIA Tool",
            Visible = true,
        };
        _notifyIcon.DoubleClick += (_, _) => Restore();

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Ouvrir", null, (_, _) => Restore());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quitter", null, (_, _) => onExitRequested());
        _notifyIcon.ContextMenuStrip = menu;
    }

    private void Restore()
    {
        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        GC.SuppressFinalize(this);
    }
}
