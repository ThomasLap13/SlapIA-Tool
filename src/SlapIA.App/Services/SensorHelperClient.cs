using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace SlapIA.App.Services;

/// <summary>
/// Reads CPU temperature published by the separate, elevated SlapIA.SensorHelper.exe process
/// (see that project for why: LibreHardwareMonitor's CPU sensors need administrator rights,
/// and this app deliberately stays unelevated so Velopack's silent self-update keeps working).
/// The main app never runs as admin itself - it only launches this small helper on request,
/// which triggers its own separate UAC prompt.
/// </summary>
public static class SensorHelperClient
{
    // Keep in sync with SlapIA.SensorHelper.Program.
    private const string MapName = "Global\\SlapIAToolCpuSensor";
    private const int MapCapacity = 32;
    private static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(10);

    [StructLayout(LayoutKind.Sequential)]
    private struct SensorPayload
    {
        public double CpuTemperatureC;
        public long TimestampUtcTicks;
    }

    public static bool IsHelperRunning => Process.GetProcessesByName("SlapIA.SensorHelper").Length > 0;

    /// <summary>Reads the latest CPU temperature from the helper's shared memory, or null if
    /// the helper isn't running or hasn't published a fresh value recently.</summary>
    public static float? TryReadCpuTemperature()
    {
        try
        {
            using var mmf = MemoryMappedFile.OpenExisting(MapName);
            using var accessor = mmf.CreateViewAccessor(0, MapCapacity, MemoryMappedFileAccess.Read);
            accessor.Read(0, out SensorPayload payload);

            var age = DateTime.UtcNow - new DateTime(payload.TimestampUtcTicks, DateTimeKind.Utc);
            return age <= StaleAfter ? (float)payload.CpuTemperatureC : null;
        }
        catch (FileNotFoundException)
        {
            return null; // Helper isn't running.
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Launches the elevated helper (triggers a single UAC prompt). Returns false if
    /// the user cancels the prompt or the helper executable can't be found.</summary>
    public static bool TryStartHelper()
    {
        if (IsHelperRunning)
            return true;

        var exePath = Path.Combine(AppContext.BaseDirectory, "SlapIA.SensorHelper.exe");
        if (!File.Exists(exePath))
            return false;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                Verb = "runas",
            });
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false; // User declined the UAC prompt.
        }
    }
}
