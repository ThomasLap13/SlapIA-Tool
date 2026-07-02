using SlapIA.App.Models;

namespace SlapIA.App.Services;

public interface IPerformanceMonitorService : IDisposable
{
    event EventHandler<LiveMetricsSample>? SampleReady;
    void Start(TimeSpan interval);
    void Stop();
}
