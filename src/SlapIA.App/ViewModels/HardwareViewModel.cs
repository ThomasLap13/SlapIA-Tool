using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SlapIA.App.Models;
using SlapIA.App.Services;

namespace SlapIA.App.ViewModels;

public partial class HardwareViewModel : ObservableObject
{
    private readonly ISystemInfoService _systemInfoService;

    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private SystemSnapshot? snapshot;

    public HardwareViewModel(ISystemInfoService systemInfoService)
    {
        _systemInfoService = systemInfoService;
    }

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
        OnPropertyChanged(nameof(ProcessorCopyText));
        OnPropertyChanged(nameof(MemoryCopyText));
        OnPropertyChanged(nameof(GraphicsCopyText));
        OnPropertyChanged(nameof(DisksCopyText));
        OnPropertyChanged(nameof(NetworkCopyText));
        OnPropertyChanged(nameof(MotherboardCopyText));
    }

    public string ProcessorCopyText => Snapshot?.Processor is { } cpu
        ? $"{cpu.Name}\n{cpu.Cores} coeurs / {cpu.LogicalProcessors} threads @ {cpu.MaxClockSpeedGHz:0.00} GHz"
        : "";

    public string MemoryCopyText => Snapshot?.Memory is { } mem
        ? $"{mem.TotalGB:0.#} Go{(mem.MemoryType is null ? "" : $" {mem.MemoryType}")} - {mem.ModuleCount} barrette(s){(mem.SpeedMHz is { } s ? $" @ {s} MHz" : "")}"
        : "";

    public string GraphicsCopyText => Snapshot is null
        ? ""
        : string.Join(Environment.NewLine, Snapshot.GraphicsCards.Select(g =>
            $"{g.Name}{(g.VramGB is { } v ? $" - {v:0.#} Go VRAM" : "")}"));

    public string DisksCopyText => Snapshot is null
        ? ""
        : string.Join(Environment.NewLine, Snapshot.Disks.Select(d =>
            $"{d.Model} - {d.SizeGB:0.#} Go - {d.MediaType} - {d.InterfaceType}"));

    public string NetworkCopyText => Snapshot is null
        ? ""
        : string.Join(Environment.NewLine, Snapshot.NetworkAdapters.Select(n =>
            $"{n.Name} - {n.IPv4Address ?? "-"} - {n.MacAddress ?? "-"}"));

    public string MotherboardCopyText => Snapshot is null
        ? ""
        : $"{Snapshot.MotherboardManufacturer} {Snapshot.MotherboardModel}\nBIOS {Snapshot.BiosVersion}";
}
