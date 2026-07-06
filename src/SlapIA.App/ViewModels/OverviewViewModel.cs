using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SlapIA.App.Models;
using SlapIA.App.Services;

namespace SlapIA.App.ViewModels;

public partial class OverviewViewModel : ObservableObject
{
    private static LocalizationService Loc => LocalizationService.Instance;

    private readonly ISystemInfoService _systemInfoService;
    private SystemSnapshot? _snapshot;

    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string computerName = "-";
    [ObservableProperty] private string userName = "-";
    [ObservableProperty] private string osName = "-";
    [ObservableProperty] private string osVersion = "-";
    [ObservableProperty] private string uptime = "-";
    [ObservableProperty] private string processorName = "-";
    [ObservableProperty] private string processorDetails = "-";
    [ObservableProperty] private string memoryTotal = "-";
    [ObservableProperty] private string memoryDetails = "-";
    [ObservableProperty] private string graphicsCardName = "-";
    [ObservableProperty] private string graphicsCardDetails = "-";
    [ObservableProperty] private string primaryDisk = "-";
    [ObservableProperty] private string motherboard = "-";

    public OverviewViewModel(ISystemInfoService systemInfoService)
    {
        _systemInfoService = systemInfoService;
        Loc.PropertyChanged += (_, _) => ApplyLocalizedText();
    }

    [RelayCommand]
    public async Task LoadAsync(bool forceRefresh = false)
    {
        IsLoading = true;
        try
        {
            _snapshot = await _systemInfoService.GetSnapshotAsync(forceRefresh);
            ApplyLocalizedText();
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>(Re)formats every display string from the last-fetched snapshot. Called after
    /// a load and again whenever the UI language changes, so labels never go stale.</summary>
    private void ApplyLocalizedText()
    {
        var snapshot = _snapshot;
        if (snapshot is null)
            return;

        ComputerName = snapshot.OperatingSystem?.ComputerName ?? "-";
        UserName = snapshot.OperatingSystem?.UserName ?? "-";
        OsName = snapshot.OperatingSystem?.Name ?? "-";
        OsVersion = snapshot.OperatingSystem?.Version ?? "-";
        Uptime = FormatUptime(snapshot.OperatingSystem?.Uptime);
        ProcessorName = snapshot.Processor?.Name ?? "-";
        ProcessorDetails = snapshot.Processor is { } cpu
            ? string.Format(Loc["Hardware_CoresThreads"], cpu.Cores, cpu.LogicalProcessors, cpu.MaxClockSpeedGHz)
            : "-";
        MemoryTotal = snapshot.Memory is { } mem
            ? mem.MemoryType is null
                ? string.Format(Loc["Overview_MemoryTotalPlain"], mem.TotalGB)
                : string.Format(Loc["Overview_MemoryTotalWithType"], mem.TotalGB, mem.MemoryType)
            : "-";
        MemoryDetails = snapshot.Memory is { } memDetails
            ? string.Format(Loc["Overview_MemoryModules"], memDetails.ModuleCount)
                + (memDetails.Manufacturer is { } man ? $" - {man}" : "")
                + (memDetails.SpeedMHz is { } speed ? $" @ {speed} MHz" : "")
            : "-";

        var primaryGpu = snapshot.GraphicsCards.FirstOrDefault();
        GraphicsCardName = primaryGpu?.Name ?? "-";
        GraphicsCardDetails = primaryGpu?.VramGB is { } vram ? string.Format(Loc["Hardware_VramFormat"], vram) : "-";

        var mainVolume = snapshot.Volumes.FirstOrDefault();
        PrimaryDisk = mainVolume is not null
            ? string.Format(Loc["Overview_FreeOf"], mainVolume.FreeGB, mainVolume.TotalGB)
            : "-";

        Motherboard = snapshot.MotherboardManufacturer is null
            ? "-"
            : $"{snapshot.MotherboardManufacturer} {snapshot.MotherboardModel}".Trim();
    }

    private static string FormatUptime(TimeSpan? uptime)
    {
        if (uptime is not { } value || value <= TimeSpan.Zero)
            return "-";
        return value.Days > 0
            ? string.Format(Loc["Overview_Uptime_Days"], value.Days, value.Hours, value.Minutes)
            : string.Format(Loc["Overview_Uptime_Hours"], value.Hours, value.Minutes);
    }
}
