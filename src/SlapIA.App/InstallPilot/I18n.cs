using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Text.Json;
using Microsoft.Win32;

namespace InstallPilot
{
    public static class I18n
    {
        public static string lang_code = "fr";
        public static string theme = "dark";
        public static string default_source = null;
        public static bool setup_completed = false;

        /// <summary>Raised whenever <see cref="lang_code"/> or <see cref="theme"/> changes via
        /// <see cref="SetLanguage"/>/<see cref="SetTheme"/> - lets any currently-visible
        /// InstallPilot view refresh even when it wasn't the one that made the change (e.g. the
        /// app-wide Preferences button, or SlapIA's own theme/language following the OS).</summary>
        public static event Action Changed;

        public static void SetLanguage(string code)
        {
            if (lang_code == code) return;
            lang_code = code;
            Changed?.Invoke();
        }

        public static void SetTheme(string newTheme)
        {
            if (theme == newTheme) return;
            theme = newTheme;
            if (System.Windows.Application.Current != null)
                ApplyTheme(System.Windows.Application.Current);
            Changed?.Invoke();
        }

        public static ImageSource LoadImage(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return null;

            // 1. Try to load from external file first (next to exe or parent)
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string checkPath1 = System.IO.Path.Combine(baseDir, relativePath);
                string checkPath2 = System.IO.Path.Combine(baseDir, "..", relativePath);
                
                string fullPath = null;
                if (File.Exists(checkPath1)) fullPath = checkPath1;
                else if (File.Exists(checkPath2)) fullPath = checkPath2;

                if (fullPath != null)
                {
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(fullPath);
                    bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    return bitmap;
                }
            }
            catch { }

            // 2. Fallback to embedded resource
            try
            {
                string resourcePath = relativePath.Replace('\\', '/');
                if (!resourcePath.StartsWith("/")) resourcePath = "/" + resourcePath;
                resourcePath = "/InstallPilot" + resourcePath;

                var uri = new Uri("pack://application:,,," + resourcePath, UriKind.Absolute);
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = uri;
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                return bitmap;
            }
            catch { }

            return null;
        }

        public static void SetWindowIcon(Window window)
        {
            try
            {
                var iconSource = LoadImage("icons/logo.png");
                if (iconSource == null) iconSource = LoadImage("icons/logo.ico");
                if (iconSource != null)
                {
                    window.Icon = iconSource;
                }
            }
            catch { }
        }

        public static Dictionary<string, string> T = new Dictionary<string, string>();

        public static readonly Dictionary<string, string> LanguageNames = new Dictionary<string, string>
        {
            { "fr", "Français" },
            { "en", "English" },
            { "ru", "Русский" },
            { "zh", "中文" }
        };

        public static readonly Dictionary<string, string> LanguageLabels = new Dictionary<string, string>
        {
            { "Français", "fr" },
            { "English", "en" },
            { "Русский", "ru" },
            { "中文", "zh" }
        };

        public static readonly Dictionary<string, Dictionary<string, string>> CategoryLabels = new Dictionary<string, Dictionary<string, string>>
        {
            { "web", new Dictionary<string, string> { { "fr", "Navigateurs Web" }, { "en", "Web Browsers" }, { "ru", "Веб-браузеры" }, { "zh", "网页浏览器" } } },
            { "messaging", new Dictionary<string, string> { { "fr", "Messagerie" }, { "en", "Messaging" }, { "ru", "Сообщения" }, { "zh", "消息" } } },
            { "games", new Dictionary<string, string> { { "fr", "Jeux" }, { "en", "Games" }, { "ru", "Игры" }, { "zh", "游戏" } } },
            { "media", new Dictionary<string, string> { { "fr", "Multimédia" }, { "en", "Media" }, { "ru", "Медиа" }, { "zh", "媒体" } } },
            { "productivity", new Dictionary<string, string> { { "fr", "Productivité" }, { "en", "Productivity" }, { "ru", "Продуктивность" }, { "zh", "效率" } } },
            { "security", new Dictionary<string, string> { { "fr", "Sécurité" }, { "en", "Security" }, { "ru", "Безопасность" }, { "zh", "安全" } } },
            { "utilities", new Dictionary<string, string> { { "fr", "Utilitaires" }, { "en", "Utilities" }, { "ru", "Утилиты" }, { "zh", "实用工具" } } },
            { "dev_tools", new Dictionary<string, string> { { "fr", "Outils Dev" }, { "en", "Developer Tools" }, { "ru", "Инструменты разработчика" }, { "zh", "开发工具" } } },
            { "other", new Dictionary<string, string> { { "fr", "Autres" }, { "en", "Other" }, { "ru", "Другое" }, { "zh", "其他" } } }
        };

        private static readonly Dictionary<string, Dictionary<string, string>> Languages = new Dictionary<string, Dictionary<string, string>>
        {
            {
                "fr", new Dictionary<string, string>
                {
                    { "title", "InstallPilot" },
                    { "step1", "Choisissez les applications" },
                    { "step2", "2. Téléchargez et installez" },
                    { "language", "Langue" },
                    { "check_now", "Actualiser" },
                    { "install_selected", "Obtenir votre sélection" },
                    { "status_checking", "Vérification en cours..." },
                    { "scan_popup_title", "Analyse des applications" },
                    { "scan_progress", "Analyse {n}/{total} : {name}" },
                    { "select_app", "Sélectionnez au moins une application." },
                    { "no_url", "Aucune URL disponible." },
                    { "log_open_store", "Ouverture du Microsoft Store pour {name}..." },
                    { "log_open_web", "Ouverture du site officiel pour {name}..." },
                    { "log_done", "Terminé." },
                    { "install_instructions", "Sélectionnez des applications puis cliquez sur « Obtenir votre sélection »." },
                    { "config_note", "Pour ajouter une application, éditez app_config.json." },
                    { "error_loading_config", "Impossible de charger la configuration." },
                    { "select_all", "tout" },
                    { "select_none", "aucun" },
                    { "n_selected", "{n} sélectionnée(s)" },
                    { "installed_label", "Installé" },
                    { "ask_source_msg", "Comment installer {name} ?" },
                    { "btn_store", "Microsoft Store" },
                    { "btn_official", "Site officiel" },
                    { "downloading", "Téléchargement de {name}..." },
                    { "download_done", "Lancement de l'installateur pour {name}..." },
                    { "download_error", "Erreur de téléchargement pour {name} : {err}" },
                    { "winget_installing", "Installation de {name} via winget..." },
                    { "winget_success", "✓ {name} installé avec succès." },
                    { "winget_error", "Échec winget pour {name} (code {code})." },
                    { "winget_unavailable", "winget non disponible — ouverture du téléchargeur." },
                    { "inst_title", "Installation — {n} application(s)" },
                    { "inst_waiting", "En attente" },
                    { "inst_downloading", "Téléchargement {pct}%" },
                    { "inst_installing", "Installation..." },
                    { "inst_installing_popup", "Installation... (une fenêtre peut s'ouvrir)" },
                    { "inst_ok", "Installé" },
                    { "inst_err", "Erreur" },
                    { "inst_cancel", "Annuler" },
                    { "inst_close", "Fermer" },
                    { "inst_summary", "{ok} / {total} installé(s)" },
                    { "inst_save_script", "Sauvegarder le script (.bat)" },
                    { "inst_script_saved", "Script créé :\n{path}" },
                    { "inst_open_store", "Ouverture du Store..." },
                    { "inst_no_url", "Aucune source disponible" },
                    { "src_store", "Store" },
                    { "src_exe", "EXE" },
                    { "nav_all", "Toutes les apps" },
                    { "nav_only_store", "Uniquement Store" },
                    { "nav_only_exe", "Uniquement EXE" },
                    { "search_ph", "Rechercher..." },
                    { "dark_mode", "Mode sombre" },
                    { "light_mode", "Mode clair" },
                    { "update_all", "Tout Mettre à Jour" },
                    { "pref_source", "Source préf." },
                    { "preferences", "Préférences" },
                    { "no_nvidia_gpu", "Pas de GPU NVIDIA" },
                    { "wiz_welcome_title", "Bienvenue sur InstallPilot" },
                    { "wiz_welcome_desc", "Réinstallez tous vos logiciels essentiels en quelques clics." },
                    { "wiz_welcome_f1", "• Détecte automatiquement les applications déjà présentes." },
                    { "wiz_welcome_f2", "• Télécharge proprement depuis le Store ou les sites officiels." },
                    { "wiz_welcome_f3", "• Installe tout silencieusement en arrière-plan sans publicité." },
                    { "wiz_btn_start", "Commencer" },
                    { "wiz_pref_title", "Personnalisez vos préférences" },
                    { "wiz_pref_lang", "Langue de l'application" },
                    { "wiz_pref_theme", "Thème graphique" },
                    { "wiz_pref_source", "Source préférée par défaut" },
                    { "wiz_source_store", "Microsoft Store" },
                    { "wiz_source_exe", "Site officiel (EXE)" },
                    { "wiz_source_none", "Au choix" },
                    { "wiz_btn_finish", "Terminer" },
                    { "wiz_btn_back", "Retour" },
                    { "err_select_source", "Veuillez choisir la source (Store / EXE) pour : {name}" },
                    { "err_uac_denied", "L'autorisation d'administrateur a été refusée ou a échoué." },
                    { "sc_title", "InstallPilot - Installation en cours..." },
                    { "sc_warning", "Ne fermez pas cette fenetre. Elle se fermera automatiquement a la fin." },
                    { "sc_install_store", "Installation de {name} depuis le Microsoft Store..." },
                    { "sc_install_winget", "Installation de {name} via Winget..." },
                    { "sc_downloading", "Telechargement de {name}..." },
                    { "sc_installing", "Installation de {name}..." },
                    { "sc_no_link", "Impossible d'installer {name} : aucun lien direct trouve." },
                    { "sc_done", "Toutes les installations sont terminees !" },
                    { "sc_cleanup", "Nettoyage..." },
                    { "sc_update_all", "InstallPilot - Mise a jour de TOUTES vos applications en cours..." },
                    { "sc_update_warning", "Winget va scanner votre PC et telecharger les dernieres versions." },
                    { "sc_update_sel", "InstallPilot - Mise a jour de la selection en cours..." },
                    { "sc_update_store", "Telechargement de l'installateur natif Store pour {name}..." },
                    { "sc_update_store_warn", "Veuillez finaliser la mise a jour de {name} dans la fenetre qui vient de s'ouvrir." },
                    { "sc_update_winget", "Mise a jour de {name} via Winget..." },
                    { "sc_update_done", "Mises a jour terminees !" },
                    { "sc_fallback_mode", "InstallPilot - Mode de secours Batch (PowerShell non disponible/bloque)" },
                    { "inst_retry", "Réessayer" },
                    { "inst_manual", "Manuel" },
                    { "upd_title", "Mises à jour" },
                    { "upd_update_sel", "Mettre à jour la sélection" },
                    { "upd_searching", "Recherche des mises à jour disponibles..." },
                    { "upd_in_background", "Mise à jour en arrière-plan..." },
                    { "upd_all_done", "Mises à jour terminées avec succès !" },
                    { "upd_all_up_to_date", "Toutes vos applications sont à jour !" },
                    { "inst_checking_env", "Vérification de l'environnement..." },
                    { "inst_gen_script", "Génération du script d'installation..." },
                    { "inst_background", "Installation de la sélection en arrière-plan..." },
                    { "inst_wait_store", "En attente (Microsoft Store)" },
                    { "inst_wait_official", "En attente (Site Officiel)" },
                    { "inst_all_done", "Toutes les installations sont terminées !" },
                    { "inst_downloading_x", "Téléchargement..." },
                    { "inst_installing_x", "Installation..." },
                    { "inst_done", "Terminé" },
                    { "inst_retry_attempt", "Nouvelle tentative..." },
                    { "inst_unknown_error", "Erreur inconnue" },
                    { "inst_error_prefix", "Erreur : {msg}" },
                    { "inst_error_code", "Erreur" },
                    { "err_generic", "Erreur" }
                }
            },
            {
                "en", new Dictionary<string, string>
                {
                    { "title", "InstallPilot" },
                    { "step1", "Pick the apps you want" },
                    { "step2", "2. Download and install" },
                    { "language", "Language" },
                    { "check_now", "Refresh" },
                    { "install_selected", "Get your selection" },
                    { "status_checking", "Checking..." },
                    { "scan_popup_title", "Scanning applications" },
                    { "scan_progress", "Scanning {n}/{total}: {name}" },
                    { "select_app", "Select at least one application." },
                    { "no_url", "No URL available." },
                    { "log_open_store", "Opening Microsoft Store for {name}..." },
                    { "log_open_web", "Opening official website for {name}..." },
                    { "log_done", "Done." },
                    { "install_instructions", "Select apps then click 'Get your selection'." },
                    { "config_note", "To add an app, edit app_config.json." },
                    { "error_loading_config", "Unable to load configuration." },
                    { "select_all", "all" },
                    { "select_none", "none" },
                    { "n_selected", "{n} selected" },
                    { "installed_label", "Installed" },
                    { "ask_source_msg", "How to install {name}?" },
                    { "btn_store", "Microsoft Store" },
                    { "btn_official", "Official website" },
                    { "downloading", "Downloading {name}..." },
                    { "download_done", "Launching installer for {name}..." },
                    { "download_error", "Download error for {name}: {err}" },
                    { "winget_installing", "Installing {name} via winget..." },
                    { "winget_success", "✓ {name} installed successfully." },
                    { "winget_error", "Winget failed for {name} (code {code})." },
                    { "winget_unavailable", "winget not available — opening downloader." },
                    { "inst_title", "Installing — {n} app(s)" },
                    { "inst_waiting", "Waiting" },
                    { "inst_downloading", "Downloading {pct}%" },
                    { "inst_installing", "Installing..." },
                    { "inst_installing_popup", "Installing... (a window may open)" },
                    { "inst_ok", "Installed" },
                    { "inst_err", "Error" },
                    { "inst_cancel", "Cancel" },
                    { "inst_close", "Close" },
                    { "inst_summary", "{ok} / {total} installed" },
                    { "inst_save_script", "Save script (.bat)" },
                    { "inst_script_saved", "Script saved:\n{path}" },
                    { "inst_open_store", "Opening Store..." },
                    { "inst_no_url", "No source available" },
                    { "src_store", "Store" },
                    { "src_exe", "EXE" },
                    { "nav_all", "All apps" },
                    { "nav_only_store", "Store Only" },
                    { "nav_only_exe", "EXE Only" },
                    { "search_ph", "Search..." },
                    { "dark_mode", "Dark mode" },
                    { "light_mode", "Light mode" },
                    { "update_all", "Update All" },
                    { "pref_source", "Pref. source" },
                    { "preferences", "Preferences" },
                    { "no_nvidia_gpu", "No NVIDIA GPU" },
                    { "wiz_welcome_title", "Welcome to InstallPilot" },
                    { "wiz_welcome_desc", "Reinstall all your essential software in just a few clicks." },
                    { "wiz_welcome_f1", "• Automatically detects apps already installed." },
                    { "wiz_welcome_f2", "• Downloads cleanly from the Store or official sites." },
                    { "wiz_welcome_f3", "• Installs everything silently in the background." },
                    { "wiz_btn_start", "Get Started" },
                    { "wiz_pref_title", "Customize your preferences" },
                    { "wiz_pref_lang", "App Language" },
                    { "wiz_pref_theme", "Graphical theme" },
                    { "wiz_pref_source", "Preferred default source" },
                    { "wiz_source_store", "Microsoft Store" },
                    { "wiz_source_exe", "Official site (EXE)" },
                    { "wiz_source_none", "Choose manually" },
                    { "wiz_btn_finish", "Finish" },
                    { "wiz_btn_back", "Back" },
                    { "err_select_source", "Please choose the source (Store / EXE) for: {name}" },
                    { "err_uac_denied", "Administrator authorization was denied or failed." },
                    { "sc_title", "InstallPilot - Installation in progress..." },
                    { "sc_warning", "Do not close this window. It will close automatically when finished." },
                    { "sc_install_store", "Installing {name} from Microsoft Store..." },
                    { "sc_install_winget", "Installing {name} via Winget..." },
                    { "sc_downloading", "Downloading {name}..." },
                    { "sc_installing", "Installing {name}..." },
                    { "sc_no_link", "Cannot install {name}: no direct link found." },
                    { "sc_done", "All installations completed!" },
                    { "sc_cleanup", "Cleaning up..." },
                    { "sc_update_all", "InstallPilot - Updating ALL your applications in progress..." },
                    { "sc_update_warning", "Winget will scan your PC and download the latest versions." },
                    { "sc_update_sel", "InstallPilot - Updating selection in progress..." },
                    { "sc_update_store", "Downloading native Store installer for {name}..." },
                    { "sc_update_store_warn", "Please finalize the update of {name} in the window that just opened." },
                    { "sc_update_winget", "Updating {name} via Winget..." },
                    { "sc_update_done", "Updates completed!" },
                    { "sc_fallback_mode", "InstallPilot - Batch fallback mode (PowerShell not available/blocked)" },
                    { "inst_retry", "Retry" },
                    { "inst_manual", "Manual" },
                    { "upd_title", "Updates" },
                    { "upd_update_sel", "Update selection" },
                    { "upd_searching", "Searching for available updates..." },
                    { "upd_in_background", "Updating in background..." },
                    { "upd_all_done", "Updates completed successfully!" },
                    { "upd_all_up_to_date", "All your apps are up to date!" },
                    { "inst_checking_env", "Checking environment..." },
                    { "inst_gen_script", "Generating install script..." },
                    { "inst_background", "Installing selection in background..." },
                    { "inst_wait_store", "Waiting (Microsoft Store)" },
                    { "inst_wait_official", "Waiting (Official Site)" },
                    { "inst_all_done", "All installations completed!" },
                    { "inst_downloading_x", "Downloading..." },
                    { "inst_installing_x", "Installing..." },
                    { "inst_done", "Done" },
                    { "inst_retry_attempt", "Retrying..." },
                    { "inst_unknown_error", "Unknown error" },
                    { "inst_error_prefix", "Error: {msg}" },
                    { "inst_error_code", "Error" },
                    { "err_generic", "Error" }
                }
            },
            {
                "ru", new Dictionary<string, string>
                {
                    { "title", "InstallPilot" },
                    { "step1", "Выберите приложения" },
                    { "step2", "2. Загрузите и установите" },
                    { "language", "Язык" },
                    { "check_now", "Обновить" },
                    { "install_selected", "Получить выбор" },
                    { "status_checking", "Проверка..." },
                    { "scan_popup_title", "Сканирование приложений" },
                    { "scan_progress", "Сканирование {n}/{total}: {name}" },
                    { "select_app", "Выберите хотя бы одно приложение." },
                    { "no_url", "Нет доступной ссылки." },
                    { "log_open_store", "Открытие Microsoft Store для {name}..." },
                    { "log_open_web", "Открытие официального сайта для {name}..." },
                    { "log_done", "Готово." },
                    { "install_instructions", "Выберите приложения, затем нажмите «Получить выбор»." },
                    { "config_note", "Чтобы добавить приложение, отредактируйте app_config.json." },
                    { "error_loading_config", "Не удалось загрузить конфигурацию." },
                    { "select_all", "все" },
                    { "select_none", "ни одно" },
                    { "n_selected", "{n} выбрано" },
                    { "installed_label", "Установлено" },
                    { "ask_source_msg", "Как установить {name}?" },
                    { "btn_store", "Microsoft Store" },
                    { "btn_official", "Официальный сайт" },
                    { "downloading", "Загрузка {name}..." },
                    { "download_done", "Запуск установщика для {name}..." },
                    { "download_error", "Ошибка загрузки для {name}: {err}" },
                    { "winget_installing", "Установка {name} через winget..." },
                    { "winget_success", "✓ {name} успешно установлен." },
                    { "winget_error", "winget не удалось для {name} (код {code})." },
                    { "winget_unavailable", "winget недоступен — открывается загрузчик." },
                    { "inst_title", "Установка — {n} приложений" },
                    { "inst_waiting", "Ожидание" },
                    { "inst_downloading", "Загрузка {pct}%" },
                    { "inst_installing", "Установка..." },
                    { "inst_installing_popup", "Установка... (окно может открыться)" },
                    { "inst_ok", "Установлено" },
                    { "inst_err", "Ошибка" },
                    { "inst_cancel", "Отмена" },
                    { "inst_close", "Закрыть" },
                    { "inst_summary", "{ok} / {total} установлено" },
                    { "inst_save_script", "Сохранить скрипт (.bat)" },
                    { "inst_script_saved", "Скрипт сохранен:\n{path}" },
                    { "inst_open_store", "Открытие Store..." },
                    { "inst_no_url", "Нет доступного источника" },
                    { "src_store", "Store" },
                    { "src_exe", "EXE" },
                    { "nav_all", "Все приложения" },
                    { "nav_only_store", "Только Store" },
                    { "nav_only_exe", "Только EXE" },
                    { "search_ph", "Поиск..." },
                    { "dark_mode", "Темная тема" },
                    { "light_mode", "Светлая тема" },
                    { "update_all", "Обновить всё" },
                    { "pref_source", "Предпочт. источник" },
                    { "preferences", "Настройки" },
                    { "no_nvidia_gpu", "Нет GPU NVIDIA" },
                    { "wiz_welcome_title", "Добро пожаловать в InstallPilot" },
                    { "wiz_welcome_desc", "Переустановите всё необходимое ПО в несколько кликов." },
                    { "wiz_welcome_f1", "• Автоматически обнаруживает уже установленные приложения." },
                    { "wiz_welcome_f2", "• Загружает из Store или с официальных сайтов." },
                    { "wiz_welcome_f3", "• Устанавливает всё тихо в фоне." },
                    { "wiz_btn_start", "Начать" },
                    { "wiz_pref_title", "Настройте параметры" },
                    { "wiz_pref_lang", "Язык приложения" },
                    { "wiz_pref_theme", "Графическая тема" },
                    { "wiz_pref_source", "Предпочтительный источник по умолчанию" },
                    { "wiz_source_store", "Microsoft Store" },
                    { "wiz_source_exe", "Официальный сайт (EXE)" },
                    { "wiz_source_none", "Выбрать вручную" },
                    { "wiz_btn_finish", "Готово" },
                    { "wiz_btn_back", "Назад" },
                    { "err_select_source", "Пожалуйста, выберите источник (Store / EXE) для: {name}" },
                    { "err_uac_denied", "В правах администратора было отказано или произошел сбой." },
                    { "sc_title", "InstallPilot - Выполняется установка..." },
                    { "sc_warning", "Не закрывайте это окно. Оно закроется автоматически по завершении." },
                    { "sc_install_store", "Установка {name} из Microsoft Store..." },
                    { "sc_install_winget", "Установка {name} через Winget..." },
                    { "sc_downloading", "Загрузка {name}..." },
                    { "sc_installing", "Установка {name}..." },
                    { "sc_no_link", "Не удалось установить {name}: прямая ссылка не найдена." },
                    { "sc_done", "Все установки завершены!" },
                    { "sc_cleanup", "Очистка..." },
                    { "sc_update_all", "InstallPilot - Выполняется обновление ВСЕХ ваших приложений..." },
                    { "sc_update_warning", "Winget просканирует ваш ПК и загрузит последние версии." },
                    { "sc_update_sel", "InstallPilot - Выполняется обновление выбранных приложений..." },
                    { "sc_update_store", "Загрузка нативного установщика Store для {name}..." },
                    { "sc_update_store_warn", "Пожалуйста, завершите обновление {name} в открывшемся окне." },
                    { "sc_update_winget", "Обновление {name} через Winget..." },
                    { "sc_update_done", "Обновления завершены!" },
                    { "sc_fallback_mode", "InstallPilot - Резервный пакетный режим (PowerShell недоступен или заблокирован)" },
                    { "inst_retry", "Повторить" },
                    { "inst_manual", "Вручную" },
                    { "upd_title", "Обновления" },
                    { "upd_update_sel", "Обновить выбранное" },
                    { "upd_searching", "Поиск доступных обновлений..." },
                    { "upd_in_background", "Обновление в фоне..." },
                    { "upd_all_done", "Обновления успешно завершены !" },
                    { "upd_all_up_to_date", "Все ваши приложения обновлены!" },
                    { "inst_checking_env", "Проверка среды..." },
                    { "inst_gen_script", "Создание скрипта установки..." },
                    { "inst_background", "Установка в фоновом режиме..." },
                    { "inst_wait_store", "Ожидание (Microsoft Store)" },
                    { "inst_wait_official", "Ожидание (Официальный сайт)" },
                    { "inst_all_done", "Все установки завершены!" },
                    { "inst_downloading_x", "Загрузка..." },
                    { "inst_installing_x", "Установка..." },
                    { "inst_done", "Завершено" },
                    { "inst_retry_attempt", "Повторная попытка..." },
                    { "inst_unknown_error", "Неизвестная ошибка" },
                    { "inst_error_prefix", "Ошибка: {msg}" },
                    { "inst_error_code", "Ошибка" },
                    { "err_generic", "Ошибка" }
                }
            },
            {
                "zh", new Dictionary<string, string>
                {
                    { "title", "InstallPilot" },
                    { "step1", "选择您想要的应用" },
                    { "step2", "2. 下载并安装" },
                    { "language", "语言" },
                    { "check_now", "刷新" },
                    { "install_selected", "获取所选项" },
                    { "status_checking", "检查中..." },
                    { "scan_popup_title", "正在扫描应用" },
                    { "scan_progress", "扫描 {n}/{total}：{name}" },
                    { "select_app", "请选择至少一个应用。" },
                    { "no_url", "没有可用的链接。" },
                    { "log_open_store", "正在为{name}打开 Microsoft Store..." },
                    { "log_open_web", "正在为{name}打开官方网站..." },
                    { "log_done", "完成。" },
                    { "install_instructions", "选择应用后点击“获取所选项”。" },
                    { "config_note", "要添加应用，请编辑 app_config.json。" },
                    { "error_loading_config", "无法加载配置。" },
                    { "select_all", "全部" },
                    { "select_none", "无" },
                    { "n_selected", "已选择 {n}" },
                    { "installed_label", "已安装" },
                    { "ask_source_msg", "如何安装 {name}？" },
                    { "btn_store", "Microsoft Store" },
                    { "btn_official", "官方网站" },
                    { "downloading", "正在下载 {name}..." },
                    { "download_done", "正在启动 {name} 的安装程序..." },
                    { "download_error", "{name} 下载错误：{err}" },
                    { "winget_installing", "正在通过 winget 安装 {name}..." },
                    { "winget_success", "✓ {name} 安装成功。" },
                    { "winget_error", "winget 安装 {name} 失败 (代码 {code})。" },
                    { "winget_unavailable", "winget 不可用 — 正在打开下载程序。" },
                    { "inst_title", "安装 — {n} 个应用" },
                    { "inst_waiting", "等待" },
                    { "inst_downloading", "下载 {pct}%" },
                    { "inst_installing", "安装中..." },
                    { "inst_installing_popup", "安装中...（可能会打开一个窗口）" },
                    { "inst_ok", "已安装" },
                    { "inst_err", "错误" },
                    { "inst_cancel", "取消" },
                    { "inst_close", "关闭" },
                    { "inst_summary", "{ok} / {total} 已安装" },
                    { "inst_save_script", "保存脚本 (.bat)" },
                    { "inst_script_saved", "脚本已保存：\n{path}" },
                    { "inst_open_store", "打开 Store..." },
                    { "inst_no_url", "没有可用来源" },
                    { "src_store", "Store" },
                    { "src_exe", "EXE" },
                    { "nav_all", "所有应用" },
                    { "nav_only_store", "仅 Store" },
                    { "nav_only_exe", "仅 EXE" },
                    { "search_ph", "搜索..." },
                    { "dark_mode", "深色模式" },
                    { "light_mode", "浅色模式" },
                    { "update_all", "全部更新" },
                    { "pref_source", "首选来源" },
                    { "preferences", "首选项" },
                    { "no_nvidia_gpu", "没有 NVIDIA GPU" },
                    { "wiz_welcome_title", "欢迎使用 InstallPilot" },
                    { "wiz_welcome_desc", "只需几次点击即可重新安装所有必备软件。" },
                    { "wiz_welcome_f1", "• 自动检测已安装的应用。" },
                    { "wiz_welcome_f2", "• 从 Store 或官方网站干净下载。" },
                    { "wiz_welcome_f3", "• 在后台静默安装所有程序。" },
                    { "wiz_btn_start", "开始" },
                    { "wiz_pref_title", "自定义您的偏好设置" },
                    { "wiz_pref_lang", "应用语言" },
                    { "wiz_pref_theme", "界面主题" },
                    { "wiz_pref_source", "默认首选来源" },
                    { "wiz_source_store", "Microsoft Store" },
                    { "wiz_source_exe", "官方网站 (EXE)" },
                    { "wiz_source_none", "手动选择" },
                    { "wiz_btn_finish", "完成" },
                    { "wiz_btn_back", "返回" },
                    { "err_select_source", "请选择 {name} 的安装来源（Store / EXE）" },
                    { "err_uac_denied", "管理员权限已被拒绝或失败。" },
                    { "sc_title", "InstallPilot - 正在安装..." },
                    { "sc_warning", "请勿关闭此窗口。安装完成后它将自动关闭。" },
                    { "sc_install_store", "正在从 Microsoft Store 安装 {name}..." },
                    { "sc_install_winget", "正在通过 Winget 安装 {name}..." },
                    { "sc_downloading", "正在下载 {name}..." },
                    { "sc_installing", "正在安装 {name}..." },
                    { "sc_no_link", "无法安装 {name}：未找到直接下载链接。" },
                    { "sc_done", "所有安装已完成！" },
                    { "sc_cleanup", "正在清理..." },
                    { "sc_update_all", "InstallPilot - 正在更新您的所有应用..." },
                    { "sc_update_warning", "Winget 将扫描您的电脑并下载最新版本。" },
                    { "sc_update_sel", "InstallPilot - 正在更新所选应用..." },
                    { "sc_update_store", "正在为 {name} 下载官方 Store 安装程序..." },
                    { "sc_update_store_warn", "请在刚打开的窗口中完成 {name} 的更新。" },
                    { "sc_update_winget", "正在通过 Winget 更新 {name}..." },
                    { "sc_update_done", "更新已完成！" },
                    { "sc_fallback_mode", "InstallPilot - 批处理备用模式（PowerShell 不可用/被阻止）" },
                    { "inst_retry", "重试" },
                    { "inst_manual", "手动" },
                    { "upd_title", "更新" },
                    { "upd_update_sel", "更新所选" },
                    { "upd_searching", "正在搜索可用更新..." },
                    { "upd_in_background", "正在后台更新..." },
                    { "upd_all_done", "更新已成功完成！" },
                    { "upd_all_up_to_date", "您的所有应用均已是最新版本！" },
                    { "inst_checking_env", "正在检查环境..." },
                    { "inst_gen_script", "正在生成安装脚本..." },
                    { "inst_background", "正在后台安装所选应用..." },
                    { "inst_wait_store", "等待中（Microsoft Store）" },
                    { "inst_wait_official", "等待中（官方网站）" },
                    { "inst_all_done", "所有安装已完成！" },
                    { "inst_downloading_x", "下载中..." },
                    { "inst_installing_x", "安装中..." },
                    { "inst_done", "完成" },
                    { "inst_retry_attempt", "正在重试..." },
                    { "inst_unknown_error", "未知错误" },
                    { "inst_error_prefix", "错误：{msg}" },
                    { "inst_error_code", "错误" },
                    { "err_generic", "错误" }
                }
            }
        };

        public static readonly Dictionary<string, string> DarkColors = new Dictionary<string, string>
        {
            { "bg", "#202020" },
            { "sidebar", "#1a1a1a" },
            { "surface", "#2b2b2b" },
            { "surface2", "#252525" },
            { "border", "#3a3a3a" },
            { "fg", "#f3f3f3" },
            { "fg2", "#ababab" },
            { "fg3", "#606060" },
            { "accent", "#0078d4" },
            { "accent_hv", "#1a8ae0" },
            { "accent_dis", "#0f3d6b" },
            { "accent_fg", "#ffffff" },
            { "btn_sec", "#333333" },
            { "btn_sec_fg", "#f3f3f3" },
            { "btn_sec_hv", "#404040" },
            { "hover", "#2e2e2e" },
            { "sidebar_hover", "#252525" },
            { "sidebar_active", "#2d2d2d" },
            { "error", "#f28b82" },
            { "tog_on", "#0078d4" },
            { "tog_off", "#555555" },
            { "scrollbar", "#404040" },
            { "installed", "#57c94a" },
            { "nav_bar", "#0078d4" }
        };

        public static readonly Dictionary<string, string> LightColors = new Dictionary<string, string>
        {
            { "bg", "#f3f3f3" },
            { "sidebar", "#ebebeb" },
            { "surface", "#ffffff" },
            { "surface2", "#f0f0f0" },
            { "border", "#d6d6d6" },
            { "fg", "#1a1a1a" },
            { "fg2", "#5a5a5a" },
            { "fg3", "#aaaaaa" },
            { "accent", "#0078d4" },
            { "accent_hv", "#006cbe" },
            { "accent_dis", "#c5e0f9" },
            { "accent_fg", "#ffffff" },
            { "btn_sec", "#e5e5e5" },
            { "btn_sec_fg", "#1a1a1a" },
            { "btn_sec_hv", "#d5d5d5" },
            { "hover", "#e8e8e8" },
            { "sidebar_hover", "#e4e4e4" },
            { "sidebar_active", "#dcdcdc" },
            { "error", "#c42b1c" },
            { "tog_on", "#0078d4" },
            { "tog_off", "#9e9e9e" },
            { "scrollbar", "#c0c0c0" },
            { "installed", "#2e7d32" },
            { "nav_bar", "#0078d4" }
        };

        static I18n()
        {
            LoadSettings();
            UpdateT();
        }

        private static string GetSettingsFilePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string dir = Path.Combine(appData, "InstallPilot");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "settings.json");
        }

        public static void LoadSettings()
        {
            try
            {
                string path = GetSettingsFilePath();
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                    if (dict != null)
                    {
                        if (dict.TryGetValue("lang", out var langEl) && langEl.ValueKind == JsonValueKind.String) lang_code = langEl.GetString() ?? "fr";
                        if (dict.TryGetValue("theme", out var themeEl) && themeEl.ValueKind == JsonValueKind.String) theme = themeEl.GetString() ?? "dark";
                        if (dict.TryGetValue("default_source", out var srcEl)) default_source = srcEl.ValueKind == JsonValueKind.String ? srcEl.GetString() : null;
                        if (dict.TryGetValue("setup_completed", out var setupEl) && (setupEl.ValueKind == JsonValueKind.True || setupEl.ValueKind == JsonValueKind.False)) setup_completed = setupEl.GetBoolean();
                    }
                }
                else
                {
                    theme = DetectWindowsTheme();
                }
            }
            catch
            {
                theme = DetectWindowsTheme();
            }
        }

        public static void SaveSettings()
        {
            try
            {
                string path = GetSettingsFilePath();
                var dict = new Dictionary<string, object?>
                {
                    { "lang", lang_code },
                    { "theme", theme },
                    { "default_source", default_source },
                    { "setup_completed", setup_completed }
                };
                string json = JsonSerializer.Serialize(dict);
                File.WriteAllText(path, json);
            }
            catch { }
        }

        public static string tr(string key, params object[] args)
        {
            var dict = Languages.ContainsKey(lang_code) ? Languages[lang_code] : Languages["en"];
            string raw = dict.ContainsKey(key) ? dict[key] : key;

            if (args != null && args.Length > 0)
            {
                Dictionary<string, object> placeholders = args[0] as Dictionary<string, object>;
                if (placeholders != null)
                {
                    foreach (var pair in placeholders)
                        raw = raw.Replace("{" + pair.Key + "}", pair.Value != null ? pair.Value.ToString() : "");
                }
                else if (args.Length >= 2 && args.Length % 2 == 0)
                {
                    // Handle alternating key/value pairs: tr(key, "n", 1, "total", 5, "name", "Firefox")
                    for (int i = 0; i + 1 < args.Length; i += 2)
                    {
                        string k = args[i] != null ? args[i].ToString() : null;
                        string v = args[i + 1] != null ? args[i + 1].ToString() : "";
                        if (k != null) raw = raw.Replace("{" + k + "}", v);
                    }
                }
            }
            return raw;
        }

        public static string tr(string key, string placeholderName, object val)
        {
            var placeholders = new Dictionary<string, object> { { placeholderName, val } };
            return tr(key, placeholders);
        }

        public static string tr(string key, string p1, object v1, string p2, object v2)
        {
            var placeholders = new Dictionary<string, object> { { p1, v1 }, { p2, v2 } };
            return tr(key, placeholders);
        }

        public static string CategoryTitle(string key)
        {
            if (CategoryLabels.ContainsKey(key))
            {
                var dict = CategoryLabels[key];
                if (dict.ContainsKey(lang_code)) return dict[lang_code];
                if (dict.ContainsKey("en")) return dict["en"];
            }
            return key;
        }

        private static string DetectWindowsTheme()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        var val = key.GetValue("AppsUseLightTheme");
                        if (val != null)
                        {
                            return (int)val == 1 ? "light" : "dark";
                        }
                    }
                }
            }
            catch { }
            return "dark";
        }

        public static string GetWindowsAccentColor()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\DWM"))
                {
                    if (key != null)
                    {
                        var val = key.GetValue("AccentColor");
                        if (val != null)
                        {
                            uint raw = Convert.ToUInt32(val);
                            byte r = (byte)(raw & 0xFF);
                            byte g = (byte)((raw >> 8) & 0xFF);
                            byte b = (byte)((raw >> 16) & 0xFF);
                            return string.Format("#{0:x2}{1:x2}{2:x2}", r, g, b);
                        }
                    }
                }
            }
            catch { }
            return "#0078d4"; // Default Segoe Blue
        }

        public static string LightenColor(string hexColor, double factor = 0.18)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hexColor);
                byte r = (byte)Math.Min(255, (int)(color.R + (255 - color.R) * factor));
                byte g = (byte)Math.Min(255, (int)(color.G + (255 - color.G) * factor));
                byte b = (byte)Math.Min(255, (int)(color.B + (255 - color.B) * factor));
                return string.Format("#{0:x2}{1:x2}{2:x2}", r, g, b);
            }
            catch
            {
                return hexColor;
            }
        }

        public static void UpdateT()
        {
            T.Clear();
            var baseColors = (theme == "light") ? LightColors : DarkColors;
            foreach (var pair in baseColors)
            {
                T[pair.Key] = pair.Value;
            }

            // Apply system accent color if it matches standard hex color format
            string systemAccent = GetWindowsAccentColor();
            if (!string.IsNullOrEmpty(systemAccent) && systemAccent.StartsWith("#") && systemAccent.Length == 7)
            {
                T["accent"] = systemAccent;
                T["accent_hv"] = LightenColor(systemAccent, 0.18);
                T["tog_on"] = systemAccent;
                T["nav_bar"] = systemAccent;
            }
        }

        public static void ApplyTheme(Application app)
        {
            UpdateT();

            // Store theme colors into Application resources as solid color brushes
            foreach (var pair in T)
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(pair.Value);
                    app.Resources[pair.Key + "Color"] = color;
                    app.Resources[pair.Key + "Brush"] = new SolidColorBrush(color);
                }
                catch { }
            }
        }
    }
}
