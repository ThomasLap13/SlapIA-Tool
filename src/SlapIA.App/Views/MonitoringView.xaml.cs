using System.Windows.Controls;
using System.Windows.Input;
using SlapIA.App.ViewModels;

namespace SlapIA.App.Views;

public partial class MonitoringView : UserControl
{
    public MonitoringView()
    {
        InitializeComponent();
    }

    private void EnableCpuTemp_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MonitoringViewModel vm)
            vm.EnableCpuTemperatureCommand.Execute(null);
    }
}
