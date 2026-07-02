using SlapIA.App.Models;

namespace SlapIA.App.Services;

public interface IInstalledSoftwareService
{
    Task<IReadOnlyList<InstalledApplication>> GetInstalledApplicationsAsync();
}
