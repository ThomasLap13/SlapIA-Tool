using SlapIA.App.Models;

namespace SlapIA.App.Services;

public interface ISystemInfoService
{
    Task<SystemSnapshot> GetSnapshotAsync(bool forceRefresh = false);
}
