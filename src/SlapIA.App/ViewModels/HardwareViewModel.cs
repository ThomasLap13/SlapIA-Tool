using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SlapIA.App.Models;
using SlapIA.App.Services;

namespace SlapIA.App.ViewModels;

public partial class HardwareViewModel : ObservableObject
{
    private static LocalizationService Loc => LocalizationService.Instance;

    private readonly ISystemInfoService _systemInfoService;

    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private SystemSnapshot? snapshot;

    public HardwareViewModel(ISystemInfoService systemInfoService)
    {
        _systemInfoService = systemInfoService;
        Loc.PropertyChanged += (_, _) => RaiseLocalizedTextChanged();
    }

    private void RaiseLocalizedTextChanged()
    {
        OnPropertyChanged(nameof(ProcessorDetailsText));
        OnPropertyChanged(nameof(MemoryTotalText));
        OnPropertyChanged(nameof(MemoryDetailsText));
        OnPropertyChanged(nameof(BiosLineText));
        OnPropertyChanged(nameof(ProcessorCopyText));
        OnPropertyChanged(nameof(MemoryCopyText));
        OnPropertyChanged(nameof(MotherboardCopyText));
    }

    public string ProcessorDetailsText => Snapshot?.Processor is { } cpu
        ? string.Format(Loc["Hardware_CoresThreads"], cpu.Cores, cpu.LogicalProcessors, cpu.MaxClockSpeedGHz)
        : "";

    public string MemoryTotalText => Snapshot?.Memory is { } mem
        ? string.Format(Loc["Overview_MemoryTotalPlain"], mem.TotalGB)
        : "";

    public string MemoryDetailsText => Snapshot?.Memory is { } mem
        ? string.Format(Loc["Hardware_MemoryLine"], mem.ModuleCount, mem.Manufacturer ?? "-", mem.MemoryType ?? "-", mem.SpeedMHz?.ToString() ?? "-")
        : "";

    public string BiosLineText => Snapshot is null ? "" : Loc["Hardware_Bios"] + Snapshot.BiosVersion;

    [RelayCommand]
    public async Task LoadAsync(bool forceRefresh = false)
    {
        if (Snapshot is not null && !forceRefresh)
            return;

        IsLoading = true;
        try
        {
            Snapshot = await _systemInfoService.GetSnapshotAsync(forceRefresh);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private Task Refresh() => LoadAsync(forceRefresh: true);

    [RelayCommand]
    private void CopyToClipboard(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        Clipboard.SetText(text);
    }

    partial void OnSnapshotChanged(SystemSnapshot? value)
    {
        RaiseLocalizedTextChanged();
        OnPropertyChanged(nameof(MotherboardCopyText));
    }

    public string ProcessorCopyText => Snapshot?.Processor is { } cpu
        ? $"{cpu.Name}\n{string.Format(Loc["Hardware_CoresThreads"], cpu.Cores, cpu.LogicalProcessors, cpu.MaxClockSpeedGHz)}"
        : "";

    public string MemoryCopyText => Snapshot?.Memory is { } mem
        ? $"{string.Format(Loc["Overview_MemoryTotalPlain"], mem.TotalGB)}{(mem.MemoryType is null ? "" : $" {mem.MemoryType}")}{(mem.Manufacturer is null ? "" : $" {mem.Manufacturer}")} - {string.Format(Loc["Overview_MemoryModules"], mem.ModuleCount)}{(mem.SpeedMHz is { } s ? $" @ {s} MHz" : "")}"
        : "";

    public string MotherboardCopyText => Snapshot is null
        ? ""
        : $"{Snapshot.MotherboardManufacturer} {Snapshot.MotherboardModel}\n{Loc["Hardware_Bios"]}{Snapshot.BiosVersion}";
}
