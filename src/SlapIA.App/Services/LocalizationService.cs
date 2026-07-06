using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace SlapIA.App.Services;

/// <summary>
/// Live-switchable UI translations. XAML consumes this via the {loc:Tr Key} markup extension
/// (a Binding on this[key]); C# code (ViewModels/Services building composite strings) calls
/// LocalizationService.Instance["Key"] directly. Switching language raises PropertyChanged for
/// the "Item[]" indexer, which WPF's binding engine treats as "re-evaluate every {loc:Tr ...}
/// binding" - no window/view reload needed.
/// </summary>
public class LocalizationService : INotifyPropertyChanged
{
    public static LocalizationService Instance { get; } = new();

    public const string French = "fr";
    public const string English = "en";

    private string _language;

    public event PropertyChangedEventHandler? PropertyChanged;

    private LocalizationService()
    {
        _language = LoadSavedLanguage() ?? DetectDefaultLanguage();
    }

    public string CurrentLanguage => _language;

    public string this[string key] =>
        Translations.TryGetValue(_language, out var dict) && dict.TryGetValue(key, out var value)
            ? value
            : Translations[French].GetValueOrDefault(key, key);

    public void SetLanguage(string language)
    {
        if (language == _language || !Translations.ContainsKey(language))
            return;

        _language = language;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguage)));
        SaveLanguage(language);
    }

    private static string DetectDefaultLanguage()
    {
        var iso = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return iso == French ? French : English;
    }

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SlapIA.Tool", "settings.json");

    private static string? LoadSavedLanguage()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(SettingsPath));
            return doc.RootElement.TryGetProperty("language", out var lang) ? lang.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    private static void SaveLanguage(string language)
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new { language }));
        }
        catch
        {
            // Best-effort only; the app just re-detects the default language next launch.
        }
    }

    private static readonly Dictionary<string, Dictionary<string, string>> Translations = new()
    {
        [French] = new()
        {
            // Sidebar / navigation
            ["Sidebar_Section_General"] = "GENERAL",
            ["Nav_Overview"] = "Vue d'ensemble",
            ["Nav_Hardware"] = "Materiel",
            ["Nav_Monitoring"] = "Monitoring",
            ["Nav_Software"] = "Logiciels installes",
            ["Sidebar_CheckUpdates"] = "Verifier les mises a jour",

            // Page titles (also used by MainViewModel.CurrentPageTitle)
            ["Page_Overview"] = "Vue d'ensemble",
            ["Page_Hardware"] = "Materiel",
            ["Page_Monitoring"] = "Monitoring temps reel",
            ["Page_Software"] = "Logiciels installes",

            // Update flow
            ["Update_Checking"] = "Recherche de mises a jour...",
            ["Update_OnlyInstalled"] = "Mise a jour disponible uniquement pour la version installee (setup.exe).",
            ["Update_AlreadyLatest"] = "Vous utilisez deja la derniere version.",
            ["Update_Downloading"] = "Telechargement de la version {0}...",
            ["Update_DownloadingProgress"] = "Telechargement... {0}%",
            ["Update_Failed"] = "Echec de la mise a jour : {0}",

            // Overview page
            ["Overview_Computer"] = "Ordinateur",
            ["Overview_User"] = "Utilisateur : ",
            ["Overview_OperatingSystem"] = "Systeme d'exploitation",
            ["Overview_Version"] = "Version ",
            ["Overview_Uptime"] = "Temps de fonctionnement",
            ["Overview_Processor"] = "Processeur",
            ["Overview_Memory"] = "Memoire vive",
            ["Overview_GraphicsCard"] = "Carte graphique",
            ["Overview_PrimaryDisk"] = "Disque principal",
            ["Overview_Motherboard"] = "Carte mere",
            ["Overview_FreeOf"] = "{0:0.#} Go libres / {1:0.#} Go",
            ["Overview_Uptime_Days"] = "{0} j {1} h {2} min",
            ["Overview_Uptime_Hours"] = "{0} h {1} min",
            ["Overview_MemoryTotalPlain"] = "{0:0.#} Go",
            ["Overview_MemoryTotalWithType"] = "{0:0.#} Go ({1})",
            ["Overview_MemoryModules"] = "{0} barrette(s)",

            // Hardware page
            ["Hardware_Refresh"] = "Actualiser",
            ["Hardware_Processor"] = "Processeur",
            ["Hardware_Memory"] = "Memoire vive",
            ["Hardware_GraphicsCards"] = "Cartes graphiques",
            ["Hardware_Disks"] = "Disques physiques",
            ["Hardware_Volumes"] = "Volumes",
            ["Hardware_Network"] = "Reseau",
            ["Hardware_Motherboard"] = "Carte mere / BIOS",
            ["Hardware_Bios"] = "BIOS ",
            ["Hardware_CoresThreads"] = "{0} coeurs / {1} threads @ {2:0.00} GHz",
            ["Hardware_MemoryLine"] = "{0} barrette(s) - {1} - {2} - {3} MHz",
            ["Hardware_VramFormat"] = "{0:0.#} Go VRAM",
            ["Hardware_DiskLine"] = "{0:0.#} Go - {1} - {2}",
            ["Hardware_VolumeFree"] = "{0:0.#} Go libres sur ",
            ["Copy_Tooltip"] = "Copier",

            // Monitoring page
            ["Monitoring_Cpu"] = "CPU",
            ["Monitoring_Ram"] = "RAM",
            ["Monitoring_Disk"] = "Disque",
            ["Monitoring_Gpu"] = "GPU",
            ["Monitoring_DiskActivity"] = "Activite disque",
            ["Monitoring_UsedOfTotal"] = "{0:0.#} Go / {1:0.#} Go",
            ["Monitoring_TempNotAvailable"] = "N/A",
            ["Monitoring_TempTooltip"] = "Lancez SlapIA Tool en tant qu'administrateur pour afficher la temperature du processeur (necessaire pour acceder aux capteurs materiels).",
            ["Monitoring_EnableCpuTemp"] = "Activer",
            ["Monitoring_EnableCpuTempTooltip"] = "Autoriser un acces administrateur (une seule fois par session) pour lire la temperature du processeur, sans lancer toute l'application en administrateur.",
            ["Monitoring_ChartCpu"] = "CPU",
            ["Monitoring_ChartRam"] = "RAM",
            ["Monitoring_ChartDisk"] = "Disque",
            ["Monitoring_ChartGpu"] = "GPU",

            // Software page
            ["Software_SearchPlaceholder"] = "Rechercher un logiciel...",
            ["Software_Count"] = "{0} / {1} logiciels",
            ["Software_ColumnName"] = "Nom",
            ["Software_ColumnVersion"] = "Version",
            ["Software_ColumnPublisher"] = "Editeur",
            ["Software_ColumnInstallDate"] = "Installation",

            // Alerts (tray notifications)
            ["Alert_CpuTempTitle"] = "Temperature CPU elevee",
            ["Alert_CpuTempBody"] = "Le CPU atteint {0:0} degC.",
            ["Alert_GpuTempTitle"] = "Temperature GPU elevee",
            ["Alert_GpuTempBody"] = "Le GPU atteint {0:0} degC.",
            ["Alert_CpuUsageTitle"] = "Charge CPU elevee",
            ["Alert_CpuUsageBody"] = "Le CPU est utilise a {0:0} % depuis un moment.",

            // Language switcher
            ["Language_French"] = "FR",
            ["Language_English"] = "EN",
        },

        [English] = new()
        {
            ["Sidebar_Section_General"] = "GENERAL",
            ["Nav_Overview"] = "Overview",
            ["Nav_Hardware"] = "Hardware",
            ["Nav_Monitoring"] = "Monitoring",
            ["Nav_Software"] = "Installed software",
            ["Sidebar_CheckUpdates"] = "Check for updates",

            ["Page_Overview"] = "Overview",
            ["Page_Hardware"] = "Hardware",
            ["Page_Monitoring"] = "Real-time monitoring",
            ["Page_Software"] = "Installed software",

            ["Update_Checking"] = "Checking for updates...",
            ["Update_OnlyInstalled"] = "Updates are only available for the installed version (setup.exe).",
            ["Update_AlreadyLatest"] = "You're already on the latest version.",
            ["Update_Downloading"] = "Downloading version {0}...",
            ["Update_DownloadingProgress"] = "Downloading... {0}%",
            ["Update_Failed"] = "Update failed: {0}",

            ["Overview_Computer"] = "Computer",
            ["Overview_User"] = "User: ",
            ["Overview_OperatingSystem"] = "Operating system",
            ["Overview_Version"] = "Version ",
            ["Overview_Uptime"] = "Uptime",
            ["Overview_Processor"] = "Processor",
            ["Overview_Memory"] = "Memory",
            ["Overview_GraphicsCard"] = "Graphics card",
            ["Overview_PrimaryDisk"] = "Primary disk",
            ["Overview_Motherboard"] = "Motherboard",
            ["Overview_FreeOf"] = "{0:0.#} GB free / {1:0.#} GB",
            ["Overview_Uptime_Days"] = "{0}d {1}h {2}min",
            ["Overview_Uptime_Hours"] = "{0}h {1}min",
            ["Overview_MemoryTotalPlain"] = "{0:0.#} GB",
            ["Overview_MemoryTotalWithType"] = "{0:0.#} GB ({1})",
            ["Overview_MemoryModules"] = "{0} module(s)",

            ["Hardware_Refresh"] = "Refresh",
            ["Hardware_Processor"] = "Processor",
            ["Hardware_Memory"] = "Memory",
            ["Hardware_GraphicsCards"] = "Graphics cards",
            ["Hardware_Disks"] = "Physical disks",
            ["Hardware_Volumes"] = "Volumes",
            ["Hardware_Network"] = "Network",
            ["Hardware_Motherboard"] = "Motherboard / BIOS",
            ["Hardware_Bios"] = "BIOS ",
            ["Hardware_CoresThreads"] = "{0} cores / {1} threads @ {2:0.00} GHz",
            ["Hardware_MemoryLine"] = "{0} module(s) - {1} - {2} - {3} MHz",
            ["Hardware_VramFormat"] = "{0:0.#} GB VRAM",
            ["Hardware_DiskLine"] = "{0:0.#} GB - {1} - {2}",
            ["Hardware_VolumeFree"] = "{0:0.#} GB free of ",
            ["Copy_Tooltip"] = "Copy",

            ["Monitoring_Cpu"] = "CPU",
            ["Monitoring_Ram"] = "RAM",
            ["Monitoring_Disk"] = "Disk",
            ["Monitoring_Gpu"] = "GPU",
            ["Monitoring_DiskActivity"] = "Disk activity",
            ["Monitoring_UsedOfTotal"] = "{0:0.#} GB / {1:0.#} GB",
            ["Monitoring_TempNotAvailable"] = "N/A",
            ["Monitoring_TempTooltip"] = "Run SlapIA Tool as administrator to display the CPU temperature (required to access hardware sensors).",
            ["Monitoring_EnableCpuTemp"] = "Enable",
            ["Monitoring_EnableCpuTempTooltip"] = "Grant administrator access (once per session) to read the CPU temperature, without running the whole application as administrator.",
            ["Monitoring_ChartCpu"] = "CPU",
            ["Monitoring_ChartRam"] = "RAM",
            ["Monitoring_ChartDisk"] = "Disk",
            ["Monitoring_ChartGpu"] = "GPU",

            ["Software_SearchPlaceholder"] = "Search for an application...",
            ["Software_Count"] = "{0} / {1} applications",
            ["Software_ColumnName"] = "Name",
            ["Software_ColumnVersion"] = "Version",
            ["Software_ColumnPublisher"] = "Publisher",
            ["Software_ColumnInstallDate"] = "Installed on",

            ["Alert_CpuTempTitle"] = "High CPU temperature",
            ["Alert_CpuTempBody"] = "CPU has reached {0:0} degC.",
            ["Alert_GpuTempTitle"] = "High GPU temperature",
            ["Alert_GpuTempBody"] = "GPU has reached {0:0} degC.",
            ["Alert_CpuUsageTitle"] = "High CPU usage",
            ["Alert_CpuUsageBody"] = "CPU has been at {0:0}% usage for a while.",

            ["Language_French"] = "FR",
            ["Language_English"] = "EN",
        },
    };
}
