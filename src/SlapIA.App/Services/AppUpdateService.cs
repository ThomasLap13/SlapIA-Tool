using Velopack;
using Velopack.Sources;

namespace SlapIA.App.Services;

/// <summary>
/// Checks GitHub Releases (https://github.com/ThomasLap13/SlapIA-Tool) for newer builds and
/// applies them via Velopack. Only meaningful when the app was installed through the Velopack
/// setup.exe - running via "dotnet run" reports <see cref="IsInstalled"/> = false and never
/// finds updates.
/// </summary>
public class AppUpdateService : IAppUpdateService
{
    private const string RepoUrl = "https://github.com/ThomasLap13/SlapIA-Tool";

    private readonly UpdateManager _updateManager = new(new GithubSource(RepoUrl, null, false));

    public bool IsInstalled => _updateManager.IsInstalled;

    public async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        if (!IsInstalled)
            return null;

        return await _updateManager.CheckForUpdatesAsync();
    }

    public async Task DownloadAndApplyAsync(UpdateInfo info, Action<int>? onProgress = null)
    {
        await _updateManager.DownloadUpdatesAsync(info, onProgress);
        _updateManager.ApplyUpdatesAndRestart(info);
    }
}
