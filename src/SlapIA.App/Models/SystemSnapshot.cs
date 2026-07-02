namespace SlapIA.App.Models;

public record ProcessorInfo(string Name, int Cores, int LogicalProcessors, double MaxClockSpeedGHz);

public record MemoryInfo(double TotalGB, int ModuleCount, int? SpeedMHz, string? MemoryType);

public record GraphicsCardInfo(string Name, double? VramGB, string? DriverVersion);

public record DiskInfo(string Model, double SizeGB, string MediaType, string InterfaceType);

public record VolumeInfo(string Letter, string? Label, double TotalGB, double FreeGB, string FileSystem);

public record NetworkAdapterInfo(string Name, string? MacAddress, string? IPv4Address, bool IsUp);

public record OperatingSystemInfo(
    string Name,
    string Version,
    string Architecture,
    string ComputerName,
    string UserName,
    DateTime? InstallDate,
    TimeSpan Uptime);

public class SystemSnapshot
{
    public ProcessorInfo? Processor { get; set; }
    public MemoryInfo? Memory { get; set; }
    public List<GraphicsCardInfo> GraphicsCards { get; set; } = new();
    public List<DiskInfo> Disks { get; set; } = new();
    public List<VolumeInfo> Volumes { get; set; } = new();
    public List<NetworkAdapterInfo> NetworkAdapters { get; set; } = new();
    public OperatingSystemInfo? OperatingSystem { get; set; }
    public string? MotherboardManufacturer { get; set; }
    public string? MotherboardModel { get; set; }
    public string? BiosVersion { get; set; }
}
