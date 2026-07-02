using System.Windows;
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
    }
}
