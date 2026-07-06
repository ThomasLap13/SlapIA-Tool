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
    public const string Russian = "ru";
    public const string Chinese = "zh";

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
        return iso switch
        {
            French => French,
            Russian => Russian,
            Chinese => Chinese,
            _ => English,
        };
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
            ["Nav_InstallPilot"] = "InstallPilot",
            ["Sidebar_CheckUpdates"] = "Verifier les mises a jour",
            ["Sidebar_Preferences"] = "Preferences",

            // Page titles (also used by MainViewModel.CurrentPageTitle)
            ["Page_Overview"] = "Vue d'ensemble",
            ["Page_Hardware"] = "Materiel",
            ["Page_Monitoring"] = "Monitoring temps reel",
            ["Page_Software"] = "Logiciels installes",
            ["Page_InstallPilot"] = "InstallPilot",

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

            // BIOS update helper - no vendor exposes a "latest version" API, so this only links
            // to a search scoped to the detected manufacturer's site plus generic instructions.
            ["Bios_CheckUpdatesButton"] = "Verifier les mises a jour BIOS sur le site {0}",
            ["Bios_HowToUpdateTitle"] = "Comment mettre a jour :",
            ["Bios_Instructions_Asus"] = "1. Telechargez le fichier BIOS (.CAP) correspondant exactement a votre modele\n2. Copiez-le a la racine d'une cle USB formatee en FAT32\n3. Redemarrez et entrez dans le BIOS (touche Suppr ou F2 au demarrage)\n4. Ouvrez EZ Flash 3, selectionnez le fichier et lancez la mise a jour",
            ["Bios_Instructions_Msi"] = "1. Telechargez le fichier BIOS correspondant exactement a votre modele\n2. Copiez-le a la racine d'une cle USB formatee en FAT32\n3. Redemarrez et entrez dans le BIOS (touche Suppr)\n4. Ouvrez M-Flash, selectionnez le fichier et lancez la mise a jour",
            ["Bios_Instructions_Gigabyte"] = "1. Telechargez le fichier BIOS correspondant exactement a votre modele\n2. Copiez-le a la racine d'une cle USB formatee en FAT32\n3. Redemarrez et entrez dans le BIOS (touche Suppr)\n4. Ouvrez Q-Flash, selectionnez le fichier et lancez la mise a jour",
            ["Bios_Instructions_ASRock"] = "1. Telechargez le fichier BIOS correspondant exactement a votre modele\n2. Copiez-le a la racine d'une cle USB formatee en FAT32\n3. Redemarrez et entrez dans le BIOS (touche Suppr ou F2)\n4. Ouvrez Instant Flash, selectionnez le fichier et lancez la mise a jour",
            ["Bios_Instructions_Dell"] = "Utilisez de preference Dell Command | Update (ou Windows Update), qui detecte et installe la mise a jour BIOS automatiquement. Sinon, telechargez le fichier .EXE depuis le support Dell et executez-le directement sous Windows.",
            ["Bios_Instructions_Hp"] = "Utilisez de preference HP Support Assistant, qui detecte et installe la mise a jour BIOS automatiquement. Sinon, telechargez le fichier .EXE depuis le support HP et executez-le directement sous Windows.",
            ["Bios_Instructions_Lenovo"] = "Utilisez de preference Lenovo Vantage (ou Lenovo System Update), qui detecte et installe la mise a jour BIOS automatiquement. Sinon, telechargez l'utilitaire depuis le support Lenovo et suivez les instructions.",
            ["Bios_Instructions_Generic"] = "Recherchez votre modele exact de carte mere sur le site du fabricant, section Support / Pilotes et BIOS, telechargez la derniere version et suivez les instructions fournies (generalement via une cle USB ou un utilitaire Windows).",

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
        },

        [English] = new()
        {
            ["Sidebar_Section_General"] = "GENERAL",
            ["Nav_Overview"] = "Overview",
            ["Nav_Hardware"] = "Hardware",
            ["Nav_Monitoring"] = "Monitoring",
            ["Nav_Software"] = "Installed software",
            ["Nav_InstallPilot"] = "InstallPilot",
            ["Sidebar_CheckUpdates"] = "Check for updates",
            ["Sidebar_Preferences"] = "Preferences",

            ["Page_Overview"] = "Overview",
            ["Page_Hardware"] = "Hardware",
            ["Page_Monitoring"] = "Real-time monitoring",
            ["Page_Software"] = "Installed software",
            ["Page_InstallPilot"] = "InstallPilot",

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

            ["Bios_CheckUpdatesButton"] = "Check for BIOS updates on {0}'s site",
            ["Bios_HowToUpdateTitle"] = "How to update:",
            ["Bios_Instructions_Asus"] = "1. Download the BIOS file (.CAP) matching your exact model\n2. Copy it to the root of a FAT32-formatted USB drive\n3. Restart and enter the BIOS (Del or F2 at boot)\n4. Open EZ Flash 3, select the file, and start the update",
            ["Bios_Instructions_Msi"] = "1. Download the BIOS file matching your exact model\n2. Copy it to the root of a FAT32-formatted USB drive\n3. Restart and enter the BIOS (Del key)\n4. Open M-Flash, select the file, and start the update",
            ["Bios_Instructions_Gigabyte"] = "1. Download the BIOS file matching your exact model\n2. Copy it to the root of a FAT32-formatted USB drive\n3. Restart and enter the BIOS (Del key)\n4. Open Q-Flash, select the file, and start the update",
            ["Bios_Instructions_ASRock"] = "1. Download the BIOS file matching your exact model\n2. Copy it to the root of a FAT32-formatted USB drive\n3. Restart and enter the BIOS (Del or F2)\n4. Open Instant Flash, select the file, and start the update",
            ["Bios_Instructions_Dell"] = "Prefer Dell Command | Update (or Windows Update), which detects and installs the BIOS update automatically. Otherwise, download the .EXE from Dell support and run it directly in Windows.",
            ["Bios_Instructions_Hp"] = "Prefer HP Support Assistant, which detects and installs the BIOS update automatically. Otherwise, download the .EXE from HP support and run it directly in Windows.",
            ["Bios_Instructions_Lenovo"] = "Prefer Lenovo Vantage (or Lenovo System Update), which detects and installs the BIOS update automatically. Otherwise, download the utility from Lenovo support and follow the instructions.",
            ["Bios_Instructions_Generic"] = "Search for your exact motherboard model on the manufacturer's site, under Support / Drivers & BIOS, download the latest version and follow the provided instructions (usually via a USB drive or a Windows utility).",

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
        },

        [Russian] = new()
        {
            ["Sidebar_Section_General"] = "ОБЩЕЕ",
            ["Nav_Overview"] = "Обзор",
            ["Nav_Hardware"] = "Оборудование",
            ["Nav_Monitoring"] = "Мониторинг",
            ["Nav_Software"] = "Установленные программы",
            ["Nav_InstallPilot"] = "InstallPilot",
            ["Sidebar_CheckUpdates"] = "Проверить обновления",
            ["Sidebar_Preferences"] = "Настройки",

            ["Page_Overview"] = "Обзор",
            ["Page_Hardware"] = "Оборудование",
            ["Page_Monitoring"] = "Мониторинг в реальном времени",
            ["Page_Software"] = "Установленные программы",
            ["Page_InstallPilot"] = "InstallPilot",

            ["Update_Checking"] = "Проверка обновлений...",
            ["Update_OnlyInstalled"] = "Обновления доступны только для установленной версии (setup.exe).",
            ["Update_AlreadyLatest"] = "У вас уже установлена последняя версия.",
            ["Update_Downloading"] = "Загрузка версии {0}...",
            ["Update_DownloadingProgress"] = "Загрузка... {0}%",
            ["Update_Failed"] = "Ошибка обновления: {0}",

            ["Overview_Computer"] = "Компьютер",
            ["Overview_User"] = "Пользователь: ",
            ["Overview_OperatingSystem"] = "Операционная система",
            ["Overview_Version"] = "Версия ",
            ["Overview_Uptime"] = "Время работы",
            ["Overview_Processor"] = "Процессор",
            ["Overview_Memory"] = "Оперативная память",
            ["Overview_GraphicsCard"] = "Видеокарта",
            ["Overview_PrimaryDisk"] = "Основной диск",
            ["Overview_Motherboard"] = "Материнская плата",
            ["Overview_FreeOf"] = "{0:0.#} ГБ свободно / {1:0.#} ГБ",
            ["Overview_Uptime_Days"] = "{0} дн {1} ч {2} мин",
            ["Overview_Uptime_Hours"] = "{0} ч {1} мин",
            ["Overview_MemoryTotalPlain"] = "{0:0.#} ГБ",
            ["Overview_MemoryTotalWithType"] = "{0:0.#} ГБ ({1})",
            ["Overview_MemoryModules"] = "{0} модуль(ей)",

            ["Hardware_Refresh"] = "Обновить",
            ["Hardware_Processor"] = "Процессор",
            ["Hardware_Memory"] = "Оперативная память",
            ["Hardware_GraphicsCards"] = "Видеокарты",
            ["Hardware_Disks"] = "Физические диски",
            ["Hardware_Volumes"] = "Тома",
            ["Hardware_Network"] = "Сеть",
            ["Hardware_Motherboard"] = "Материнская плата / BIOS",
            ["Hardware_Bios"] = "BIOS ",
            ["Hardware_CoresThreads"] = "{0} ядер / {1} потоков @ {2:0.00} ГГц",
            ["Hardware_MemoryLine"] = "{0} модуль(ей) - {1} - {2} - {3} МГц",
            ["Hardware_VramFormat"] = "{0:0.#} ГБ VRAM",
            ["Hardware_DiskLine"] = "{0:0.#} ГБ - {1} - {2}",
            ["Hardware_VolumeFree"] = "{0:0.#} ГБ свободно из ",
            ["Copy_Tooltip"] = "Копировать",

            ["Bios_CheckUpdatesButton"] = "Проверить обновления BIOS на сайте {0}",
            ["Bios_HowToUpdateTitle"] = "Как обновить:",
            ["Bios_Instructions_Asus"] = "1. Скачайте файл BIOS (.CAP), точно соответствующий вашей модели\n2. Скопируйте его в корень USB-накопителя, отформатированного в FAT32\n3. Перезагрузитесь и войдите в BIOS (клавиша Del или F2 при загрузке)\n4. Откройте EZ Flash 3, выберите файл и запустите обновление",
            ["Bios_Instructions_Msi"] = "1. Скачайте файл BIOS, точно соответствующий вашей модели\n2. Скопируйте его в корень USB-накопителя, отформатированного в FAT32\n3. Перезагрузитесь и войдите в BIOS (клавиша Del)\n4. Откройте M-Flash, выберите файл и запустите обновление",
            ["Bios_Instructions_Gigabyte"] = "1. Скачайте файл BIOS, точно соответствующий вашей модели\n2. Скопируйте его в корень USB-накопителя, отформатированного в FAT32\n3. Перезагрузитесь и войдите в BIOS (клавиша Del)\n4. Откройте Q-Flash, выберите файл и запустите обновление",
            ["Bios_Instructions_ASRock"] = "1. Скачайте файл BIOS, точно соответствующий вашей модели\n2. Скопируйте его в корень USB-накопителя, отформатированного в FAT32\n3. Перезагрузитесь и войдите в BIOS (клавиша Del или F2)\n4. Откройте Instant Flash, выберите файл и запустите обновление",
            ["Bios_Instructions_Dell"] = "Предпочтительно используйте Dell Command | Update (или Windows Update) - они автоматически обнаруживают и устанавливают обновление BIOS. Иначе скачайте файл .EXE с сайта поддержки Dell и запустите его напрямую в Windows.",
            ["Bios_Instructions_Hp"] = "Предпочтительно используйте HP Support Assistant - он автоматически обнаруживает и устанавливает обновление BIOS. Иначе скачайте файл .EXE с сайта поддержки HP и запустите его напрямую в Windows.",
            ["Bios_Instructions_Lenovo"] = "Предпочтительно используйте Lenovo Vantage (или Lenovo System Update) - они автоматически обнаруживают и устанавливают обновление BIOS. Иначе скачайте утилиту с сайта поддержки Lenovo и следуйте инструкциям.",
            ["Bios_Instructions_Generic"] = "Найдите точную модель вашей материнской платы на сайте производителя, в разделе Поддержка / Драйверы и BIOS, скачайте последнюю версию и следуйте инструкциям (обычно через USB-накопитель или утилиту Windows).",

            ["Monitoring_Cpu"] = "ЦП",
            ["Monitoring_Ram"] = "ОЗУ",
            ["Monitoring_Disk"] = "Диск",
            ["Monitoring_Gpu"] = "ГП",
            ["Monitoring_DiskActivity"] = "Активность диска",
            ["Monitoring_UsedOfTotal"] = "{0:0.#} ГБ / {1:0.#} ГБ",
            ["Monitoring_TempNotAvailable"] = "Н/Д",
            ["Monitoring_TempTooltip"] = "Запустите SlapIA Tool от имени администратора, чтобы отобразить температуру процессора (нужен доступ к аппаратным датчикам).",
            ["Monitoring_EnableCpuTemp"] = "Включить",
            ["Monitoring_EnableCpuTempTooltip"] = "Разрешите доступ администратора (один раз за сеанс), чтобы считывать температуру процессора, не запуская всё приложение от имени администратора.",
            ["Monitoring_ChartCpu"] = "ЦП",
            ["Monitoring_ChartRam"] = "ОЗУ",
            ["Monitoring_ChartDisk"] = "Диск",
            ["Monitoring_ChartGpu"] = "ГП",

            ["Software_SearchPlaceholder"] = "Поиск программы...",
            ["Software_Count"] = "{0} / {1} программ",
            ["Software_ColumnName"] = "Название",
            ["Software_ColumnVersion"] = "Версия",
            ["Software_ColumnPublisher"] = "Издатель",
            ["Software_ColumnInstallDate"] = "Дата установки",

            ["Alert_CpuTempTitle"] = "Высокая температура ЦП",
            ["Alert_CpuTempBody"] = "Температура ЦП достигла {0:0} °C.",
            ["Alert_GpuTempTitle"] = "Высокая температура ГП",
            ["Alert_GpuTempBody"] = "Температура ГП достигла {0:0} °C.",
            ["Alert_CpuUsageTitle"] = "Высокая загрузка ЦП",
            ["Alert_CpuUsageBody"] = "Загрузка ЦП уже некоторое время держится на уровне {0:0} %.",
        },

        [Chinese] = new()
        {
            ["Sidebar_Section_General"] = "常规",
            ["Nav_Overview"] = "概览",
            ["Nav_Hardware"] = "硬件",
            ["Nav_Monitoring"] = "监控",
            ["Nav_Software"] = "已安装软件",
            ["Nav_InstallPilot"] = "InstallPilot",
            ["Sidebar_CheckUpdates"] = "检查更新",
            ["Sidebar_Preferences"] = "首选项",

            ["Page_Overview"] = "概览",
            ["Page_Hardware"] = "硬件",
            ["Page_Monitoring"] = "实时监控",
            ["Page_Software"] = "已安装软件",
            ["Page_InstallPilot"] = "InstallPilot",

            ["Update_Checking"] = "正在检查更新...",
            ["Update_OnlyInstalled"] = "更新仅适用于已安装的版本 (setup.exe)。",
            ["Update_AlreadyLatest"] = "您已是最新版本。",
            ["Update_Downloading"] = "正在下载版本 {0}...",
            ["Update_DownloadingProgress"] = "下载中... {0}%",
            ["Update_Failed"] = "更新失败：{0}",

            ["Overview_Computer"] = "计算机",
            ["Overview_User"] = "用户：",
            ["Overview_OperatingSystem"] = "操作系统",
            ["Overview_Version"] = "版本 ",
            ["Overview_Uptime"] = "运行时间",
            ["Overview_Processor"] = "处理器",
            ["Overview_Memory"] = "内存",
            ["Overview_GraphicsCard"] = "显卡",
            ["Overview_PrimaryDisk"] = "主磁盘",
            ["Overview_Motherboard"] = "主板",
            ["Overview_FreeOf"] = "可用 {0:0.#} GB / 共 {1:0.#} GB",
            ["Overview_Uptime_Days"] = "{0} 天 {1} 小时 {2} 分钟",
            ["Overview_Uptime_Hours"] = "{0} 小时 {1} 分钟",
            ["Overview_MemoryTotalPlain"] = "{0:0.#} GB",
            ["Overview_MemoryTotalWithType"] = "{0:0.#} GB ({1})",
            ["Overview_MemoryModules"] = "{0} 条内存",

            ["Hardware_Refresh"] = "刷新",
            ["Hardware_Processor"] = "处理器",
            ["Hardware_Memory"] = "内存",
            ["Hardware_GraphicsCards"] = "显卡",
            ["Hardware_Disks"] = "物理磁盘",
            ["Hardware_Volumes"] = "卷",
            ["Hardware_Network"] = "网络",
            ["Hardware_Motherboard"] = "主板 / BIOS",
            ["Hardware_Bios"] = "BIOS ",
            ["Hardware_CoresThreads"] = "{0} 核 / {1} 线程 @ {2:0.00} GHz",
            ["Hardware_MemoryLine"] = "{0} 条内存 - {1} - {2} - {3} MHz",
            ["Hardware_VramFormat"] = "{0:0.#} GB 显存",
            ["Hardware_DiskLine"] = "{0:0.#} GB - {1} - {2}",
            ["Hardware_VolumeFree"] = "可用 {0:0.#} GB，共 ",
            ["Copy_Tooltip"] = "复制",

            ["Bios_CheckUpdatesButton"] = "在{0}官网检查BIOS更新",
            ["Bios_HowToUpdateTitle"] = "更新方法：",
            ["Bios_Instructions_Asus"] = "1. 下载与您型号完全匹配的BIOS文件（.CAP）\n2. 将其复制到FAT32格式化的U盘根目录\n3. 重启并进入BIOS（开机时按Del或F2键）\n4. 打开EZ Flash 3，选择该文件并开始更新",
            ["Bios_Instructions_Msi"] = "1. 下载与您型号完全匹配的BIOS文件\n2. 将其复制到FAT32格式化的U盘根目录\n3. 重启并进入BIOS（按Del键）\n4. 打开M-Flash，选择该文件并开始更新",
            ["Bios_Instructions_Gigabyte"] = "1. 下载与您型号完全匹配的BIOS文件\n2. 将其复制到FAT32格式化的U盘根目录\n3. 重启并进入BIOS（按Del键）\n4. 打开Q-Flash，选择该文件并开始更新",
            ["Bios_Instructions_ASRock"] = "1. 下载与您型号完全匹配的BIOS文件\n2. 将其复制到FAT32格式化的U盘根目录\n3. 重启并进入BIOS（按Del或F2键）\n4. 打开Instant Flash，选择该文件并开始更新",
            ["Bios_Instructions_Dell"] = "建议使用 Dell Command | Update（或 Windows Update），它会自动检测并安装BIOS更新。否则，请从戴尔支持网站下载 .EXE 文件并直接在Windows中运行。",
            ["Bios_Instructions_Hp"] = "建议使用 HP Support Assistant，它会自动检测并安装BIOS更新。否则，请从惠普支持网站下载 .EXE 文件并直接在Windows中运行。",
            ["Bios_Instructions_Lenovo"] = "建议使用 Lenovo Vantage（或 Lenovo System Update），它们会自动检测并安装BIOS更新。否则，请从联想支持网站下载该工具并按照说明操作。",
            ["Bios_Instructions_Generic"] = "请在制造商网站的支持/驱动程序与BIOS部分查找您的确切主板型号，下载最新版本并按照提供的说明操作（通常通过U盘或Windows工具进行）。",

            ["Monitoring_Cpu"] = "CPU",
            ["Monitoring_Ram"] = "内存",
            ["Monitoring_Disk"] = "磁盘",
            ["Monitoring_Gpu"] = "GPU",
            ["Monitoring_DiskActivity"] = "磁盘活动",
            ["Monitoring_UsedOfTotal"] = "{0:0.#} GB / {1:0.#} GB",
            ["Monitoring_TempNotAvailable"] = "不可用",
            ["Monitoring_TempTooltip"] = "以管理员身份运行SlapIA Tool以显示处理器温度（需要访问硬件传感器）。",
            ["Monitoring_EnableCpuTemp"] = "启用",
            ["Monitoring_EnableCpuTempTooltip"] = "授予一次管理员权限（每次会话仅需一次）以读取处理器温度，而无需以管理员身份运行整个应用程序。",
            ["Monitoring_ChartCpu"] = "CPU",
            ["Monitoring_ChartRam"] = "内存",
            ["Monitoring_ChartDisk"] = "磁盘",
            ["Monitoring_ChartGpu"] = "GPU",

            ["Software_SearchPlaceholder"] = "搜索软件...",
            ["Software_Count"] = "{0} / {1} 个软件",
            ["Software_ColumnName"] = "名称",
            ["Software_ColumnVersion"] = "版本",
            ["Software_ColumnPublisher"] = "发布者",
            ["Software_ColumnInstallDate"] = "安装日期",

            ["Alert_CpuTempTitle"] = "CPU温度过高",
            ["Alert_CpuTempBody"] = "CPU温度已达到{0:0}°C。",
            ["Alert_GpuTempTitle"] = "GPU温度过高",
            ["Alert_GpuTempBody"] = "GPU温度已达到{0:0}°C。",
            ["Alert_CpuUsageTitle"] = "CPU占用率过高",
            ["Alert_CpuUsageBody"] = "CPU占用率已持续一段时间保持在{0:0}%。",
        },
    };
}
