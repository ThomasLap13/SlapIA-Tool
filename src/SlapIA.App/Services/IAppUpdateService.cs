using Velopack;

namespace SlapIA.App.Services;

public interface IAppUpdateService
{
    /// <summary>True when this instance was installed via the Velopack installer (not "dotnet run").</summary>
    bool IsInstalled { get; }

    Task<UpdateInfo?> CheckForUpdatesAsync();

    /// <summary>Downloads the update and restarts the app into it. Does not return on success.</summary>
    Task DownloadAndApplyAsync(UpdateInfo info, Action<int>? onProgress = null);
}
