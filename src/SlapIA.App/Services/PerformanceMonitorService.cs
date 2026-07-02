using System.Diagnostics;
using System.Runtime.InteropServices;
using SlapIA.App.Models;
using Timer = System.Timers.Timer;

namespace SlapIA.App.Services;

/// <summary>
/// Samples live CPU / RAM / disk / GPU usage on a timer and raises <see cref="SampleReady"/>
/// from a background thread. GPU sampling degrades to null when the "GPU Engine" performance
/// counter category isn't available (older Windows versions, some driver setups).
/// </summary>
public class PerformanceMonitorService : IPerformanceMonitorService
{
    public event EventHandler<LiveMetricsSample>? SampleReady;

    private readonly PerformanceCounter _cpuCounter = new("Processor", "% Processor Time", "_Total");
    private readonly PerformanceCounter _diskCounter = new("PhysicalDisk", "% Disk Time", "_Total");
    private readonly List<PerformanceCounter>? _gpuCounters;
    private Timer? _timer;

    public PerformanceMonitorService()
    {
        // The first NextValue() call always returns 0; discard it now so the first sample
        // shown in the UI is meaningful.
        SafeNextValue(_cpuCounter);
        SafeNextValue(_diskCounter);

        _gpuCounters = TryCreateGpuCounters();
        _gpuCounters?.ForEach(c => SafeNextValue(c));
    }

    public void Start(TimeSpan interval)
    {
        if (_timer is not null)
            return;

        _timer = new Timer(interval.TotalMilliseconds) { AutoReset = true };
        _timer.Elapsed += (_, _) => Sample();
        _timer.Start();
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
    }

    private void Sample()
    {
        var cpu = SafeNextValue(_cpuCounter);
        var disk = Math.Min(100f, SafeNextValue(_diskCounter));
        var (totalRamGb, usedRamGb, ramPercent) = ReadMemory();
        var gpu = ReadGpuUsage();

        SampleReady?.Invoke(this, new LiveMetricsSample
        {
            Timestamp = DateTime.Now,
            CpuUsagePercent = cpu,
            RamUsagePercent = ramPercent,
            RamUsedGB = usedRamGb,
            RamTotalGB = totalRamGb,
            DiskUsagePercent = disk,
            GpuUsagePercent = gpu,
        });
    }

    private static float SafeNextValue(PerformanceCounter counter)
    {
        try { return counter.NextValue(); }
        catch { return 0f; }
    }

    private static List<PerformanceCounter>? TryCreateGpuCounters()
    {
        try
        {
            if (!PerformanceCounterCategory.Exists("GPU Engine"))
                return null;

            var category = new PerformanceCounterCategory("GPU Engine");
            var instances = category.GetInstanceNames()
                .Where(n => n.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var counters = new List<PerformanceCounter>();
            foreach (var instance in instances)
            {
                try { counters.Add(new PerformanceCounter("GPU Engine", "Utilization Percentage", instance, true)); }
                catch { /* instance can disappear between enumeration and creation */ }
            }
            return counters.Count > 0 ? counters : null;
        }
        catch
        {
            return null;
        }
    }

    private float? ReadGpuUsage()
    {
        if (_gpuCounters is null || _gpuCounters.Count == 0)
            return null;

        try
        {
            var total = _gpuCounters.Sum(SafeNextValue);
            return Math.Clamp(total, 0f, 100f);
        }
        catch
        {
            return null;
        }
    }

    private static (float totalGb, float usedGb, float percent) ReadMemory()
    {
        var status = new MEMORYSTATUSEX();
        if (!GlobalMemoryStatusEx(status))
            return (0, 0, 0);

        var totalGb = status.ullTotalPhys / 1024f / 1024 / 1024;
        var availGb = status.ullAvailPhys / 1024f / 1024 / 1024;
        var usedGb = totalGb - availGb;
        var percent = totalGb > 0 ? usedGb / totalGb * 100f : 0f;
        return (totalGb, usedGb, percent);
    }

    public void Dispose()
    {
        Stop();
        _cpuCounter.Dispose();
        _diskCounter.Dispose();
        _gpuCounters?.ForEach(c => c.Dispose());
        GC.SuppressFinalize(this);
    }

    [StructLayout(LayoutKind.Sequential)]
    private class MEMORYSTATUSEX
    {
        public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);
}
