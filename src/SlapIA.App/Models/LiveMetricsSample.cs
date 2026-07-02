namespace SlapIA.App.Models;

public class LiveMetricsSample
{
    public DateTime Timestamp { get; init; }
    public float CpuUsagePercent { get; init; }
    public float RamUsagePercent { get; init; }
    public float RamUsedGB { get; init; }
    public float RamTotalGB { get; init; }
    public float DiskUsagePercent { get; init; }
    public float? GpuUsagePercent { get; init; }
    public float? CpuTemperatureC { get; init; }
    public float? GpuTemperatureC { get; init; }
}
