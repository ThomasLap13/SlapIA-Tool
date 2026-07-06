using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SlapIA.App.Models;
using SlapIA.App.Services;

namespace SlapIA.App.ViewModels;

public partial class SoftwareViewModel : ObservableObject
{
    private static LocalizationService Loc => LocalizationService.Instance;

    private readonly IInstalledSoftwareService _softwareService;
    private List<InstalledApplication> _allApps = new();

    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string searchText = "";
    [ObservableProperty] private ObservableCollection<InstalledApplication> applications = new();
    [ObservableProperty] private int totalCount;

    public string CountText => string.Format(Loc["Software_Count"], Applications.Count, TotalCount);

    public SoftwareViewModel(IInstalledSoftwareService softwareService)
    {
        _softwareService = softwareService;
        Loc.PropertyChanged += (_, _) => OnPropertyChanged(nameof(CountText));
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (_allApps.Count > 0)
            return;

        IsLoading = true;
        try
        {
            _allApps = (await _softwareService.GetInstalledApplicationsAsync()).ToList();
            TotalCount = _allApps.Count;
            ApplyFilter();
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnApplicationsChanged(ObservableCollection<InstalledApplication> value) => OnPropertyChanged(nameof(CountText));

    partial void OnTotalCountChanged(int value) => OnPropertyChanged(nameof(CountText));

    private void ApplyFilter()
    {
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _allApps
            : _allApps.Where(a =>
                a.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                (a.Publisher?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();

        Applications = new ObservableCollection<InstalledApplication>(filtered);
    }
}
