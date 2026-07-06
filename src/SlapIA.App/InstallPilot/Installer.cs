using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Text;
using System.Text.Json;
using Forms = System.Windows.Forms;

namespace InstallPilot
{
    public static class Installer
    {
        private static readonly string TempDirName = "InstallPilot_Temp";
        private static Dictionary<string, string> _externalUrls = null;

        static Installer()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | (SecurityProtocolType)3072;
        }

        private static string GetTempDirectoryPath()
        {
            string programData = Environment.GetEnvironmentVariable("ProgramData");
            if (string.IsNullOrEmpty(programData)) programData = @"C:\ProgramData";
            string candidate = Path.Combine(programData, TempDirName);
            try
            {
                Directory.CreateDirectory(candidate);
                string testFile = Path.Combine(candidate, ".write_test");
                File.WriteAllText(testFile, "ok");
                File.Delete(testFile);
                return candidate;
            }
            catch
            {
                string fallback = Path.Combine(Path.GetTempPath(), TempDirName);
                Directory.CreateDirectory(fallback);
                return fallback;
            }
        }

        public static string TempDir
        {
            get { return GetTempDirectoryPath(); }
        }

        public static void CleanupTemp()
        {
            string[] tempDirs = {
                Path.Combine(Path.GetTempPath(), ".InstallPilot_Temp"),
                Path.Combine(Path.GetTempPath(), "InstallPilot"),
                TempDir
            };

            foreach (var dir in tempDirs)
            {
                if (!Directory.Exists(dir)) continue;
                try
                {
                    foreach (var file in Directory.GetFiles(dir))
                    {
                        try { File.Delete(file); } catch { }
                    }
                    foreach (var sub in Directory.GetDirectories(dir))
                    {
                        try { Directory.Delete(sub, true); } catch { }
                    }
                }
                catch { }
            }
        }

        public static Dictionary<string, string> LoadExternalUrls()
        {
            if (_externalUrls != null) return _externalUrls;
            _externalUrls = new Dictionary<string, string>();

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string check1 = Path.Combine(baseDir, "urls.json");
            string check2 = "urls.json";
            string check3 = Path.Combine(baseDir, "..", "urls.json");

            string embeddedPath = null;
            if (File.Exists(check1)) embeddedPath = check1;
            else if (File.Exists(check2)) embeddedPath = check2;
            else if (File.Exists(check3)) embeddedPath = check3;

            string json = null;
            if (embeddedPath != null)
            {
                try
                {
                    json = File.ReadAllText(embeddedPath);
                }
                catch { }
            }

            if (json == null)
            {
                try
                {
                    Uri uri = new Uri("pack://application:,,,/InstallPilot/urls.json", UriKind.Absolute);
                    var resourceStream = System.Windows.Application.GetResourceStream(uri);
                    if (resourceStream != null)
                    {
                        using (var reader = new StreamReader(resourceStream.Stream))
                        {
                            json = reader.ReadToEnd();
                        }
                    }
                }
                catch { }
            }

            if (json != null)
            {
                try
                {
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (dict != null)
                    {
                        foreach (var pair in dict) _externalUrls[pair.Key] = pair.Value;
                    }
                }
                catch { }
            }

            // 2. Fetch online updates from GitHub
            try
            {
                using (var client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "InstallPilot/1.0");
                    string url = "https://raw.githubusercontent.com/ThomasLap13/InstallPilot/main/urls.json";
                    string onlineJson = client.DownloadString(url);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(onlineJson);
                    if (dict != null)
                    {
                        foreach (var pair in dict) _externalUrls[pair.Key] = pair.Value;
                    }
                }
            }
            catch { }

            // 3. Working directory override
            string pwdPath = Path.Combine(Directory.GetCurrentDirectory(), "urls.json");
            if (File.Exists(pwdPath))
            {
                try
                {
                    string overrideJson = File.ReadAllText(pwdPath);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(overrideJson);
                    if (dict != null)
                    {
                        foreach (var pair in dict) _externalUrls[pair.Key] = pair.Value;
                    }
                }
                catch { }
            }

            return _externalUrls;
        }

        public static string ResolveDownloadUrl(AppInfo app)
        {
            try
            {
                var extUrls = LoadExternalUrls();
                if (extUrls != null && extUrls.ContainsKey(app.id) && !string.IsNullOrEmpty(extUrls[app.id]))
                {
                    return extUrls[app.id];
                }
            }
            catch { }

            string direct = app.download_url;
            // SourceForge /download URLs do a clean HTTP 302 to the mirror — Invoke-WebRequest follows it natively.
            // No transformation needed; stripping /download caused 404s on the mirror subdomain.

            if (app.download_resolver == null)
            {
                return !string.IsNullOrEmpty(direct) ? direct : app.official_url;
            }

            string rtype = app.download_resolver.type;
            try
            {
                using (var client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                    
                    if (rtype == "github")
                    {
                        client.Headers.Add("Accept", "application/vnd.github.v3+json");
                        string api = "https://api.github.com/repos/" + app.download_resolver.owner + "/" + app.download_resolver.repo + "/releases/latest";
                        string response = client.DownloadString(api);
                        using var doc = JsonDocument.Parse(response);
                        if (doc.RootElement.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                        {
                            var regex = new Regex(app.download_resolver.pattern, RegexOptions.IgnoreCase);
                            foreach (var asset in assets.EnumerateArray())
                            {
                                string name = asset.TryGetProperty("name", out var nameEl) ? (nameEl.GetString() ?? "") : "";
                                if (regex.IsMatch(name))
                                {
                                    return asset.TryGetProperty("browser_download_url", out var urlEl) ? urlEl.GetString() : null;
                                }
                            }
                        }
                    }
                    else if (rtype == "vlc")
                    {
                        string html = client.DownloadString("https://get.videolan.org/vlc/last/win64/");
                        var match = Regex.Match(html, @"href=""(vlc-[^""]*-win64\.exe)""", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            return "https://ftp.osuosl.org/pub/videolan/vlc/last/win64/" + match.Groups[1].Value;
                        }
                    }
                    else if (rtype == "7zip")
                    {
                        string html = client.DownloadString("https://www.7-zip.org/download.html");
                        var match = Regex.Match(html, @"href=""(a/7z\d+-x64\.exe)""", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            return "https://www.7-zip.org/" + match.Groups[1].Value;
                        }
                    }
                    else if (rtype == "nodejs")
                    {
                        string json = client.DownloadString("https://nodejs.org/dist/index.json");
                        using var doc = JsonDocument.Parse(json);
                        foreach (var rel in doc.RootElement.EnumerateArray())
                        {
                            bool isLts = rel.TryGetProperty("lts", out var ltsEl) &&
                                ltsEl.ValueKind != JsonValueKind.Null &&
                                !(ltsEl.ValueKind == JsonValueKind.False);
                            if (isLts)
                            {
                                string ver = rel.TryGetProperty("version", out var verEl) ? (verEl.GetString() ?? "") : "";
                                return "https://nodejs.org/dist/" + ver + "/node-" + ver + "-x64.msi";
                            }
                        }
                    }
                    else if (rtype == "libreoffice")
                    {
                        string html = client.DownloadString("https://download.documentfoundation.org/libreoffice/stable/");
                        var matches = Regex.Matches(html, @"href=""(\d+\.\d+\.\d+)/""", RegexOptions.IgnoreCase);
                        if (matches.Count > 0)
                        {
                            string latestVer = "";
                            Version bestVersion = new Version(0, 0, 0);
                            foreach (Match m in matches)
                            {
                                string verStr = m.Groups[1].Value;
                                try
                                {
                                    var v = new Version(verStr);
                                    if (v > bestVersion)
                                    {
                                        bestVersion = v;
                                        latestVer = verStr;
                                    }
                                }
                                catch { }
                            }
                            if (!string.IsNullOrEmpty(latestVer))
                            {
                                return "https://download.documentfoundation.org/libreoffice/stable/" + latestVer + "/win/x86_64/LibreOffice_" + latestVer + "_Win_x86-64.msi";
                            }
                        }
                    }
                    else if (rtype == "keepass")
                    {
                        // Step 1: get current version from official endpoint
                        string ver = client.DownloadString("https://keepass.info/update/version2x.txt").Trim();
                        if (!string.IsNullOrEmpty(ver) && Regex.IsMatch(ver, @"^\d+\.\d+"))
                        {
                            // Step 2: capture the signed CDN URL (with ts= token) via non-redirecting request
                            // SourceForge returns HTTP 302 → Location: downloads.sourceforge.net/...?ts=TOKEN&r=...
                            // The token is valid for several minutes — enough time for the PS script to use it
                            string sfPage = "https://sourceforge.net/projects/keepass/files/KeePass%202.x/" + ver + "/KeePass-" + ver + "-Setup.exe/download";
                            try
                            {
                                var req = (HttpWebRequest)WebRequest.Create(sfPage);
                                req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
                                req.Referer = "https://sourceforge.net/";
                                req.AllowAutoRedirect = false;
                                req.Timeout = 10000;
                                using (var resp = (HttpWebResponse)req.GetResponse())
                                {
                                    string loc = resp.Headers["Location"];
                                    if (!string.IsNullOrEmpty(loc)) return loc;
                                }
                            }
                            catch { }
                            // Fallback: direct CDN URL without token (works on some mirrors)
                            return "https://downloads.sourceforge.net/project/keepass/KeePass%202.x/" + ver + "/KeePass-" + ver + "-Setup.exe";
                        }
                    }
                    else if (rtype == "everything")
                    {
                        string html = client.DownloadString("https://www.voidtools.com/downloads/");
                        var match = Regex.Match(html, @"href=""([^""]*Everything-([^""]*)\.x64-Setup\.exe)""", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            string url = match.Groups[1].Value;
                            return url.StartsWith("/") ? "https://www.voidtools.com" + url : url;
                        }
                    }
                    else if (rtype == "nvidia")
                    {
                        string html = client.DownloadString("https://www.nvidia.com/en-us/software/nvidia-app/");
                        var match = Regex.Match(html, @"href=""([^""]*NVIDIA_app_v[^""]*\.exe)""", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            return match.Groups[1].Value;
                        }
                    }
                    else if (rtype == "python")
                    {
                        string html = client.DownloadString("https://www.python.org/downloads/windows/");
                        var matches = Regex.Matches(html, @"href=""([^""]*/ftp/python/(\d+\.\d+\.\d+)/python-\d+\.\d+\.\d+-amd64\.exe)""", RegexOptions.IgnoreCase);
                        if (matches.Count > 0)
                        {
                            string bestUrl = "";
                            Version bestVersion = new Version(0, 0, 0);
                            foreach (Match m in matches)
                            {
                                string verStr = m.Groups[2].Value;
                                if (verStr.Contains("a") || verStr.Contains("b") || verStr.Contains("rc")) continue;
                                try
                                {
                                    var v = new Version(verStr);
                                    if (v > bestVersion)
                                    {
                                        bestVersion = v;
                                        // Fix relative URLs — python.org uses href="/ftp/..." (no domain)
                                        bestUrl = m.Groups[1].Value;
                                        if (bestUrl.StartsWith("/"))
                                            bestUrl = "https://www.python.org" + bestUrl;
                                    }
                                }
                                catch { }
                            }
                            if (!string.IsNullOrEmpty(bestUrl)) return bestUrl;
                        }
                    }
                    else if (rtype == "cpuz")
                    {
                        string html = client.DownloadString("https://www.cpuid.com/softwares/cpu-z.html");
                        var match = Regex.Match(html, @"href=""([^""]*cpu-z_([0-9.]+)-en\.exe)""", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            string path = match.Groups[1].Value.Replace("/downloads/", "/");
                            return "https://download.cpuid.com" + path;
                        }
                    }
                    else if (rtype == "winrar")
                    {
                        string html = client.DownloadString("https://www.rarlab.com/download.htm");
                        var match = Regex.Match(html, @"href=""([^""]*winrar-x64-[^""]*\.exe)""", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            string url = match.Groups[1].Value;
                            return url.StartsWith("/") ? "https://www.rarlab.com" + url : url;
                        }
                    }
                    else if (rtype == "winscp")
                    {
                        string html = client.DownloadString("https://winscp.net/eng/download.php");
                        var match = Regex.Match(html, @"href=""([^""]*/download/WinSCP-([^""]*)-Setup\.exe/download)""", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            string landingUrl = "https://winscp.net" + match.Groups[1].Value;
                            string landingHtml = client.DownloadString(landingUrl);
                            var match2 = Regex.Match(landingHtml, @"href=""(https://cdn\.winscp\.net/files/WinSCP-[^""]+)""", RegexOptions.IgnoreCase);
                            if (match2.Success)
                            {
                                return match2.Groups[1].Value.Replace("&amp;", "&");
                            }
                            return landingUrl;
                        }
                    }
                    else if (rtype == "filezilla")
                    {
                        // FileZilla update API returns download_x64="URL" as XML attribute (not <url> tags)
                        string xml = client.DownloadString("https://update.filezilla-project.org/update.php?platform=win64&type=client&version=0&bits=64");
                        var match = Regex.Match(xml, @"download_x64=""(https://download\.filezilla-project\.org/client/[^""]+\.exe)""", RegexOptions.IgnoreCase);
                        if (!match.Success)
                            match = Regex.Match(xml, @"\bdownload=""(https://download\.filezilla-project\.org/client/[^""]+\.exe)""", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            return match.Groups[1].Value;
                        }
                    }
                    else if (rtype == "putty")
                    {
                        string html = client.DownloadString("https://www.chiark.greenend.org.uk/~sgtatham/putty/latest.html");
                        var match = Regex.Match(html, @"href=""([^""]*putty-64bit-[^""]*\.msi)""", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            return match.Groups[1].Value;
                        }
                    }
                    else if (rtype == "plex")
                    {
                        string json = client.DownloadString("https://plex.tv/api/downloads/6.json");
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("computer", out var comp) &&
                            comp.TryGetProperty("Windows", out var win) &&
                            win.TryGetProperty("releases", out var releases) &&
                            releases.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var rel in releases.EnumerateArray())
                            {
                                string build = rel.TryGetProperty("build", out var buildEl) ? (buildEl.GetString() ?? "") : "";
                                if (build.Contains("x86_64"))
                                {
                                    return rel.TryGetProperty("url", out var urlEl) ? urlEl.GetString() : null;
                                }
                            }
                        }
                    }
                    else if (rtype == "deezer")
                    {
                        var request = WebRequest.Create("https://www.deezer.com/desktop/download?platform=win32&architecture=x86");
                        request.Method = "HEAD";
                        using (var response = request.GetResponse())
                        {
                            return response.ResponseUri.AbsoluteUri;
                        }
                    }
                }
            }
            catch { }

            return !string.IsNullOrEmpty(direct) ? direct : app.official_url;
        }

        public static string[] GetSilentArgs(string dest, AppInfo app)
        {
            string ext = Path.GetExtension(dest).ToLower();
            if (app.installer_args != null && app.installer_args.Count > 0)
            {
                return app.installer_args.ToArray();
            }
            if (ext == ".msi")
            {
                return new[] { "/i", dest, "/qn", "/norestart" };
            }
            return new[] { "/S" };
        }

        public static bool HasWinget()
        {
            try
            {
                string wingetBin = Detection.FindWingetPath();
                var startInfo = new ProcessStartInfo
                {
                    FileName = wingetBin,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var process = Process.Start(startInfo))
                {
                    return process != null && process.WaitForExit(5000) && process.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        public static string EscapePsString(string s)
        {
            return s.Replace("'", "''");
        }

        public static string GenerateNiniteScript(List<Tuple<AppInfo, string>> appsWithSource)
        {
            string scriptPath = Path.Combine(TempDir, "InstallPilot_Setup.ps1");
            var lines = new List<string>
            {
                "$ProgressPreference = 'SilentlyContinue'",
                "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8",
                "Write-Host '" + EscapePsString(I18n.tr("sc_title")) + "' -ForegroundColor Cyan",
                "Write-Host '" + EscapePsString(I18n.tr("sc_warning")) + "' -ForegroundColor Yellow",
                "Write-Host ''",
                "$StatusDir = '" + TempDir + "'",
                // Winget detection at PS runtime — not at C# generation time
                "$WingetBin = $null",
                "$_wgCmd = Get-Command winget -ErrorAction SilentlyContinue",
                "if ($_wgCmd -ne $null) { $WingetBin = $_wgCmd.Source }"
            };

            foreach (var item in appsWithSource)
            {
                var app = item.Item1;
                string source = item.Item2;
                string app_id = app.id;
                string name = app.GetName(I18n.lang_code);
                string wid = app.winget_id;

                lines.Add("try {");

                string storeId = null;
                if (source == "store")
                {
                    if (!string.IsNullOrEmpty(wid) && (wid.Length == 12 || wid.Length == 14) && Regex.IsMatch(wid, "^[a-zA-Z0-9]+$"))
                    {
                        storeId = wid;
                    }
                    else
                    {
                        string storeUrl = app.store_url ?? "";
                        var match = Regex.Match(storeUrl, @"(?:/detail/|ProductId=)([a-zA-Z0-9]{12,14})", RegexOptions.IgnoreCase);
                        if (match.Success)
                            storeId = match.Groups[1].Value.ToUpper();
                    }
                }

                if (!string.IsNullOrEmpty(storeId))
                {
                    lines.Add("  'installing' | Out-File \"$StatusDir\\app_" + app_id + ".status\" -Encoding utf8");
                    lines.Add("  Write-Host '" + EscapePsString(I18n.tr("sc_install_store", "name", name)) + "' -ForegroundColor Green");
                    string storeDlDest = "$StatusDir\\StoreInstaller_" + storeId + ".exe";
                    foreach (var dl in GeneratePowershellDownload("https://get.microsoft.com/installer/download/" + storeId, storeDlDest))
                        lines.Add("  " + dl);
                    lines.Add("  $proc = Start-Process -FilePath \"" + storeDlDest + "\" -ArgumentList '-silent' -PassThru -NoNewWindow");
                    lines.Add("  $proc.WaitForExit(300000) | Out-Null");
                    lines.Add("  Remove-Item -Path \"" + storeDlDest + "\" -Force -ErrorAction SilentlyContinue");
                    GenerateValidationBlock(lines, app, app_id);
                }
                else if (!string.IsNullOrEmpty(wid) && source != "store")
                {
                    // EXE install: winget if available at PS runtime, else direct download fallback
                    string dlUrl = ResolveDownloadUrl(app);

                    lines.Add("  if ($WingetBin) {");
                    lines.Add("    'installing' | Out-File \"$StatusDir\\app_" + app_id + ".status\" -Encoding utf8");
                    lines.Add("    Write-Host '" + EscapePsString(I18n.tr("sc_install_winget", "name", name)) + "' -ForegroundColor Green");
                    lines.Add("    $proc = Start-Process -FilePath $WingetBin -ArgumentList 'install', '--id', '`\"" + wid + "`\"', '-e', '--accept-source-agreements', '--accept-package-agreements', '--silent' -PassThru -NoNewWindow");
                    lines.Add("    $timeoutMs = if ('" + app_id + "' -match 'discord|teams|whatsapp|steam') { 60000 } else { 300000 }");
                    lines.Add("    $proc.WaitForExit($timeoutMs) | Out-Null");
                    lines.Add("  } else {");

                    if (!string.IsNullOrEmpty(dlUrl))
                    {
                        string dlExt = ".exe";
                        try { dlExt = Path.GetExtension(new Uri(dlUrl).AbsolutePath).ToLower(); } catch { }
                        if (string.IsNullOrEmpty(dlExt))
                            dlExt = dlUrl.ToLower().Contains("msi") ? ".msi" : dlUrl.ToLower().Contains("msix") ? ".msix" : ".exe";

                        string dlDest = "$StatusDir\\InstallPilot_Temp_" + app_id + dlExt;
                        lines.Add("    'downloading' | Out-File \"$StatusDir\\app_" + app_id + ".status\" -Encoding utf8");
                        lines.Add("    Write-Host '" + EscapePsString(I18n.tr("sc_downloading", "name", name)) + "' -ForegroundColor Green");

                        if (dlExt == ".zip" || dlExt == ".nupkg")
                        {
                            string destZip = "$StatusDir\\InstallPilot_Temp_" + app_id + ".zip";
                            string destDir = "$StatusDir\\InstallPilot_Temp_" + app_id + "_extracted";
                            lines.Add("    Remove-Item -Path \"" + destDir + "\" -Recurse -Force -ErrorAction SilentlyContinue");
                            foreach (var dl in GeneratePowershellDownload(dlUrl, destZip)) lines.Add("  " + dl);
                            lines.Add("    'installing' | Out-File \"$StatusDir\\app_" + app_id + ".status\" -Encoding utf8");
                            lines.Add("    Write-Host '" + EscapePsString(I18n.tr("sc_installing", "name", name)) + "' -ForegroundColor Green");
                            lines.Add("    Expand-Archive -Path \"" + destZip + "\" -DestinationPath \"" + destDir + "\" -Force");
                            lines.Add("    $setup = (Get-ChildItem -Path \"" + destDir + "\" -Filter \"*.exe\" -Recurse | Select-Object -First 1).FullName");
                            string[] zargs = GetSilentArgs("setup.exe", app);
                            string zargsStr = string.Join(" ", zargs);
                            lines.Add(zargsStr.Length > 0
                                ? "    $proc = Start-Process -FilePath $setup -ArgumentList '" + zargsStr + "' -PassThru -NoNewWindow"
                                : "    $proc = Start-Process -FilePath $setup -PassThru -NoNewWindow");
                            lines.Add("    $proc.WaitForExit(300000) | Out-Null");
                            lines.Add("    Remove-Item -Path \"" + destZip + "\" -Force -ErrorAction SilentlyContinue");
                            lines.Add("    Remove-Item -Path \"" + destDir + "\" -Recurse -Force -ErrorAction SilentlyContinue");
                        }
                        else
                        {
                            foreach (var dl in GeneratePowershellDownload(dlUrl, dlDest)) lines.Add("  " + dl);
                            lines.Add("    'installing' | Out-File \"$StatusDir\\app_" + app_id + ".status\" -Encoding utf8");
                            lines.Add("    Write-Host '" + EscapePsString(I18n.tr("sc_installing", "name", name)) + "' -ForegroundColor Green");
                            if (dlExt == ".msix" || dlExt == ".msixbundle" || dlExt == ".appx" || dlExt == ".appxbundle")
                                lines.Add("    $proc = Start-Process -FilePath 'powershell.exe' -ArgumentList '-Command', \"Add-AppxPackage -Path '" + dlDest + "'\" -PassThru -NoNewWindow");
                            else if (dlExt == ".msi")
                                lines.Add("    $proc = Start-Process -FilePath 'msiexec.exe' -ArgumentList '/i', \"" + dlDest + "\", '/qn', '/norestart' -PassThru -NoNewWindow");
                            else
                            {
                                string[] dargs = GetSilentArgs(dlDest, app);
                                string dargsStr = string.Join(" ", dargs);
                                lines.Add(dargsStr.Length > 0
                                    ? "    $proc = Start-Process -FilePath \"" + dlDest + "\" -ArgumentList '" + dargsStr + "' -PassThru -NoNewWindow"
                                    : "    $proc = Start-Process -FilePath \"" + dlDest + "\" -PassThru -NoNewWindow");
                            }
                            lines.Add("    $proc.WaitForExit(300000) | Out-Null");
                        }
                    }
                    else
                    {
                        lines.Add("    'error: winget indisponible et aucune source de telechargement' | Out-File \"$StatusDir\\app_" + app_id + ".status\" -Encoding utf8");
                    }

                    lines.Add("  }");
                    GenerateValidationBlock(lines, app, app_id);
                }
                else
                {
                    // No winget_id — pure download path
                    string url = ResolveDownloadUrl(app);
                    if (!string.IsNullOrEmpty(url))
                    {
                        lines.Add("  'downloading' | Out-File \"$StatusDir\\app_" + app_id + ".status\" -Encoding utf8");
                        lines.Add("  Write-Host '" + EscapePsString(I18n.tr("sc_downloading", "name", name)) + "' -ForegroundColor Green");

                        string ext = ".exe";
                        try { ext = Path.GetExtension(new Uri(url).AbsolutePath).ToLower(); } catch { }
                        if (string.IsNullOrEmpty(ext))
                            ext = url.ToLower().Contains("msi") ? ".msi" : url.ToLower().Contains("msix") ? ".msix" : ".exe";

                        string dest = "$StatusDir\\InstallPilot_Temp_" + app_id + ext;
                        if (ext == ".nupkg" || ext == ".zip")
                        {
                            string destZip = "$StatusDir\\InstallPilot_Temp_" + app_id + ".zip";
                            string destDir = "$StatusDir\\InstallPilot_Temp_" + app_id + "_extracted";
                            lines.Add("  Remove-Item -Path \"" + destDir + "\" -Recurse -Force -ErrorAction SilentlyContinue");
                            lines.AddRange(GeneratePowershellDownload(url, destZip));
                            lines.Add("  'installing' | Out-File \"$StatusDir\\app_" + app_id + ".status\" -Encoding utf8");
                            lines.Add("  Write-Host '" + EscapePsString(I18n.tr("sc_installing", "name", name)) + "' -ForegroundColor Green");
                            lines.Add("  Expand-Archive -Path \"" + destZip + "\" -DestinationPath \"" + destDir + "\" -Force");
                            lines.Add("  $setup = (Get-ChildItem -Path \"" + destDir + "\" -Filter \"*.exe\" -Recurse | Select-Object -First 1).FullName");
                            string[] sargs = GetSilentArgs("setup.exe", app);
                            string argsStr = string.Join(" ", sargs);
                            lines.Add(argsStr.Length > 0
                                ? "  $proc = Start-Process -FilePath $setup -ArgumentList '" + argsStr + "' -PassThru -NoNewWindow"
                                : "  $proc = Start-Process -FilePath $setup -PassThru -NoNewWindow");
                        }
                        else
                        {
                            lines.AddRange(GeneratePowershellDownload(url, dest));
                            lines.Add("  'installing' | Out-File \"$StatusDir\\app_" + app_id + ".status\" -Encoding utf8");
                            lines.Add("  Write-Host '" + EscapePsString(I18n.tr("sc_installing", "name", name)) + "' -ForegroundColor Green");
                            if (ext == ".msix" || ext == ".msixbundle" || ext == ".appx" || ext == ".appxbundle")
                                lines.Add("  $proc = Start-Process -FilePath 'powershell.exe' -ArgumentList '-Command', \"Add-AppxPackage -Path '" + dest + "'\" -PassThru -NoNewWindow");
                            else if (ext == ".msi")
                                lines.Add("  $proc = Start-Process -FilePath 'msiexec.exe' -ArgumentList '/i', \"" + dest + "\", '/qn', '/norestart' -PassThru -NoNewWindow");
                            else
                            {
                                string[] sargs = GetSilentArgs(dest, app);
                                string argsStr = string.Join(" ", sargs);
                                lines.Add(argsStr.Length > 0
                                    ? "  $proc = Start-Process -FilePath \"" + dest + "\" -ArgumentList '" + argsStr + "' -PassThru -NoNewWindow"
                                    : "  $proc = Start-Process -FilePath \"" + dest + "\" -PassThru -NoNewWindow");
                            }
                        }

                        lines.Add("  $timeoutMs = if ('" + app_id + "' -match 'discord|teams|whatsapp|steam') { 60000 } else { 300000 }");
                        lines.Add("  $proc.WaitForExit($timeoutMs) | Out-Null");

                        if (ext == ".nupkg" || ext == ".zip")
                        {
                            lines.Add("  Remove-Item -Path \"$StatusDir\\InstallPilot_Temp_" + app_id + ".zip\" -Force -ErrorAction SilentlyContinue");
                            lines.Add("  Remove-Item -Path \"$StatusDir\\InstallPilot_Temp_" + app_id + "_extracted\" -Recurse -Force -ErrorAction SilentlyContinue");
                        }

                        GenerateValidationBlock(lines, app, app_id);
                    }
                    else
                    {
                        lines.Add("  'error: Aucun lien trouve' | Out-File \"$StatusDir\\app_" + app_id + ".status\" -Encoding utf8");
                        lines.Add("  Write-Host '" + EscapePsString(I18n.tr("sc_no_link", "name", name)) + "' -ForegroundColor Red");
                        string fallbackUrl = !string.IsNullOrEmpty(app.store_url) ? app.store_url : app.official_url;
                        if (!string.IsNullOrEmpty(fallbackUrl))
                            lines.Add("  Start-Process '" + fallbackUrl + "'");
                    }
                }

                lines.Add("} catch {");
                lines.Add("  \"error: $($_.Exception.Message)\" | Out-File \"$StatusDir\\app_" + app_id + ".status\" -Encoding utf8");
                lines.Add("}");
            }

            lines.Add("Write-Host ''");
            lines.Add("Write-Host '" + EscapePsString(I18n.tr("sc_done")) + "' -ForegroundColor Cyan");
            lines.Add("'done' | Out-File \"$StatusDir\\script.done\" -Encoding utf8");
            lines.Add("Start-Sleep -Seconds 3");

            File.WriteAllLines(scriptPath, lines, Encoding.UTF8);
            return scriptPath;
        }

        public static string GenerateFallbackBatScript(List<Tuple<AppInfo, string>> appsWithSource)
        {
            string scriptPath = Path.Combine(TempDir, "InstallPilot_Setup_Fallback.bat");
            var lines = new List<string>
            {
                "@echo off",
                "chcp 65001 >nul",
                "echo " + I18n.tr("sc_fallback_mode"),
                "echo " + I18n.tr("sc_title"),
                "echo.",
                "set \"StatusDir=" + TempDir + "\""
            };

            bool hasWinget = HasWinget();
            string wingetBin = Detection.FindWingetPath();

            foreach (var item in appsWithSource)
            {
                var app = item.Item1;
                string source = item.Item2;
                string app_id = app.id;
                string name = app.GetName(I18n.lang_code);
                string wid = app.winget_id;

                lines.Add("echo installing > \"%StatusDir%\\app_" + app_id + ".status\"");
                lines.Add("echo " + I18n.tr("sc_installing", "name", name));

                string storeId = null;
                if (source == "store")
                {
                    if (!string.IsNullOrEmpty(wid) && (wid.Length == 12 || wid.Length == 14) && Regex.IsMatch(wid, "^[a-zA-Z0-9]+$"))
                    {
                        storeId = wid;
                    }
                    else
                    {
                        string storeUrl = app.store_url ?? "";
                        var match = Regex.Match(storeUrl, @"(?:/detail/|ProductId=)([a-zA-Z0-9]{12,14})", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            storeId = match.Groups[1].Value.ToUpper();
                        }
                    }
                }

                if (!string.IsNullOrEmpty(storeId))
                {
                    string dest = "%StatusDir%\\StoreInstaller_" + storeId + ".exe";
                    lines.Add("curl -A \"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36\" -L --retry 3 --retry-delay 2 -o \"" + dest + "\" \"https://get.microsoft.com/installer/download/" + storeId + "\"");
                    lines.Add("\"" + dest + "\" -silent");
                    lines.Add("del /f /q \"" + dest + "\"");
                }
                else if (!string.IsNullOrEmpty(wid) && source != "store")
                {
                    lines.Add("\"" + wingetBin + "\" install --id \"" + wid + "\" -e --accept-source-agreements --accept-package-agreements --silent");
                }
                else
                {
                    string url = ResolveDownloadUrl(app);
                    if (!string.IsNullOrEmpty(url))
                    {
                        string ext = ".exe";
                        try
                        {
                            var uri = new Uri(url);
                            ext = Path.GetExtension(uri.AbsolutePath).ToLower();
                        }
                        catch { }
                        if (string.IsNullOrEmpty(ext))
                        {
                            ext = url.ToLower().Contains("msi") ? ".msi" : url.ToLower().Contains("msix") ? ".msix" : ".exe";
                        }

                        string dest = "%StatusDir%\\InstallPilot_Temp_" + app_id + ext;
                        lines.Add("curl -A \"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36\" -L --retry 3 --retry-delay 2 -o \"" + dest + "\" \"" + url + "\"");
                        
                        string[] sargs = GetSilentArgs(dest, app);
                        if (ext == ".msix" || ext == ".msixbundle" || ext == ".appx" || ext == ".appxbundle")
                        {
                            lines.Add("powershell.exe -Command \"Add-AppxPackage -Path '" + dest + "'\"");
                        }
                        else if (ext == ".msi")
                        {
                            lines.Add("msiexec.exe /i \"" + dest + "\" /qn /norestart");
                        }
                        else
                        {
                            string argsStr = "";
                            foreach (var arg in sargs) argsStr += " \"" + arg + "\"";
                            lines.Add("\"" + dest + "\" " + argsStr);
                        }
                        lines.Add("del /f /q \"" + dest + "\"");
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(wid))
                        {
                            lines.Add("\"" + wingetBin + "\" install --id \"" + wid + "\" -e --accept-source-agreements --accept-package-agreements --silent");
                        }
                        else
                        {
                            lines.Add("echo error: Aucun lien ou winget ID trouve > \"%StatusDir%\\app_" + app_id + ".status\"");
                            lines.Add("goto next_" + app_id);
                        }
                    }
                }

                lines.Add("if %errorlevel% equ 0 goto success_" + app_id);
                lines.Add("if %errorlevel% equ 3010 goto success_" + app_id);
                lines.Add("echo error: Code %errorlevel% > \"%StatusDir%\\app_" + app_id + ".status\"");
                lines.Add("goto next_" + app_id);
                lines.Add(":success_" + app_id);
                lines.Add("echo success > \"%StatusDir%\\app_" + app_id + ".status\"");
                lines.Add(":next_" + app_id);
            }

            lines.Add("echo done > \"%StatusDir%\\script.done\"");
            lines.Add("echo " + I18n.tr("sc_done"));
            lines.Add("timeout /t 3 >nul");

            File.WriteAllLines(scriptPath, lines, Encoding.Default); // Use system default encoding for batch
            return scriptPath;
        }

        private static void GenerateValidationBlock(List<string> lines, AppInfo app, string app_id)
        {
            bool hasChecks = (app.check_paths != null && app.check_paths.Count > 0) ||
                             (app.registry_names != null && app.registry_names.Count > 0) ||
                             (app.appx_names != null && app.appx_names.Count > 0);

            if (!hasChecks || app.skip_validation)
            {
                lines.Add("  if (-not $proc.HasExited -or $proc.ExitCode -eq 0 -or $proc.ExitCode -eq 3010 -or $null -eq $proc.ExitCode) {");
                lines.Add("    'success' | Out-File \"$StatusDir\\app_" + app_id + ".status\" -Encoding utf8");
                lines.Add("  } else {");
                lines.Add("    \"error: Code $($proc.ExitCode)\" | Out-File \"$StatusDir\\app_" + app_id + ".status\" -Encoding utf8");
                lines.Add("  }");
                return;
            }

            lines.Add("  $isInstalled = $false");
            lines.Add("  for ($retry = 0; $retry -lt 30; $retry++) {");

            // 1. Path checks
            if (app.check_paths != null)
            {
                foreach (var path in app.check_paths)
                {
                    string psPath = path.Replace("%LOCALAPPDATA%", "$env:LOCALAPPDATA")
                                        .Replace("%APPDATA%", "$env:APPDATA")
                                        .Replace("%PROGRAMFILES%", "$env:ProgramFiles")
                                        .Replace("/", "\\");
                    lines.Add("    if (Test-Path \"" + psPath + "\") { $isInstalled = $true }");
                }
            }

            // 2. Registry checks
            if (app.registry_names != null && app.registry_names.Count > 0)
            {
                string namesPs = string.Join(", ", app.registry_names.ConvertAll(n => "'" + EscapePsString(n) + "'"));
                lines.Add("    if (-not $isInstalled) {");
                lines.Add("      $regNames = @(" + namesPs + ")");
                lines.Add("      $paths = @('HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\*', 'HKLM:\\SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\*', 'HKCU:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\*')");
                lines.Add("      foreach ($p in $paths) {");
                lines.Add("        Get-ItemProperty $p -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -ne $null } | ForEach-Object {");
                lines.Add("          foreach ($n in $regNames) { if ($_.DisplayName -like \"*$n*\") { $isInstalled = $true; break } }");
                lines.Add("        }");
                lines.Add("        if ($isInstalled) { break }");
                lines.Add("      }");
                lines.Add("    }");
            }

            // 3. Appx checks
            if (app.appx_names != null && app.appx_names.Count > 0)
            {
                string namesPs = string.Join(", ", app.appx_names.ConvertAll(n => "'" + EscapePsString(n) + "'"));
                lines.Add("    if (-not $isInstalled) {");
                lines.Add("      $appxNames = @(" + namesPs + ")");
                lines.Add("      foreach ($n in $appxNames) {");
                lines.Add("        if (Get-AppxPackage -Name \"*$n*\" -ErrorAction SilentlyContinue) { $isInstalled = $true; break }");
                lines.Add("      }");
                lines.Add("    }");
            }

            lines.Add("    if ($isInstalled) { break }");
            lines.Add("    Start-Sleep -Seconds 1");
            lines.Add("  }");

            lines.Add("  if ($isInstalled) {");
            lines.Add("    'success' | Out-File \"$StatusDir\\app_" + app_id + ".status\" -Encoding utf8");
            lines.Add("  } else {");
            lines.Add("    if (-not $proc.HasExited) {");
            lines.Add("      \"error: Delai depasse et installation non detectee\" | Out-File \"$StatusDir\\app_" + app_id + ".status\" -Encoding utf8");
            lines.Add("    } elseif ($proc.ExitCode -ne 0 -and $proc.ExitCode -ne 3010 -and $null -ne $proc.ExitCode) {");
            lines.Add("      \"error: Code $($proc.ExitCode)\" | Out-File \"$StatusDir\\app_" + app_id + ".status\" -Encoding utf8");
            lines.Add("    } else {");
            lines.Add("      \"error: Installation non detectee apres execution\" | Out-File \"$StatusDir\\app_" + app_id + ".status\" -Encoding utf8");
            lines.Add("    }");
            lines.Add("  }");
        }

        private static List<string> GeneratePowershellDownload(string url, string dest)
        {
            return new List<string>
            {
                "  $dlSuccess = $false",
                "  $dlHeaders = @{ 'Referer' = 'https://sourceforge.net/'; 'Accept' = '*/*' }",
                "  for ($dlRetry = 0; $dlRetry -lt 3; $dlRetry++) {",
                "    try {",
                "      Invoke-WebRequest -Uri '" + url + "' -OutFile \"" + dest + "\" -UserAgent 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36' -Headers $dlHeaders -UseBasicParsing -MaximumRedirection 10 -ErrorAction Stop",
                "      $dlSuccess = $true",
                "      break",
                "    } catch {",
                "      Start-Sleep -Seconds 2",
                "    }",
                "  }",
                "  if (-not $dlSuccess) { throw \"Failed to download from " + url + "\" }"
            };
        }

        public static string GenerateUpdateAllScript()
        {
            string scriptPath = Path.Combine(TempDir, "InstallPilot_UpdateAll.ps1");
            var lines = new List<string>
            {
                "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8",
                "Write-Host '" + EscapePsString(I18n.tr("sc_update_all")) + "' -ForegroundColor Cyan",
                "Write-Host '" + EscapePsString(I18n.tr("sc_update_warning")) + "' -ForegroundColor Yellow",
                "Write-Host '" + EscapePsString(I18n.tr("sc_warning")) + "' -ForegroundColor Yellow",
                "Write-Host ''",
                "winget upgrade --all --include-unknown --accept-source-agreements --accept-package-agreements",
                "Write-Host ''",
                "Write-Host '" + EscapePsString(I18n.tr("sc_cleanup")) + "' -ForegroundColor DarkGray",
                "Remove-Item -Path '" + TempDir + "' -Recurse -Force -ErrorAction SilentlyContinue",
                "Write-Host '" + EscapePsString(I18n.tr("sc_update_done")) + "' -ForegroundColor Cyan",
                "Start-Sleep -Seconds 3"
            };

            File.WriteAllLines(scriptPath, lines, Encoding.UTF8);
            return scriptPath;
        }

        public static string GenerateSelectiveUpdateScript(List<Dictionary<string, string>> updates)
        {
            string scriptPath = Path.Combine(TempDir, "InstallPilot_UpdateSelection.ps1");
            var lines = new List<string>
            {
                "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8",
                "Write-Host '" + EscapePsString(I18n.tr("sc_update_sel")) + "' -ForegroundColor Cyan",
                "Write-Host '" + EscapePsString(I18n.tr("sc_warning")) + "' -ForegroundColor Yellow",
                "Write-Host ''"
            };

            foreach (var up in updates)
            {
                string wid = up["id"];
                string source = up["source"].ToLower();
                string name = up["name"];

                if (source == "msstore")
                {
                    lines.Add("Write-Host '" + EscapePsString(I18n.tr("sc_update_store", "name", name)) + "' -ForegroundColor Green");
                    string dest = TempDir + "\\InstallPilot_Store_" + wid + ".exe";
                    string storeDlUrl = "https://get.microsoft.com/installer/download/" + wid;
                    lines.AddRange(GeneratePowershellDownload(storeDlUrl, dest));
                    lines.Add("Write-Host '" + EscapePsString(I18n.tr("sc_update_store_warn", "name", name)) + "' -ForegroundColor Yellow");
                    lines.Add("Start-Process -FilePath \"" + dest + "\" -Wait");
                }
                else
                {
                    lines.Add("Write-Host '" + EscapePsString(I18n.tr("sc_update_winget", "name", name)) + "' -ForegroundColor Green");
                    lines.Add("winget upgrade --id `\"" + wid + "`\" -e --accept-source-agreements --accept-package-agreements --silent");
                }
            }

            lines.AddRange(new[]
            {
                "Write-Host ''",
                "Write-Host '" + EscapePsString(I18n.tr("sc_cleanup")) + "' -ForegroundColor DarkGray",
                "Remove-Item -Path '" + TempDir + "' -Recurse -Force -ErrorAction SilentlyContinue",
                "Write-Host '" + EscapePsString(I18n.tr("sc_update_done")) + "' -ForegroundColor Cyan",
                "Start-Sleep -Seconds 3"
            });

            File.WriteAllLines(scriptPath, lines, Encoding.UTF8);
            return scriptPath;
        }

        public static void GenerateSaveScriptFile(List<Tuple<AppInfo, string>> appsWithSource)
        {
            var dialog = new Forms.SaveFileDialog
            {
                Title = I18n.tr("inst_save_script"),
                FileName = "InstallPilot_Installer.bat",
                DefaultExt = ".bat",
                Filter = "Batch script (*.bat)|*.bat|All files (*.*)|*.*",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };

            if (dialog.ShowDialog() == Forms.DialogResult.OK)
            {
                var lines = new List<string>
                {
                    "@echo off",
                    "title InstallPilot — Script d'installation",
                    "echo Genere le " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                    "echo.",
                };

                foreach (var item in appsWithSource)
                {
                    var app = item.Item1;
                    string source = item.Item2;
                    string name = app.GetName(I18n.lang_code);
                    string wid = app.winget_id;
                    string dlUrl = !string.IsNullOrEmpty(app.download_url) ? app.download_url : app.official_url;

                    if (!string.IsNullOrEmpty(wid))
                    {
                        lines.Add("echo Installation de " + name + "...");
                        lines.Add("winget install --id \"" + wid + "\" -e --accept-source-agreements --accept-package-agreements --silent");
                        lines.Add("echo.");
                    }
                    else if (!string.IsNullOrEmpty(dlUrl))
                    {
                        lines.Add("echo Ouverture de " + name + "...");
                        lines.Add("start \"\" \"" + dlUrl + "\"");
                        lines.Add("echo.");
                    }
                }
                lines.Add("echo Termine !");
                lines.Add("pause");

                File.WriteAllLines(dialog.FileName, lines, Encoding.Default);
                Forms.MessageBox.Show(I18n.tr("inst_script_saved", "path", dialog.FileName), I18n.tr("inst_save_script"), Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Information);
            }
        }
    }
}
