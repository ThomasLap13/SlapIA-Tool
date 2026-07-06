using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace InstallPilot
{
    public class DownloadResolver
    {
        public string type { get; set; }
        public string owner { get; set; }
        public string repo { get; set; }
        public string pattern { get; set; }
    }

    public class AppInfo
    {
        public string id { get; set; }
        public Dictionary<string, string> names { get; set; }
        public object description { get; set; } // Can be string or Dictionary<string, string>
        public string short_name { get; set; }
        public string color { get; set; }
        public string icon_path { get; set; }
        public string official_url { get; set; }
        public string download_url { get; set; }
        public string store_url { get; set; }
        public string winget_id { get; set; }
        public string category { get; set; }
        public List<string> registry_names { get; set; }
        public List<string> appx_names { get; set; }
        public List<string> check_paths { get; set; }
        public List<string> installer_args { get; set; }
        public bool skip_validation { get; set; }
        public DownloadResolver download_resolver { get; set; }

        public string GetName(string langCode)
        {
            if (names != null)
            {
                if (names.ContainsKey(langCode)) return names[langCode];
                if (names.ContainsKey("en")) return names["en"];
                foreach (var pair in names) return pair.Value; // Fallback to first name
            }
            return id;
        }

        public string GetDescription(string langCode)
        {
            if (description == null) return "";
            if (description is string s) return s;
            if (description is JsonElement je)
            {
                if (je.ValueKind == JsonValueKind.String) return je.GetString() ?? "";
                if (je.ValueKind == JsonValueKind.Object)
                {
                    if (je.TryGetProperty(langCode, out var v)) return v.GetString() ?? "";
                    if (je.TryGetProperty("fr", out var vfr)) return vfr.GetString() ?? "";
                    if (je.TryGetProperty("en", out var ven)) return ven.GetString() ?? "";
                    foreach (var prop in je.EnumerateObject()) return prop.Value.GetString() ?? "";
                }
            }
            return "";
        }
    }

    public class AppConfigContainer
    {
        public List<AppInfo> apps { get; set; }
    }

    public static class AppConfig
    {
        private static List<AppInfo> _cachedApps = null;

        public static List<AppInfo> LoadApps()
        {
            if (_cachedApps != null)
                return _cachedApps;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string check1 = Path.Combine(baseDir, "app_config.json");
            string check2 = "app_config.json";
            string check3 = Path.Combine(baseDir, "..", "app_config.json");

            string configPath = null;
            if (File.Exists(check1)) configPath = check1;
            else if (File.Exists(check2)) configPath = check2;
            else if (File.Exists(check3)) configPath = check3;

            string json = null;
            if (configPath != null)
            {
                try
                {
                    json = File.ReadAllText(configPath);
                }
                catch { }
            }

            if (json == null)
            {
                try
                {
                    var uri = new Uri("pack://application:,,,/InstallPilot/app_config.json", UriKind.Absolute);
                    var resourceStream = System.Windows.Application.GetResourceStream(uri);
                    if (resourceStream != null)
                    {
                        using (var reader = new StreamReader(resourceStream.Stream))
                        {
                            json = reader.ReadToEnd();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error loading embedded config: " + ex.Message);
                }
            }

            if (string.IsNullOrEmpty(json))
            {
                return new List<AppInfo>();
            }

            try
            {
                var container = JsonSerializer.Deserialize<AppConfigContainer>(json);
                _cachedApps = (container != null) ? container.apps : null;
                if (_cachedApps == null) _cachedApps = new List<AppInfo>();
                return _cachedApps;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error parsing config: " + ex.Message);
                return new List<AppInfo>();
            }
        }
    }
}
