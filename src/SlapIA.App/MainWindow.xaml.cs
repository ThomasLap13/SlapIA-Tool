using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using SlapIA.App.Services;
using SlapIA.App.ViewModels;

namespace SlapIA.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private readonly TrayIconService _trayIconService;
    private bool _allowClose;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        SourceInitialized += (_, _) =>
            MicaService.Apply(this, App.ThemeService.CurrentTheme == AppTheme.Dark);

        _trayIconService = new TrayIconService(this, ExitFromTray);
        Closing += MainWindow_Closing;
        Closed += (_, _) =>
        {
            _viewModel.Shutdown();
            _trayIconService.Dispose();
        };
    }

    /// <summary>Closing the window (X button) hides it to the tray instead of exiting; only
    /// the tray menu's "Quitter" (<see cref="ExitFromTray"/>) sets <see cref="_allowClose"/>
    /// and lets the close actually go through.</summary>
    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
            return;

        e.Cancel = true;
        Hide();
    }

    private void ExitFromTray()
    {
        _allowClose = true;
        Close();
    }

    private void NavRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string key })
            _viewModel.NavigateByKey(key);
    }
}
