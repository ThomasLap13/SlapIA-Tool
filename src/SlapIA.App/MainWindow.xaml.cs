using System.Windows;
using System.Windows.Controls;
using SlapIA.App.Services;
using SlapIA.App.ViewModels;

namespace SlapIA.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Closed += (_, _) => _viewModel.Shutdown();
        SourceInitialized += (_, _) =>
            MicaService.Apply(this, App.ThemeService.CurrentTheme == AppTheme.Dark);
    }

    private void NavRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string key })
            _viewModel.NavigateByKey(key);
    }
}
