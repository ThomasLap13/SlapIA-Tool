using Microsoft.Win32;
using SlapIA.App.Models;

namespace SlapIA.App.Services;

/// <summary>
/// Enumerates installed applications from the standard Windows Uninstall registry keys
/// (same source Control Panel &gt; Programs and Features uses).
/// </summary>
public class InstalledSoftwareService : IInstalledSoftwareService
{
    private static readonly (RegistryKey Hive, string SubKey)[] Roots =
    {
        (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
        (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
        (Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
    };

    public Task<IReadOnlyList<InstalledApplication>> GetInstalledApplicationsAsync()
        => Task.Run(() => (IReadOnlyList<InstalledApplication>)ReadApplications());

    private static List<InstalledApplication> ReadApplications()
    {
        var apps = new List<InstalledApplication>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (hive, subKeyPath) in Roots)
        {
            using var root = hive.OpenSubKey(subKeyPath);
            if (root is null)
                continue;

            foreach (var keyName in root.GetSubKeyNames())
            {
                using var key = root.OpenSubKey(keyName);
                if (key is null)
                    continue;

                var name = key.GetValue("DisplayName") as string;
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                // Skip OS components and update patches, they clutter the list without being "software" a user installed.
                if (Convert.ToInt32(key.GetValue("SystemComponent", 0)) == 1)
                    continue;
                if (key.GetValue("ParentKeyName") is not null)
                    continue;
                if (!seenNames.Add(name))
                    continue;

                DateTime? installDate = null;
                if (key.GetValue("InstallDate") is string raw &&
                    DateTime.TryParseExact(raw, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var parsed))
                {
                    installDate = parsed;
                }

                apps.Add(new InstalledApplication(
                    Name: name.Trim(),
                    Version: key.GetValue("DisplayVersion") as string,
                    Publisher: key.GetValue("Publisher") as string,
                    InstallDate: installDate,
                    InstallLocation: key.GetValue("InstallLocation") as string));
            }
        }

        return apps.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
