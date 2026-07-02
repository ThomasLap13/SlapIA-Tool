using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SlapIA.App.Services;

namespace SlapIA.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IAppUpdateService _updateService;

    public OverviewViewModel Overview { get; }
    public HardwareViewModel Hardware { get; }
    public MonitoringViewModel Monitoring { get; }
    public SoftwareViewModel Software { get; }

    [ObservableProperty] private object? currentView;
    [ObservableProperty] private string currentPageTitle = "Vue d'ensemble";
    [ObservableProperty] private string selectedNavKey = "overview";
    [ObservableProperty] private string updateStatus = "";
    [ObservableProperty] private bool isCheckingForUpdate;

    public string AppVersion => Assembly.GetExecutingAssembly().GetName().Version is { } v
        ? $"v{v.Major}.{v.Minor}.{v.Build}"
        : "";

    public MainViewModel() : this(new AppUpdateService())
    {
    }

    public MainViewModel(IAppUpdateService updateService)
    {
        _updateService = updateService;

        var systemInfoService = new SystemInfoService();
        Overview = new OverviewViewModel(systemInfoService);
        Hardware = new HardwareViewModel(systemInfoService);
        Monitoring = new MonitoringViewModel(new PerformanceMonitorService());
        Software = new SoftwareViewModel(new InstalledSoftwareService());

        CurrentView = Overview;
        _ = Overview.LoadAsync();
    }

    [RelayCommand]
    private async Task CheckForUpdates()
    {
        if (IsCheckingForUpdate)
            return;

        IsCheckingForUpdate = true;
        UpdateStatus = "Recherche de mises a jour...";
        try
        {
            if (!_updateService.IsInstalled)
            {
                UpdateStatus = "Mise a jour disponible uniquement pour la version installee (setup.exe).";
                return;
            }

            var info = await _updateService.CheckForUpdatesAsync();
            if (info is null)
            {
                UpdateStatus = "Vous utilisez deja la derniere version.";
                return;
            }

            UpdateStatus = $"Telechargement de la version {info.TargetFullRelease.Version}...";
            await _updateService.DownloadAndApplyAsync(info, progress => UpdateStatus = $"Telechargement... {progress}%");
            // On success, ApplyUpdatesAndRestart terminates this process - code below rarely runs.
        }
        catch (Exception ex)
        {
            UpdateStatus = $"Echec de la mise a jour : {ex.Message}";
        }
        finally
        {
            IsCheckingForUpdate = false;
        }
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
