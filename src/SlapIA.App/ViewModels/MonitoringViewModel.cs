using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using SlapIA.App.Models;
using SlapIA.App.Services;
using App = SlapIA.App.App;

namespace SlapIA.App.ViewModels;

public partial class MonitoringViewModel : ObservableObject, IDisposable
{
    private const int MaxPoints = 60;

    // Threshold-based tray alerts: edge-triggered (fires once when crossing up), re-arms once
    // the value drops AlertResetMargin below the threshold again, and never fires more than
    // once per AlertCooldown even if it keeps hovering right at the line.
    private const float CpuTempAlertThreshold = 85f;
    private const float GpuTempAlertThreshold = 85f;
    private const float CpuUsageAlertThreshold = 95f;
    private const float AlertResetMargin = 5f;
    private static readonly TimeSpan AlertCooldown = TimeSpan.FromMinutes(5);

    private static LocalizationService Loc => LocalizationService.Instance;

    private readonly IPerformanceMonitorService _perf;
    private readonly ObservableCollection<double> _cpuValues = new();
    private readonly ObservableCollection<double> _ramValues = new();
    private readonly ObservableCollection<double> _diskValues = new();
    private readonly ObservableCollection<double> _gpuValues = new();

    private bool _cpuTempAlertArmed = true;
    private bool _gpuTempAlertArmed = true;
    private bool _cpuUsageAlertArmed = true;
    private DateTime _lastCpuTempAlert = DateTime.MinValue;
    private DateTime _lastGpuTempAlert = DateTime.MinValue;
    private DateTime _lastCpuUsageAlert = DateTime.MinValue;

    [ObservableProperty] private float cpuUsage;
    [ObservableProperty] private float ramUsage;
    [ObservableProperty] private float ramUsedGB;
    [ObservableProperty] private float ramTotalGB;
    [ObservableProperty] private float diskUsage;
    [ObservableProperty] private float? gpuUsage;
    [ObservableProperty] private float? cpuTemperature;
    [ObservableProperty] private float? gpuTemperature;
    [ObservableProperty] private bool isRunning;
    [ObservableProperty] private bool isCpuTempHelperStarting;

    public bool HasCpuTemperature => CpuTemperature is not null;
    public bool ShowEnableCpuTemp => CpuTemperature is null && !SensorHelperClient.IsHelperRunning;
    public bool ShowCpuTempUnavailable => CpuTemperature is null && !ShowEnableCpuTemp;

    public string RamUsageText => string.Format(Loc["Monitoring_UsedOfTotal"], RamUsedGB, RamTotalGB);

    [ObservableProperty] private ISeries[] series = Array.Empty<ISeries>();
    public Axis[] YAxes { get; } = { new Axis { MinLimit = 0, MaxLimit = 100, Name = "%" } };
    public Axis[] XAxes { get; } = { new Axis { IsVisible = false } };

    public MonitoringViewModel(IPerformanceMonitorService perf)
    {
        _perf = perf;
        BuildLocalizedSeries();
        Loc.PropertyChanged += (_, _) =>
        {
            BuildLocalizedSeries();
            OnPropertyChanged(nameof(RamUsageText));
        };
    }

    /// <summary>(Re)creates the series wrapping the same live value collections, just with
    /// names in the current language - called once at startup and again on language switch,
    /// since LiveChartsCore's legend doesn't refresh from an in-place Name mutation.</summary>
    private void BuildLocalizedSeries()
    {
        Series = new ISeries[]
        {
            BuildSeries(_cpuValues, Loc["Monitoring_ChartCpu"], "#0078D4"),
            BuildSeries(_ramValues, Loc["Monitoring_ChartRam"], "#0F7B0F"),
            BuildSeries(_diskValues, Loc["Monitoring_ChartDisk"], "#CA5010"),
            BuildSeries(_gpuValues, Loc["Monitoring_ChartGpu"], "#8764B8"),
        };
    }

    private static LineSeries<double> BuildSeries(ObservableCollection<double> values, string name, string hexColor) => new()
    {
        Values = values,
        Name = name,
        Fill = null,
        GeometrySize = 0,
        LineSmoothness = 0.3,
        Stroke = new SolidColorPaint(SKColor.Parse(hexColor), 2.5f),
    };

    [RelayCommand]
    private async Task EnableCpuTemperature()
    {
        if (IsCpuTempHelperStarting)
            return;

        IsCpuTempHelperStarting = true;
        try
        {
            SensorHelperClient.TryStartHelper();
            // Give the elevated helper a moment to open the sensor backend and publish its
            // first reading before the next sample tick would otherwise show it as still off.
            await Task.Delay(1500);
        }
        finally
        {
            IsCpuTempHelperStarting = false;
        }
    }

    public void Start()
    {
        if (IsRunning)
            return;

        IsRunning = true;
        _perf.SampleReady += OnSampleReady;
        _perf.Start(TimeSpan.FromSeconds(1));
    }

    public void Stop()
    {
        if (!IsRunning)
            return;

        IsRunning = false;
        _perf.SampleReady -= OnSampleReady;
        _perf.Stop();
    }

    private void OnSampleReady(object? sender, LiveMetricsSample sample)
    {
        Application.Current?.Dispatcher.Invoke(() => ApplySample(sample));
    }

    private void ApplySample(LiveMetricsSample sample)
    {
        CpuUsage = sample.CpuUsagePercent;
        RamUsage = sample.RamUsagePercent;
        RamUsedGB = sample.RamUsedGB;
        RamTotalGB = sample.RamTotalGB;
        DiskUsage = sample.DiskUsagePercent;
        GpuUsage = sample.GpuUsagePercent;
        CpuTemperature = sample.CpuTemperatureC;
        GpuTemperature = sample.GpuTemperatureC;
        OnPropertyChanged(nameof(HasCpuTemperature));
        OnPropertyChanged(nameof(ShowEnableCpuTemp));
        OnPropertyChanged(nameof(ShowCpuTempUnavailable));
        OnPropertyChanged(nameof(RamUsageText));

        AddPoint(_cpuValues, sample.CpuUsagePercent);
        AddPoint(_ramValues, sample.RamUsagePercent);
        AddPoint(_diskValues, sample.DiskUsagePercent);
        AddPoint(_gpuValues, sample.GpuUsagePercent ?? 0);

        CheckAlerts(sample);
    }

    private void CheckAlerts(LiveMetricsSample sample)
    {
        CheckThresholdAlert(sample.CpuTemperatureC, CpuTempAlertThreshold, ref _cpuTempAlertArmed, ref _lastCpuTempAlert,
            value => App.TrayIcon?.ShowBalloon(Loc["Alert_CpuTempTitle"], string.Format(Loc["Alert_CpuTempBody"], value)));

        CheckThresholdAlert(sample.GpuTemperatureC, GpuTempAlertThreshold, ref _gpuTempAlertArmed, ref _lastGpuTempAlert,
            value => App.TrayIcon?.ShowBalloon(Loc["Alert_GpuTempTitle"], string.Format(Loc["Alert_GpuTempBody"], value)));

        CheckThresholdAlert(sample.CpuUsagePercent, CpuUsageAlertThreshold, ref _cpuUsageAlertArmed, ref _lastCpuUsageAlert,
            value => App.TrayIcon?.ShowBalloon(Loc["Alert_CpuUsageTitle"], string.Format(Loc["Alert_CpuUsageBody"], value)));
    }

    private static void CheckThresholdAlert(float? value, float threshold, ref bool armed, ref DateTime lastFired, Action<float> fire)
    {
        if (value is not { } v)
            return;

        if (v >= threshold)
        {
            if (armed && DateTime.UtcNow - lastFired > AlertCooldown)
            {
                fire(v);
                lastFired = DateTime.UtcNow;
                armed = false;
            }
        }
        else if (v <= threshold - AlertResetMargin)
        {
            armed = true;
        }
    }

    private static void AddPoint(ObservableCollection<double> collection, double value)
    {
        collection.Add(value);
        if (collection.Count > MaxPoints)
            collection.RemoveAt(0);
    }

    public void Dispose()
    {
        Stop();
        _perf.Dispose();
        GC.SuppressFinalize(this);
    }
}
