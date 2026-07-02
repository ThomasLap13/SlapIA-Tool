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

    /// <summary>
    /// Routes to a page by its nav key. RadioButton.IsChecked can become true through paths
    /// that never raise Click (keyboard arrow navigation between grouped radio buttons, UI
    /// Automation/accessibility tools), so the view's Checked event calls this directly
    /// instead of relying solely on the Command bound to Click.
    /// </summary>
    public void NavigateByKey(string key)
    {
        if (key == SelectedNavKey)
            return;

        switch (key)
        {
            case "overview": NavigateOverviewCommand.Execute(null); break;
            case "hardware": NavigateHardwareCommand.Execute(null); break;
            case "monitoring": NavigateMonitoringCommand.Execute(null); break;
            case "software": NavigateSoftwareCommand.Execute(null); break;
        }
    }

    public void Shutdown() => Monitoring.Dispose();
}
