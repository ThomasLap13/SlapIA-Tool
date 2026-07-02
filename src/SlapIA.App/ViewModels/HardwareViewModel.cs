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
}
