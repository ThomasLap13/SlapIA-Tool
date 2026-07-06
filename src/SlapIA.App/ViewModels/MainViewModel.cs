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
    [ObservableProperty] private string selectedNavKey = "overview";
    [ObservableProperty] private string updateStatus = "";
    [ObservableProperty] private bool isCheckingForUpdate;

    public string AppVersion => Assembly.GetExecutingAssembly().GetName().Version is { } v
        ? $"v{v.Major}.{v.Minor}.{v.Build}"
        : "";

    /// <summary>Computed (not stored) so it stays correct across both navigation and live
    /// language switches - see the LocalizationService subscription below.</summary>
    public string CurrentPageTitle => SelectedNavKey switch
    {
        "hardware" => LocalizationService.Instance["Page_Hardware"],
        "monitoring" => LocalizationService.Instance["Page_Monitoring"],
        "software" => LocalizationService.Instance["Page_Software"],
        _ => LocalizationService.Instance["Page_Overview"],
    };

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

        LocalizationService.Instance.PropertyChanged += (_, _) => OnPropertyChanged(nameof(CurrentPageTitle));

        CurrentView = Overview;
        _ = Overview.LoadAsync();
    }

    partial void OnSelectedNavKeyChanged(string value) => OnPropertyChanged(nameof(CurrentPageTitle));

    [RelayCommand]
    private async Task CheckForUpdates()
    {
        if (IsCheckingForUpdate)
            return;

        IsCheckingForUpdate = true;
        UpdateStatus = LocalizationService.Instance["Update_Checking"];
        try
        {
            if (!_updateService.IsInstalled)
            {
                UpdateStatus = LocalizationService.Instance["Update_OnlyInstalled"];
                return;
            }

            var info = await _updateService.CheckForUpdatesAsync();
            if (info is null)
            {
                UpdateStatus = LocalizationService.Instance["Update_AlreadyLatest"];
                return;
            }

            UpdateStatus = string.Format(LocalizationService.Instance["Update_Downloading"], info.TargetFullRelease.Version);
            await _updateService.DownloadAndApplyAsync(info, progress =>
                UpdateStatus = string.Format(LocalizationService.Instance["Update_DownloadingProgress"], progress));
            // On success, ApplyUpdatesAndRestart terminates this process - code below rarely runs.
        }
        catch (Exception ex)
        {
            UpdateStatus = string.Format(LocalizationService.Instance["Update_Failed"], ex.Message);
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
        SelectedNavKey = "overview";
        _ = Overview.LoadAsync();
    }

    [RelayCommand]
    private void NavigateHardware()
    {
        Monitoring.Stop();
        CurrentView = Hardware;
        SelectedNavKey = "hardware";
        _ = Hardware.LoadAsync();
    }

    [RelayCommand]
    private void NavigateMonitoring()
    {
        CurrentView = Monitoring;
        SelectedNavKey = "monitoring";
        Monitoring.Start();
    }

    [RelayCommand]
    private void NavigateSoftware()
    {
        Monitoring.Stop();
        CurrentView = Software;
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
