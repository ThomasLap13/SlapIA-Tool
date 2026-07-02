using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SlapIA.App.Services;

namespace SlapIA.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public OverviewViewModel Overview { get; }
    public HardwareViewModel Hardware { get; }
    public MonitoringViewModel Monitoring { get; }
    public SoftwareViewModel Software { get; }

    [ObservableProperty] private object? currentView;
    [ObservableProperty] private string currentPageTitle = "Vue d'ensemble";
    [ObservableProperty] private string selectedNavKey = "overview";

    public MainViewModel()
    {
        var systemInfoService = new SystemInfoService();
        Overview = new OverviewViewModel(systemInfoService);
        Hardware = new HardwareViewModel(systemInfoService);
        Monitoring = new MonitoringViewModel(new PerformanceMonitorService());
        Software = new SoftwareViewModel(new InstalledSoftwareService());

        CurrentView = Overview;
        _ = Overview.LoadAsync();
    }

    [RelayCommand]
    private void NavigateOverview()
    {
        Monitoring.Stop();
        CurrentView = Overview;
        CurrentPageTitle = "Vue d'ensemble";
        SelectedNavKey = "overview";
        _ = Overview.LoadAsync();
    }

    [RelayCommand]
    private void NavigateHardware()
    {
        Monitoring.Stop();
        CurrentView = Hardware;
        CurrentPageTitle = "Materiel";
        SelectedNavKey = "hardware";
        _ = Hardware.LoadAsync();
    }

    [RelayCommand]
    private void NavigateMonitoring()
    {
        CurrentView = Monitoring;
        CurrentPageTitle = "Monitoring temps reel";
        SelectedNavKey = "monitoring";
        Monitoring.Start();
    }

    [RelayCommand]
    private void NavigateSoftware()
    {
        Monitoring.Stop();
        CurrentView = Software;
        CurrentPageTitle = "Logiciels installes";
        SelectedNavKey = "software";
        _ = Software.LoadAsync();
    }

    public void Shutdown() => Monitoring.Dispose();
}
