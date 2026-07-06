using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using LibreHardwareMonitor.Hardware;

namespace SlapIA.SensorHelper;

/// <summary>
/// Minimal elevated helper: reads the CPU temperature (which needs administrator rights for
/// LibreHardwareMonitor's driver) and publishes it to a shared memory-mapped file that the
/// unlevated main app polls. Exits on its own once the main app is no longer running, so it
/// never lingers as an orphaned elevated process.
/// </summary>
internal static class Program
{
    // Keep in sync with SlapIA.App.Services.SensorHelperClient.
    private const string MapName = "Global\\SlapIAToolCpuSensor";
    private const int MapCapacity = 32;
    private const string MainProcessName = "SlapIA.Tool";

    [StructLayout(LayoutKind.Sequential)]
    private struct SensorPayload
    {
        public double CpuTemperatureC;
        public long TimestampUtcTicks;
    }

    private static void Main()
    {
        var computer = new Computer { IsCpuEnabled = true };
        try
        {
            computer.Open();
        }
        catch
        {
            return; // Nothing to publish if the sensor backend can't even open.
        }

        using var mmf = MemoryMappedFile.CreateNew(MapName, MapCapacity);
        using var accessor = mmf.CreateViewAccessor(0, MapCapacity);

        while (IsMainAppRunning())
        {
            var temp = ReadCpuTemperature(computer);
            if (temp is { } value)
            {
                var payload = new SensorPayload { CpuTemperatureC = value, TimestampUtcTicks = DateTime.UtcNow.Ticks };
                accessor.Write(0, ref payload);
            }
            Thread.Sleep(2000);
        }
    }

    private static float? ReadCpuTemperature(Computer computer)
    {
        float? highest = null;
        foreach (var hardware in computer.Hardware)
        {
            if (hardware.HardwareType != HardwareType.Cpu)
                continue;

            hardware.Update();
            foreach (var sensor in hardware.Sensors)
            {
                if (sensor.SensorType == SensorType.Temperature && sensor.Value is { } value)
                    highest = highest is { } h ? Math.Max(h, value) : value;
            }
        }
        return highest;
    }

    private static bool IsMainAppRunning() => Process.GetProcessesByName(MainProcessName).Length > 0;
}
