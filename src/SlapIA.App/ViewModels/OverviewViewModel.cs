using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SlapIA.App.Services;

namespace SlapIA.App.ViewModels;

public partial class OverviewViewModel : ObservableObject
{
    private readonly ISystemInfoService _systemInfoService;

    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string computerName = "-";
    [ObservableProperty] private string userName = "-";
    [ObservableProperty] private string osName = "-";
    [ObservableProperty] private string osVersion = "-";
    [ObservableProperty] private string uptime = "-";
    [ObservableProperty] private string processorName = "-";
    [ObservableProperty] private string processorDetails = "-";
    [ObservableProperty] private string memoryTotal = "-";
    [ObservableProperty] private string graphicsCardName = "-";
    [ObservableProperty] private string primaryDisk = "-";
    [ObservableProperty] private string motherboard = "-";

    public OverviewViewModel(ISystemInfoService systemInfoService)
    {
        _systemInfoService = systemInfoService;
    }

    [RelayCommand]
    public async Task LoadAsync(bool forceRefresh = false)
    {
        IsLoading = true;
        try
        {
            var snapshot = await _systemInfoService.GetSnapshotAsync(forceRefresh);

            ComputerName = snapshot.OperatingSystem?.ComputerName ?? "-";
            UserName = snapshot.OperatingSystem?.UserName ?? "-";
            OsName = snapshot.OperatingSystem?.Name ?? "-";
            OsVersion = snapshot.OperatingSystem?.Version ?? "-";
            Uptime = FormatUptime(snapshot.OperatingSystem?.Uptime);
            ProcessorName = snapshot.Processor?.Name ?? "-";
            ProcessorDetails = snapshot.Processor is { } cpu
                ? $"{cpu.Cores} coeurs / {cpu.LogicalProcessors} threads @ {cpu.MaxClockSpeedGHz:0.00} GHz"
                : "-";
            MemoryTotal = snapshot.Memory is { } mem
                ? $"{mem.TotalGB:0.#} Go{(mem.MemoryType is null ? "" : $" ({mem.MemoryType})")}"
                : "-";
            GraphicsCardName = snapshot.GraphicsCards.FirstOrDefault()?.Name ?? "-";

            var mainVolume = snapshot.Volumes.FirstOrDefault();
            PrimaryDisk = mainVolume is not null
                ? $"{mainVolume.FreeGB:0.#} Go libres / {mainVolume.TotalGB:0.#} Go"
                : "-";

            Motherboard = snapshot.MotherboardManufacturer is null
                ? "-"
                : $"{snapshot.MotherboardManufacturer} {snapshot.MotherboardModel}".Trim();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static string FormatUptime(TimeSpan? uptime)
    {
        if (uptime is not { } value || value <= TimeSpan.Zero)
            return "-";
        return value.Days > 0
            ? $"{value.Days} j {value.Hours} h {value.Minutes} min"
            : $"{value.Hours} h {value.Minutes} min";
    }
}
