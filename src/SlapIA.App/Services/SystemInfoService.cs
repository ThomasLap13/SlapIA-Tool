using System.IO;
using System.Management;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using SlapIA.App.Models;

namespace SlapIA.App.Services;

/// <summary>
/// Reads static machine information (CPU, RAM, GPU, disks, network, OS) via WMI and
/// the .NET BCL. Results are cached after the first successful read.
/// </summary>
public class SystemInfoService : ISystemInfoService
{
    private SystemSnapshot? _cached;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<SystemSnapshot> GetSnapshotAsync(bool forceRefresh = false)
    {
        if (_cached is not null && !forceRefresh)
            return _cached;

        await _lock.WaitAsync();
        try
        {
            if (_cached is not null && !forceRefresh)
                return _cached;

            _cached = await Task.Run(BuildSnapshot);
            return _cached;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static SystemSnapshot BuildSnapshot()
    {
        var snapshot = new SystemSnapshot
        {
            Processor = ReadProcessor(),
            Memory = ReadMemory(),
            GraphicsCards = ReadGraphicsCards(),
            Disks = ReadDisks(),
            Volumes = ReadVolumes(),
            NetworkAdapters = ReadNetworkAdapters(),
            OperatingSystem = ReadOperatingSystem(),
        };

        var (manufacturer, model) = ReadMotherboard();
        snapshot.MotherboardManufacturer = manufacturer;
        snapshot.MotherboardModel = model;
        snapshot.BiosVersion = ReadBiosVersion();

        return snapshot;
    }

    private static ProcessorInfo? ReadProcessor()
    {
        return QueryFirst("SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor", mo =>
            new ProcessorInfo(
                Name: CleanString(mo["Name"] as string) ?? "CPU inconnu",
                Cores: Convert.ToInt32(mo["NumberOfCores"] ?? 0),
                LogicalProcessors: Convert.ToInt32(mo["NumberOfLogicalProcessors"] ?? 0),
                MaxClockSpeedGHz: Math.Round(Convert.ToDouble(mo["MaxClockSpeed"] ?? 0) / 1000.0, 2)));
    }

    private static MemoryInfo? ReadMemory()
    {
        double totalBytes = 0;
        foreach (var mo in Query("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
        {
            totalBytes = Convert.ToDouble(mo["TotalPhysicalMemory"] ?? 0);
            break;
        }

        int moduleCount = 0;
        int? speed = null;
        string? type = null;

        foreach (var mo in Query("SELECT Capacity, Speed, SMBIOSMemoryType FROM Win32_PhysicalMemory"))
        {
            moduleCount++;
            speed ??= mo["Speed"] is null ? null : Convert.ToInt32(mo["Speed"]);
            type ??= MapMemoryType(mo["SMBIOSMemoryType"] is null ? null : Convert.ToInt32(mo["SMBIOSMemoryType"]));
        }

        if (totalBytes <= 0 && moduleCount == 0)
            return null;

        return new MemoryInfo(
            TotalGB: Math.Round(totalBytes / 1024 / 1024 / 1024, 1),
            ModuleCount: moduleCount,
            SpeedMHz: speed,
            MemoryType: type);
    }

    private static List<GraphicsCardInfo> ReadGraphicsCards()
    {
        var cards = new List<GraphicsCardInfo>();
        foreach (var mo in Query("SELECT Name, AdapterRAM, DriverVersion FROM Win32_VideoController"))
        {
            var name = CleanString(mo["Name"] as string);
            if (string.IsNullOrWhiteSpace(name))
                continue;

            double? vramGb = null;
            if (mo["AdapterRAM"] is not null)
            {
                var bytes = Convert.ToInt64(mo["AdapterRAM"]);
                // AdapterRAM is a 32-bit field in WMI and overflows/wraps for cards with >=4GB VRAM.
                if (bytes > 0)
                    vramGb = Math.Round(bytes / 1024.0 / 1024 / 1024, 1);
            }

            cards.Add(new GraphicsCardInfo(name, vramGb, mo["DriverVersion"] as string));
        }
        return cards;
    }

    private static List<DiskInfo> ReadDisks()
    {
        var disks = new List<DiskInfo>();

        // Prefer MSFT_PhysicalDisk (Storage namespace) which exposes real MediaType (SSD/HDD).
        try
        {
            using var searcher = new ManagementObjectSearcher(
                new ManagementScope(@"root\Microsoft\Windows\Storage"),
                new ObjectQuery("SELECT FriendlyName, Size, MediaType, BusType FROM MSFT_PhysicalDisk"));

            foreach (ManagementBaseObject mo in searcher.Get())
            {
                var size = mo["Size"] is null ? 0 : Convert.ToInt64(mo["Size"]);
                disks.Add(new DiskInfo(
                    Model: CleanString(mo["FriendlyName"] as string) ?? "Disque inconnu",
                    SizeGB: Math.Round(size / 1024.0 / 1024 / 1024, 1),
                    MediaType: MapStorageMediaType(mo["MediaType"] is null ? null : Convert.ToInt32(mo["MediaType"])),
                    InterfaceType: MapBusType(mo["BusType"] is null ? null : Convert.ToInt32(mo["BusType"]))));
            }
        }
        catch
        {
            disks.Clear();
        }

        if (disks.Count > 0)
            return disks;

        // Fallback for machines/permissions where the Storage namespace isn't available.
        foreach (var mo in Query("SELECT Model, Size, MediaType, InterfaceType FROM Win32_DiskDrive"))
        {
            var size = mo["Size"] is null ? 0 : Convert.ToInt64(mo["Size"]);
            disks.Add(new DiskInfo(
                Model: CleanString(mo["Model"] as string) ?? "Disque inconnu",
                SizeGB: Math.Round(size / 1024.0 / 1024 / 1024, 1),
                MediaType: CleanString(mo["MediaType"] as string) ?? "Inconnu",
                InterfaceType: mo["InterfaceType"] as string ?? "Inconnu"));
        }
        return disks;
    }

    private static List<VolumeInfo> ReadVolumes()
    {
        var volumes = new List<VolumeInfo>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType != DriveType.Fixed || !drive.IsReady)
                continue;

            volumes.Add(new VolumeInfo(
                Letter: drive.Name,
                Label: string.IsNullOrWhiteSpace(drive.VolumeLabel) ? null : drive.VolumeLabel,
                TotalGB: Math.Round(drive.TotalSize / 1024.0 / 1024 / 1024, 1),
                FreeGB: Math.Round(drive.TotalFreeSpace / 1024.0 / 1024 / 1024, 1),
                FileSystem: drive.DriveFormat));
        }
        return volumes;
    }

    private static List<NetworkAdapterInfo> ReadNetworkAdapters()
    {
        var adapters = new List<NetworkAdapterInfo>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                continue;

            var ipv4 = nic.GetIPProperties().UnicastAddresses
                .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)?
                .Address.ToString();

            var mac = nic.GetPhysicalAddress().ToString();
            var formattedMac = mac.Length == 12
                ? string.Join(":", Enumerable.Range(0, 6).Select(i => mac.Substring(i * 2, 2)))
                : null;

            adapters.Add(new NetworkAdapterInfo(
                Name: nic.Name,
                MacAddress: formattedMac,
                IPv4Address: ipv4,
                IsUp: nic.OperationalStatus == OperationalStatus.Up));
        }
        return adapters.OrderByDescending(a => a.IsUp).ToList();
    }

    private static OperatingSystemInfo? ReadOperatingSystem()
    {
        return QueryFirst("SELECT Caption, Version, OSArchitecture, InstallDate, LastBootUpTime FROM Win32_OperatingSystem", mo =>
        {
            DateTime? installDate = null;
            if (mo["InstallDate"] is string installRaw)
                installDate = ManagementDateTimeConverter.ToDateTime(installRaw);

            var uptime = TimeSpan.Zero;
            if (mo["LastBootUpTime"] is string bootRaw)
                uptime = DateTime.Now - ManagementDateTimeConverter.ToDateTime(bootRaw);

            return new OperatingSystemInfo(
                Name: CleanString(mo["Caption"] as string) ?? "Windows",
                Version: mo["Version"] as string ?? "",
                Architecture: mo["OSArchitecture"] as string ?? "",
                ComputerName: Environment.MachineName,
                UserName: Environment.UserName,
                InstallDate: installDate,
                Uptime: uptime);
        });
    }

    private static (string? manufacturer, string? model) ReadMotherboard()
    {
        foreach (var mo in Query("SELECT Manufacturer, Product FROM Win32_BaseBoard"))
            return (mo["Manufacturer"] as string, mo["Product"] as string);
        return (null, null);
    }

    private static string? ReadBiosVersion()
    {
        return QueryFirst("SELECT SMBIOSBIOSVersion FROM Win32_BIOS", mo => mo["SMBIOSBIOSVersion"] as string);
    }

    private static IEnumerable<ManagementBaseObject> Query(string wql)
    {
        ManagementObjectSearcher searcher;
        ManagementObjectCollection results;
        try
        {
            searcher = new ManagementObjectSearcher(wql);
            results = searcher.Get();
        }
        catch
        {
            // WMI can be unavailable/restricted on some machines; degrade gracefully.
            yield break;
        }

        using (searcher)
        using (results)
        {
            foreach (ManagementBaseObject mo in results)
                yield return mo;
        }
    }

    private static T? QueryFirst<T>(string wql, Func<ManagementBaseObject, T> select)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(wql);
            foreach (ManagementBaseObject mo in searcher.Get())
                return select(mo);
        }
        catch
        {
            // WMI can be unavailable/restricted on some machines; degrade gracefully.
        }
        return default;
    }

    private static string? CleanString(string? value) => value?.Trim();

    private static string MapMemoryType(int? smbiosType) => smbiosType switch
    {
        20 => "DDR",
        21 => "DDR2",
        24 => "DDR3",
        26 => "DDR4",
        34 => "DDR5",
        _ => "Inconnu",
    };

    private static string MapStorageMediaType(int? mediaType) => mediaType switch
    {
        3 => "HDD",
        4 => "SSD",
        5 => "SCM",
        _ => "Inconnu",
    };

    private static string MapBusType(int? busType) => busType switch
    {
        7 => "USB",
        8 => "RAID",
        11 => "SATA",
        17 => "NVMe",
        _ => "Inconnu",
    };
}
