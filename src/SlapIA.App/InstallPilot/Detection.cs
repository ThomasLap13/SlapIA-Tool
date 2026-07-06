using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace InstallPilot
{
    public static class Detection
    {
        private static HashSet<string> _registryCache = null;
        private static HashSet<string> _appxCache = null;
        private static HashSet<string> _wingetStoreCache = null;
        private static HashSet<string> _wingetInstalledCache = null;
        private static HashSet<string> _wingetUpgradesCache = null;
        private static bool? _nvidiaGpuCached = null;

        private static readonly Regex ReWingetStore = new Regex(@"^[A-Z0-9]{12,}$", RegexOptions.Compiled);
        private static readonly Regex ReWingetId = new Regex(@"^[A-Za-z0-9][A-Za-z0-9._-]{4,79}$", RegexOptions.Compiled);
        private static readonly Regex ReVersion = new Regex(@"^[\d.]+$", RegexOptions.Compiled);
        private static readonly Regex ReProductId = new Regex(@"ProductId=([A-Z0-9]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly string[] StoreMarkers = { "windowsapps", "\\packages\\" };

        public static void ClearCaches()
        {
            _registryCache = null;
            _appxCache = null;
            _wingetStoreCache = null;
            _wingetInstalledCache = null;
            _wingetUpgradesCache = null;
        }

        public static HashSet<string> GetRegistryApps()
        {
            if (_registryCache != null) return _registryCache;
            _registryCache = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var targets = new[]
            {
                new { Hive = RegistryHive.LocalMachine, Path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", View = RegistryView.Registry64 },
                new { Hive = RegistryHive.LocalMachine, Path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", View = RegistryView.Registry32 },
                new { Hive = RegistryHive.CurrentUser, Path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", View = RegistryView.Default }
            };

            foreach (var target in targets)
            {
                try
                {
                    using (var baseKey = RegistryKey.OpenBaseKey(target.Hive, target.View))
                    using (var key = baseKey.OpenSubKey(target.Path))
                    {
                        if (key == null) continue;
                        foreach (string subkeyName in key.GetSubKeyNames())
                        {
                            try
                            {
                                using (var subkey = key.OpenSubKey(subkeyName))
                                {
                                    if (subkey == null) continue;
                                    var dispObj = subkey.GetValue("DisplayName");
                                    var displayName = dispObj != null ? dispObj.ToString() : null;
                                    if (!string.IsNullOrEmpty(displayName))
                                    {
                                        _registryCache.Add(displayName.Trim().ToLower());
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }

            return _registryCache;
        }

        public static HashSet<string> GetAppxPackages()
        {
            if (_appxCache != null) return _appxCache;
            _appxCache = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                string path = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";
                using (var key = Registry.CurrentUser.OpenSubKey(path))
                {
                    if (key != null)
                    {
                        foreach (string subkeyName in key.GetSubKeyNames())
                        {
                            _appxCache.Add(subkeyName.ToLower());
                            int index = subkeyName.IndexOf('_');
                            if (index > 0)
                            {
                                _appxCache.Add(subkeyName.Substring(0, index).ToLower());
                            }
                        }
                    }
                }
            }
            catch { }

            return _appxCache;
        }

        public static string FindWingetPath()
        {
            // Similar to Python shutil.which("winget")
            string pathEnv = Environment.GetEnvironmentVariable("PATH");
            string[] paths = pathEnv != null ? pathEnv.Split(Path.PathSeparator) : new string[0];
            foreach (var p in paths)
            {
                try
                {
                    string target = Path.Combine(p, "winget.exe");
                    if (File.Exists(target)) return target;
                    target = Path.Combine(p, "winget");
                    if (File.Exists(target)) return target;
                }
                catch { }
            }

            string localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            if (!string.IsNullOrEmpty(localAppData))
            {
                string target = Path.Combine(localAppData, @"Microsoft\WindowsApps\winget.exe");
                if (File.Exists(target)) return target;
            }

            // Iterate user directories
            try
            {
                string usersDir = @"C:\Users";
                if (Directory.Exists(usersDir))
                {
                    foreach (string userDir in Directory.GetDirectories(usersDir))
                    {
                        string target = Path.Combine(userDir, @"AppData\Local\Microsoft\WindowsApps\winget.exe");
                        if (File.Exists(target)) return target;
                    }
                }
            }
            catch { }

            return "winget"; // Fallback
        }

        public static void LoadWingetCache()
        {
            if (_wingetStoreCache != null) return;
            _wingetStoreCache = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _wingetInstalledCache = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                string wingetBin = FindWingetPath();
                var startInfo = new ProcessStartInfo
                {
                    FileName = wingetBin,
                    Arguments = "list --accept-source-agreements",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process != null)
                    {
                        // Wait at most 20 seconds
                        if (process.WaitForExit(20000))
                        {
                            string output = process.StandardOutput.ReadToEnd();
                            string[] lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var line in lines)
                            {
                                string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                                foreach (var part in parts)
                                {
                                    if (ReWingetStore.IsMatch(part))
                                    {
                                        _wingetStoreCache.Add(part.ToUpper());
                                    }
                                    else if (part.Contains(".") && ReWingetId.IsMatch(part) && !ReVersion.IsMatch(part))
                                    {
                                        _wingetInstalledCache.Add(part.ToLower());
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }

        public static HashSet<string> GetWingetStoreIds()
        {
            return _wingetStoreCache ?? new HashSet<string>();
        }

        public static HashSet<string> GetWingetInstalled()
        {
            return _wingetInstalledCache ?? new HashSet<string>();
        }

        public static void LoadWingetUpgradesCache()
        {
            if (_wingetUpgradesCache != null) return;
            _wingetUpgradesCache = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                string wingetBin = FindWingetPath();
                var startInfo = new ProcessStartInfo
                {
                    FileName = wingetBin,
                    Arguments = "upgrade --include-unknown --accept-source-agreements",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process != null)
                    {
                        // Wait at most 30 seconds
                        if (process.WaitForExit(30000))
                        {
                            string output = process.StandardOutput.ReadToEnd();
                            string[] lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var line in lines)
                            {
                                string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                                foreach (var part in parts)
                                {
                                    if (part.Contains(".") && ReWingetId.IsMatch(part) && !ReVersion.IsMatch(part))
                                    {
                                        _wingetUpgradesCache.Add(part.ToLower());
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }

        public static HashSet<string> GetWingetUpgrades()
        {
            return _wingetUpgradesCache ?? new HashSet<string>();
        }

        private static string CheckStoreSource(AppInfo app)
        {
            if (string.IsNullOrEmpty(app.store_url)) return "system";
            var match = ReProductId.Match(app.store_url);
            if (match.Success && GetWingetStoreIds().Contains(match.Groups[1].Value.ToUpper()))
            {
                return "store";
            }
            return "system";
        }

        public static string ResolvePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            string expanded = Environment.ExpandEnvironmentVariables(path);
            if (expanded.StartsWith("~"))
            {
                string userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                expanded = Path.Combine(userHome, expanded.Substring(1).TrimStart('\\', '/'));
            }
            return expanded;
        }

        private static bool PathMatchesPattern(string pattern)
        {
            string resolved = ResolvePath(pattern).Replace('/', '\\');
            if (resolved.Contains("*") || resolved.Contains("?"))
            {
                try
                {
                    string dir = Path.GetDirectoryName(resolved);
                    string filePattern = Path.GetFileName(resolved);
                    if (Directory.Exists(dir))
                    {
                        var matches = Directory.GetFiles(dir, filePattern);
                        return matches.Length > 0;
                    }
                }
                catch { }
                return false;
            }
            return File.Exists(resolved) || Directory.Exists(resolved);
        }

        private static bool AnyMatchInDirectory(string pattern, out bool viaStore)
        {
            viaStore = false;
            string resolved = ResolvePath(pattern).Replace('/', '\\');
            try
            {
                string dir = Path.GetDirectoryName(resolved);
                string filePattern = Path.GetFileName(resolved);
                if (Directory.Exists(dir))
                {
                    var matches = Directory.GetFiles(dir, filePattern);
                    if (matches.Length > 0)
                    {
                        foreach (var m in matches)
                        {
                            foreach (var marker in StoreMarkers)
                            {
                                if (m.ToLower().Contains(marker))
                                {
                                    viaStore = true;
                                    break;
                                }
                            }
                        }
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        public static Tuple<bool, string> DetectInstallation(AppInfo app)
        {
            // 1. Check check_paths
            if (app.check_paths != null)
            {
                foreach (var p in app.check_paths)
                {
                    string resolved = ResolvePath(p).Replace('/', '\\');
                    bool isStorePath = false;
                    foreach (var marker in StoreMarkers)
                    {
                        if (resolved.ToLower().Contains(marker))
                        {
                            isStorePath = true;
                            break;
                        }
                    }

                    if (resolved.Contains("*") || resolved.Contains("?"))
                    {
                        bool viaStore;
                        if (AnyMatchInDirectory(resolved, out viaStore))
                        {
                            string src = viaStore ? "store" : CheckStoreSource(app);
                            return Tuple.Create(true, src);
                        }
                    }
                    else if (File.Exists(resolved) || Directory.Exists(resolved))
                    {
                        string src = isStorePath ? "store" : CheckStoreSource(app);
                        return Tuple.Create(true, src);
                    }
                }
            }

            // 2. Check registry names
            if (app.registry_names != null && app.registry_names.Count > 0)
            {
                var installed = GetRegistryApps();
                foreach (var rn in app.registry_names)
                {
                    string rnLower = rn.ToLower();
                    foreach (var entry in installed)
                    {
                        if (entry.Contains(rnLower))
                        {
                            return Tuple.Create(true, CheckStoreSource(app));
                        }
                    }
                }
            }

            // 3. Check Appx names
            if (app.appx_names != null && app.appx_names.Count > 0)
            {
                var pkgs = GetAppxPackages();
                foreach (var name in app.appx_names)
                {
                    if (pkgs.Contains(name.ToLower()))
                    {
                        return Tuple.Create(true, "store");
                    }
                }
            }

            // 4. Check winget ID
            if (!string.IsNullOrEmpty(app.winget_id))
            {
                var wingetInstalled = GetWingetInstalled();
                if (wingetInstalled.Contains(app.winget_id.ToLower()))
                {
                    return Tuple.Create(true, "system");
                }
            }

            return Tuple.Create(false, (string)null);
        }

        public static bool HasNvidiaGpu()
        {
            if (_nvidiaGpuCached.HasValue) return _nvidiaGpuCached.Value;

            // 1. Registry check
            try
            {
                string path = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
                using (var key = Registry.LocalMachine.OpenSubKey(path))
                {
                    if (key != null)
                    {
                        foreach (string subkeyName in key.GetSubKeyNames())
                        {
                            int dummy;
                            if (int.TryParse(subkeyName, out dummy))
                            {
                                using (var subkey = key.OpenSubKey(subkeyName))
                                {
                                    if (subkey != null)
                                    {
                                        var pObj = subkey.GetValue("ProviderName");
                                        string provider = pObj != null ? pObj.ToString().ToLower() : null;
                                        if (provider != null && provider.Contains("nvidia"))
                                        {
                                            _nvidiaGpuCached = true;
                                            return true;
                                        }
                                        var cObj = subkey.GetValue("ChipType");
                                        string chip = cObj != null ? cObj.ToString().ToLower() : null;
                                        if (chip != null && chip.Contains("nvidia"))
                                        {
                                            _nvidiaGpuCached = true;
                                            return true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            // 2. WMIC / Command fallback
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "wmic",
                    Arguments = "path win32_VideoController get name",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var proc = Process.Start(startInfo))
                {
                    if (proc != null && proc.WaitForExit(5000))
                    {
                        string output = proc.StandardOutput.ReadToEnd().ToLower();
                        if (output.Contains("nvidia"))
                        {
                            _nvidiaGpuCached = true;
                            return true;
                        }
                    }
                }
            }
            catch { }

            // 3. PowerShell fallback
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = "-NoProfile -Command \"Get-CimInstance Win32_VideoController | Select-Object -ExpandProperty Name\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var proc = Process.Start(startInfo))
                {
                    if (proc != null && proc.WaitForExit(5000))
                    {
                        string output = proc.StandardOutput.ReadToEnd().ToLower();
                        if (output.Contains("nvidia"))
                        {
                            _nvidiaGpuCached = true;
                            return true;
                        }
                    }
                }
            }
            catch { }

            _nvidiaGpuCached = false;
            return false;
        }
    }
}
