using System.Diagnostics;
using System.Runtime.InteropServices;
using LibreHardwareMonitor.Hardware;
using SlapIA.App.Models;
using Timer = System.Timers.Timer;

namespace SlapIA.App.Services;

/// <summary>
/// Samples live CPU / RAM / disk / GPU usage and CPU/GPU temperatures on a timer and raises
/// <see cref="SampleReady"/> from a background thread. Any individual metric degrades to
/// null/0 when its data source isn't available (older Windows, missing sensors, no admin
/// rights for temperatures) rather than failing the whole sample.
/// </summary>
public class PerformanceMonitorService : IPerformanceMonitorService
{
    public event EventHandler<LiveMetricsSample>? SampleReady;

    private readonly PerformanceCounter _cpuCounter = new("Processor", "% Processor Time", "_Total");
    private readonly List<PerformanceCounter> _diskCounters;
    private readonly List<PerformanceCounter>? _gpuCounters;
    private readonly Computer? _sensorComputer;
    private Timer? _timer;

    public PerformanceMonitorService()
    {
        // The first NextValue() call always returns 0; discard it now so the first sample
        // shown in the UI is meaningful.
        SafeNextValue(_cpuCounter);

        _diskCounters = CreateDiskCounters();
        _diskCounters.ForEach(c => SafeNextValue(c));

        _gpuCounters = TryCreateGpuCounters();
        _gpuCounters?.ForEach(c => SafeNextValue(c));

        _sensorComputer = TryOpenSensorComputer();
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
        var disk = ReadDiskUsage();
        var (totalRamGb, usedRamGb, ramPercent) = ReadMemory();
        var gpu = ReadGpuUsage();
        var (cpuTemp, gpuTemp) = ReadTemperatures();

        SampleReady?.Invoke(this, new LiveMetricsSample
        {
            Timestamp = DateTime.Now,
            CpuUsagePercent = cpu,
            RamUsagePercent = ramPercent,
            RamUsedGB = usedRamGb,
            RamTotalGB = totalRamGb,
            DiskUsagePercent = disk,
            GpuUsagePercent = gpu,
            CpuTemperatureC = cpuTemp,
            GpuTemperatureC = gpuTemp,
        });
    }

    private static float SafeNextValue(PerformanceCounter counter)
    {
        try { return counter.NextValue(); }
        catch { return 0f; }
    }

    /// <summary>
    /// Prefers the "_Total" instance; some systems (RAID controllers, certain drivers, remote
    /// sessions) don't expose it, so this falls back to summing every individual disk instance.
    /// </summary>
    private static List<PerformanceCounter> CreateDiskCounters()
    {
        var counters = new List<PerformanceCounter>();
        try
        {
            if (!PerformanceCounterCategory.Exists("PhysicalDisk"))
                return counters;

            var category = new PerformanceCounterCategory("PhysicalDisk");
            var instances = category.GetInstanceNames();

            if (instances.Contains("_Total"))
            {
                counters.Add(new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total", true));
            }
            else
            {
                foreach (var instance in instances)
                {
                    try { counters.Add(new PerformanceCounter("PhysicalDisk", "% Disk Time", instance, true)); }
                    catch { /* instance can disappear between enumeration and creation */ }
                }
            }
        }
        catch
        {
            // Leave counters empty; ReadDiskUsage() will report 0.
        }
        return counters;
    }

    private float ReadDiskUsage()
    {
        if (_diskCounters.Count == 0)
            return 0f;

        try
        {
            // "_Total" alone is already 0-100; summed individual instances can exceed 100 on
            // multi-disk systems under simultaneous load, so clamp either way.
            var value = _diskCounters.Count == 1 ? SafeNextValue(_diskCounters[0]) : _diskCounters.Average(SafeNextValue);
            return Math.Clamp(value, 0f, 100f);
        }
        catch
        {
            return 0f;
        }
    }

    private static List<PerformanceCounter>? TryCreateGpuCounters()
    {
        try
        {
            if (!PerformanceCounterCategory.Exists("GPU Engine"))
                return null;

            var category = new PerformanceCounterCategory("GPU Engine");
            var allInstances = category.GetInstanceNames();

            // Prefer 3D engine instances (what Task Manager's headline "GPU" number reflects);
            // fall back to every engine type if a driver doesn't report engtype_3D at all.
            var instances = allInstances.Where(n => n.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase)).ToList();
            if (instances.Count == 0)
                instances = allInstances.ToList();

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

    private static Computer? TryOpenSensorComputer()
    {
        try
        {
            var computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
            };
            computer.Open();
            return computer;
        }
        catch
        {
            // Sensor access can fail without administrator rights; temperatures just show N/A.
            return null;
        }
    }

    private (float? cpuTemp, float? gpuTemp) ReadTemperatures()
    {
        if (_sensorComputer is null)
            return (null, null);

        float? cpuTemp = null;
        float? gpuTemp = null;

        try
        {
            foreach (var hardware in _sensorComputer.Hardware)
            {
                hardware.Update();

                var isCpu = hardware.HardwareType == HardwareType.Cpu;
                var isGpu = hardware.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel;
                if (!isCpu && !isGpu)
                    continue;

                var temps = hardware.Sensors
                    .Where(s => s.SensorType == SensorType.Temperature && s.Value.HasValue)
                    .Select(s => s.Value!.Value)
                    .ToList();
                if (temps.Count == 0)
                    continue;

                // Package/hotspot sensors read highest and are the figure users expect ("CPU temp").
                var highest = temps.Max();
                if (isCpu)
                    cpuTemp = cpuTemp is { } c ? Math.Max(c, highest) : highest;
                else
                    gpuTemp = gpuTemp is { } g ? Math.Max(g, highest) : highest;
            }
        }
        catch
        {
            // Best-effort only; keep whatever was already read.
        }

        // This process deliberately never runs elevated (see SensorHelperClient), so the CPU
        // package sensor above is normally unavailable; fall back to the value published by
        // the separate elevated helper, if the user has started it and it's still running.
        cpuTemp ??= SensorHelperClient.TryReadCpuTemperature();

        return (cpuTemp, gpuTemp);
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
        _diskCounters.ForEach(c => c.Dispose());
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
