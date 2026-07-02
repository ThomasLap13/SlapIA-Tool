using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using SlapIA.App.Models;
using SlapIA.App.Services;

namespace SlapIA.App.ViewModels;

public partial class MonitoringViewModel : ObservableObject, IDisposable
{
    private const int MaxPoints = 60;

    private readonly IPerformanceMonitorService _perf;
    private readonly ObservableCollection<double> _cpuValues = new();
    private readonly ObservableCollection<double> _ramValues = new();
    private readonly ObservableCollection<double> _diskValues = new();
    private readonly ObservableCollection<double> _gpuValues = new();

    [ObservableProperty] private float cpuUsage;
    [ObservableProperty] private float ramUsage;
    [ObservableProperty] private float ramUsedGB;
    [ObservableProperty] private float ramTotalGB;
    [ObservableProperty] private float diskUsage;
    [ObservableProperty] private float? gpuUsage;
    [ObservableProperty] private bool isRunning;

    public ISeries[] Series { get; }
    public Axis[] YAxes { get; } = { new Axis { MinLimit = 0, MaxLimit = 100, Name = "%" } };
    public Axis[] XAxes { get; } = { new Axis { IsVisible = false } };

    public MonitoringViewModel(IPerformanceMonitorService perf)
    {
        _perf = perf;

        Series = new ISeries[]
        {
            BuildSeries(_cpuValues, "CPU", "#0078D4"),
            BuildSeries(_ramValues, "RAM", "#0F7B0F"),
            BuildSeries(_diskValues, "Disque", "#CA5010"),
            BuildSeries(_gpuValues, "GPU", "#8764B8"),
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

        AddPoint(_cpuValues, sample.CpuUsagePercent);
        AddPoint(_ramValues, sample.RamUsagePercent);
        AddPoint(_diskValues, sample.DiskUsagePercent);
        AddPoint(_gpuValues, sample.GpuUsagePercent ?? 0);
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
