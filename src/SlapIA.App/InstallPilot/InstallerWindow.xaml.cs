using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace InstallPilot
{
    public partial class InstallerWindow : Window
    {
        private readonly List<Tuple<AppInfo, string>> _exeApps;
        private readonly Action _onDoneCallback;

        private class AppRowControls
        {
            public TextBlock IconLbl { get; set; }
            public ProgressBar ProgressBar { get; set; }
            public TextBlock StatusLbl { get; set; }
            public Button RetryBtn { get; set; }
            public Button SiteBtn { get; set; }
            public string State { get; set; }
        }

        private readonly Dictionary<string, AppRowControls> _rowControls = new Dictionary<string, AppRowControls>();
        private bool _done = false;
        private bool _cancelled = false;
        private string _scriptPath;
        private string _tempDir;
        private double _pollStartTime;
        private Thread _workerThread;
        private Thread _spinnerThread;
        private readonly List<Process> _activeProcs = new List<Process>();

        public InstallerWindow(Window owner, List<Tuple<AppInfo, string>> exeApps, Action onDone)
        {
            InitializeComponent();
            I18n.SetWindowIcon(this);
            Owner = owner;
            _exeApps = exeApps;
            _onDoneCallback = onDone;

            TranslateUI();
            BuildAppRows();

            _workerThread = new Thread(WorkerTask) { IsBackground = true };
            _workerThread.Start();

            _spinnerThread = new Thread(SpinnerTask) { IsBackground = true };
            _spinnerThread.Start();
        }

        private void TranslateUI()
        {
            Title = I18n.tr("inst_title", "n", _exeApps.Count);
            TxtTitle.Text = I18n.tr("inst_title", "n", _exeApps.Count);
            BtnSaveScript.Content = I18n.tr("inst_save_script");
            BtnAction.Content = I18n.tr("inst_cancel");
        }

        private void BuildAppRows()
        {
            StackApps.Children.Clear();
            _rowControls.Clear();

            foreach (var item in _exeApps)
            {
                var app = item.Item1;
                string source = item.Item2;

                // Card border
                var card = new Border
                {
                    Background = Application.Current.Resources["surfaceBrush"] as Brush,
                    BorderBrush = Application.Current.Resources["borderBrush"] as Brush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Height = 54,
                    Margin = new Thickness(0, 4, 0, 4)
                };

                // Grid layout inside card
                var grid = new Grid { Margin = new Thickness(12, 6, 12, 6) };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) }); // State icon
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) }); // Logo placeholder
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(145) }); // Name
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(155) }); // Progress Bar
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) }); // Status
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Buttons

                // State label (○)
                var iconLbl = new TextBlock
                {
                    Text = "○",
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 13,
                    Foreground = Application.Current.Resources["fg3Brush"] as Brush,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                Grid.SetColumn(iconLbl, 0);
                grid.Children.Add(iconLbl);

                // App Dynamic Icon with Ellipse fallback
                UIElement iconVisual = null;
                if (!string.IsNullOrEmpty(app.icon_path))
                {
                    try
                    {
                        var imgSource = I18n.LoadImage(app.icon_path);
                        if (imgSource != null)
                        {
                            var image = new Image
                            {
                                Width = 18,
                                Height = 18,
                                VerticalAlignment = VerticalAlignment.Center,
                                HorizontalAlignment = HorizontalAlignment.Center,
                                Stretch = Stretch.Uniform,
                                Source = imgSource
                            };
                            iconVisual = image;
                        }
                    }
                    catch
                    {
                        // Fallback
                    }
                }

                if (iconVisual == null)
                {
                    Brush appBrush = Brushes.Gray;
                    if (!string.IsNullOrEmpty(app.color))
                    {
                        try { appBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(app.color)); } catch { }
                    }
                    iconVisual = new Ellipse
                    {
                        Width = 14,
                        Height = 14,
                        Fill = appBrush,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                }

                Grid.SetColumn(iconVisual, 1);
                grid.Children.Add(iconVisual);

                // Name
                var nameLbl = new TextBlock
                {
                    Text = app.GetName(I18n.lang_code),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = Application.Current.Resources["fgBrush"] as Brush,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 0, 0, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                Grid.SetColumn(nameLbl, 2);
                grid.Children.Add(nameLbl);

                // Progress Bar
                var pb = new ProgressBar
                {
                    Width = 145,
                    Height = 6,
                    Minimum = 0,
                    Maximum = 1,
                    Value = 0,
                    Foreground = Application.Current.Resources["accentBrush"] as Brush,
                    Background = Application.Current.Resources["borderBrush"] as Brush,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(4, 0, 0, 0)
                };
                // Custom template for rounded progress bar
                pb.Template = CreateProgressBarTemplate();
                Grid.SetColumn(pb, 3);
                grid.Children.Add(pb);

                // Status text
                var statusLbl = new TextBlock
                {
                    Text = I18n.tr("inst_waiting"),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 10.5,
                    Foreground = Application.Current.Resources["fg3Brush"] as Brush,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 0, 0, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                Grid.SetColumn(statusLbl, 4);
                grid.Children.Add(statusLbl);

                // Stack for dynamic buttons (Retry / Site)
                var btnStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
                
                var retryBtn = new Button
                {
                    Content = I18n.tr("inst_retry"),
                    Width = 64,
                    Height = 22,
                    FontSize = 9.5,
                    Margin = new Thickness(0, 0, 4, 0),
                    Visibility = Visibility.Collapsed
                };
                retryBtn.Click += (s, e) => RetryApp(app);
                btnStack.Children.Add(retryBtn);

                var siteBtn = new Button
                {
                    Content = I18n.tr("inst_manual"),
                    Width = 54,
                    Height = 22,
                    FontSize = 9.5,
                    Visibility = Visibility.Collapsed
                };
                siteBtn.Click += (s, e) =>
                {
                    string url = !string.IsNullOrEmpty(app.official_url) ? app.official_url : app.store_url;
                    if (!string.IsNullOrEmpty(url)) Process.Start(url);
                };
                btnStack.Children.Add(siteBtn);

                Grid.SetColumn(btnStack, 5);
                grid.Children.Add(btnStack);

                card.Child = grid;
                StackApps.Children.Add(card);

                _rowControls[app.id] = new AppRowControls
                {
                    IconLbl = iconLbl,
                    ProgressBar = pb,
                    StatusLbl = statusLbl,
                    RetryBtn = retryBtn,
                    SiteBtn = siteBtn,
                    State = "waiting"
                };
            }
        }

        private ControlTemplate CreateProgressBarTemplate()
        {
            string xaml =
                @"<ControlTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" TargetType=""ProgressBar"">" +
                @"  <Grid x:Name=""TemplateRoot"" SnapsToDevicePixels=""true"">" +
                @"    <Border Background=""{TemplateBinding Background}"" CornerRadius=""3""/>" +
                @"    <Grid x:Name=""PART_Track"">" +
                @"      <Border x:Name=""PART_Indicator"" Background=""{TemplateBinding Foreground}"" CornerRadius=""3"" HorizontalAlignment=""Left""/>" +
                @"    </Grid>" +
                @"  </Grid>" +
                @"</ControlTemplate>";
            try
            {
                return System.Windows.Markup.XamlReader.Parse(xaml) as ControlTemplate;
            }
            catch
            {
                return null;
            }
        }

        private void UpdateGlobalProgress()
        {
            int total = _exeApps.Count;
            if (total == 0) return;

            int completed = 0;
            int active = 0;
            foreach (var item in _exeApps)
            {
                var row = _rowControls[item.Item1.id];
                if (row.State == "success" || row.State == "error")
                {
                    completed++;
                }
                else if (row.State == "active")
                {
                    active++;
                }
            }

            double progress = (double)(completed) / total;
            if (progress > 1.0) progress = 1.0;
            if (progress < 0.0) progress = 0.0;

            GlobalProgBar.IsIndeterminate = false;
            GlobalProgBar.Value = progress;

            TxtGlobalStatus.Text = I18n.tr("inst_summary", "ok", completed, "total", total);
        }

        private bool IsPowershellBlocked()
        {
            string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), ".InstallPilot_Temp_Check");
            try
            {
                Directory.CreateDirectory(tempDir);
                string testScript = System.IO.Path.Combine(tempDir, "test.ps1");
                string testOutput = System.IO.Path.Combine(tempDir, "test.txt");

                File.WriteAllText(testScript, string.Format("'ok' | Out-File '{0}' -Encoding utf8", testOutput));
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = string.Format("-ExecutionPolicy Bypass -WindowStyle Hidden -File \"{0}\"", testScript),
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using (var proc = Process.Start(startInfo))
                {
                    if (proc != null && proc.WaitForExit(4000))
                    {
                        if (File.Exists(testOutput)) return false;
                    }
                }
            }
            catch { }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
            return true;
        }

        private void WorkerTask()
        {
            try
            {
                Dispatcher.Invoke(() => TxtGlobalStatus.Text = I18n.tr("inst_checking_env"));
                bool isPsBlocked = IsPowershellBlocked();

                Dispatcher.Invoke(() => TxtGlobalStatus.Text = I18n.tr("inst_gen_script"));
                _tempDir = Installer.TempDir;

                // Separate Store apps (run directly in user context) from EXE apps (elevated script)
                var storeInstallApps = new List<Tuple<AppInfo, string>>();
                var exeInstallApps = new List<Tuple<AppInfo, string>>();
                foreach (var item in _exeApps)
                {
                    bool isStore = item.Item2 == "store";
                    string su = item.Item1.store_url ?? "";
                    bool hasStoreId = Regex.IsMatch(su, @"ProductId=[a-zA-Z0-9]{12,14}", RegexOptions.IgnoreCase)
                        || (!string.IsNullOrEmpty(item.Item1.winget_id)
                            && (item.Item1.winget_id.Length == 12 || item.Item1.winget_id.Length == 14)
                            && Regex.IsMatch(item.Item1.winget_id, "^[a-zA-Z0-9]+$"));
                    if (isStore && hasStoreId)
                        storeInstallApps.Add(item);
                    else
                        exeInstallApps.Add(item);
                }

                _pollStartTime = (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;

                Dispatcher.Invoke(() =>
                {
                    GlobalProgBar.IsIndeterminate = false;
                    GlobalProgBar.Value = 0.0;
                    TxtGlobalStatus.Text = I18n.tr("inst_summary", "ok", 0, "total", _exeApps.Count);

                    foreach (var item in _exeApps)
                    {
                        var app = item.Item1;
                        string source = item.Item2;
                        var row = _rowControls[app.id];
                        row.ProgressBar.IsIndeterminate = true;
                        row.State = "active";
                        row.StatusLbl.Text = source == "store" ? I18n.tr("inst_wait_store") : I18n.tr("inst_wait_official");
                        row.StatusLbl.Foreground = Application.Current.Resources["fgBrush"] as Brush;
                    }
                });

                // Store apps: download + run in user context (no elevation — required for get.microsoft.com installer)
                foreach (var storeItem in storeInstallApps)
                {
                    var capturedItem = storeItem;
                    new Thread(() => RunStoreInstallDirect(capturedItem.Item1)) { IsBackground = true }.Start();
                }

                // EXE apps: elevated script
                if (exeInstallApps.Count > 0)
                {
                    if (isPsBlocked)
                        _scriptPath = Installer.GenerateFallbackBatScript(exeInstallApps);
                    else
                        _scriptPath = Installer.GenerateNiniteScript(exeInstallApps);

                    var startInfo = new ProcessStartInfo { UseShellExecute = true, Verb = "runas" };
                    if (isPsBlocked)
                    {
                        startInfo.FileName = "cmd.exe";
                        startInfo.Arguments = string.Format("/c \"{0}\"", _scriptPath);
                        startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    }
                    else
                    {
                        startInfo.FileName = "powershell.exe";
                        startInfo.Arguments = string.Format("-ExecutionPolicy Bypass -WindowStyle Hidden -File \"{0}\"", _scriptPath);
                    }

                    Process proc = null;
                    try
                    {
                        proc = Process.Start(startInfo);
                        if (proc != null) lock (_activeProcs) _activeProcs.Add(proc);
                    }
                    catch
                    {
                        throw new Exception(I18n.tr("err_uac_denied"));
                    }
                }

                // Call polling loop
                Thread.Sleep(1000);
                PollStatusLoop();
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke((Action)(() =>
                {
                    MessageBox.Show(this, ex.Message, I18n.tr("err_generic"), MessageBoxButton.OK, MessageBoxImage.Error);
                    CancelExecution();
                }));
            }
        }

        private void PollStatusLoop()
        {
            const int timeoutSeconds = 1800; // 30 minutes

            while (!_cancelled && !_done)
            {
                double elapsed = (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds - _pollStartTime;
                if (elapsed > timeoutSeconds)
                {
                    Dispatcher.Invoke(OnAllDone);
                    return;
                }

                if (Directory.Exists(_tempDir))
                {
                    string doneFile = System.IO.Path.Combine(_tempDir, "script.done");

                    foreach (var item in _exeApps)
                    {
                        string appId = item.Item1.id;
                        var row = _rowControls[appId];
                        string statusFile = System.IO.Path.Combine(_tempDir, string.Format("app_{0}.status", appId));

                        if (File.Exists(statusFile))
                        {
                            try
                            {
                                string status = File.ReadAllText(statusFile).Trim();
                                Dispatcher.Invoke(() =>
                                {
                                    if (row.ProgressBar.IsIndeterminate)
                                    {
                                        if (status == "downloading")
                                        {
                                            row.StatusLbl.Text = I18n.tr("inst_downloading_x");
                                        }
                                        else if (status == "installing")
                                        {
                                            row.StatusLbl.Text = I18n.tr("inst_installing_x");
                                        }
                                        else if (status == "success")
                                        {
                                            row.ProgressBar.IsIndeterminate = false;
                                            row.ProgressBar.Value = 1.0;
                                            row.IconLbl.Text = "✓";
                                            row.IconLbl.Foreground = Application.Current.Resources["installedBrush"] as Brush;
                                            row.StatusLbl.Text = I18n.tr("inst_done");
                                            row.StatusLbl.Foreground = Application.Current.Resources["installedBrush"] as Brush;
                                            row.State = "success";
                                        }
                                        else if (status.StartsWith("error:"))
                                        {
                                            string err = status.Substring(6).Trim();
                                            row.ProgressBar.IsIndeterminate = false;
                                            row.ProgressBar.Value = 1.0;
                                            row.IconLbl.Text = "✗";
                                            row.IconLbl.Foreground = Application.Current.Resources["errorBrush"] as Brush;
                                            row.StatusLbl.Text = I18n.tr("inst_error_prefix", "msg", err);
                                            row.StatusLbl.Foreground = Application.Current.Resources["errorBrush"] as Brush;
                                            row.State = "error";
                                            row.RetryBtn.Visibility = Visibility.Visible;
                                            if (!string.IsNullOrEmpty(item.Item1.official_url) || !string.IsNullOrEmpty(item.Item1.store_url))
                                            {
                                                row.SiteBtn.Visibility = Visibility.Visible;
                                            }
                                        }
                                    }
                                });
                            }
                            catch { }
                        }
                    }

                    Dispatcher.Invoke(() => UpdateGlobalProgress());

                    // Completion: script.done (EXE apps) OR all status files final (Store-only or mixed)
                    bool allFinal = true;
                    foreach (var item in _exeApps)
                    {
                        string sf = System.IO.Path.Combine(_tempDir, string.Format("app_{0}.status", item.Item1.id));
                        if (File.Exists(sf))
                        {
                            string s = "";
                            try { s = File.ReadAllText(sf).Trim(); } catch { }
                            if (s != "success" && !s.StartsWith("error:")) { allFinal = false; break; }
                        }
                        else { allFinal = false; break; }
                    }

                    if (allFinal || File.Exists(doneFile))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            OnAllDone();
                            try { Directory.Delete(_tempDir, true); } catch { }
                        });
                        return;
                    }
                }

                Thread.Sleep(1000);
            }
        }

        private void OnAllDone()
        {
            _done = true;
            GlobalProgBar.IsIndeterminate = false;
            GlobalProgBar.Value = 1.0;
            TxtGlobalStatus.Text = I18n.tr("inst_all_done");
            BtnAction.Content = I18n.tr("inst_close");
            BtnSaveScript.IsEnabled = true;

            // Update remaining indeterminate rows to error
            foreach (var item in _exeApps)
            {
                var row = _rowControls[item.Item1.id];
                if (row.State == "active" || row.State == "waiting")
                {
                    row.ProgressBar.IsIndeterminate = false;
                    row.ProgressBar.Value = 1.0;
                    row.IconLbl.Text = "✗";
                    row.IconLbl.Foreground = Application.Current.Resources["errorBrush"] as Brush;
                    row.StatusLbl.Text = I18n.tr("inst_unknown_error");
                    row.StatusLbl.Foreground = Application.Current.Resources["errorBrush"] as Brush;
                    row.State = "error";
                    row.RetryBtn.Visibility = Visibility.Visible;
                    if (!string.IsNullOrEmpty(item.Item1.official_url) || !string.IsNullOrEmpty(item.Item1.store_url))
                    {
                        row.SiteBtn.Visibility = Visibility.Visible;
                    }
                }
            }
        }

        private void SpinnerTask()
        {
            string[] spinFrames = { "|", "/", "—", "\\" };
            int idx = 0;
            while (!_cancelled && !_done)
            {
                Dispatcher.Invoke(() =>
                {
                    foreach (var pair in _rowControls)
                    {
                        if (pair.Value.State == "active")
                        {
                            pair.Value.IconLbl.Text = spinFrames[idx % spinFrames.Length];
                            pair.Value.IconLbl.Foreground = Application.Current.Resources["accentBrush"] as Brush;
                        }
                    }
                });
                idx++;
                Thread.Sleep(150);
            }
        }

        private void RetryApp(AppInfo app)
        {
            var row = _rowControls[app.id];
            row.RetryBtn.Visibility = Visibility.Collapsed;
            row.SiteBtn.Visibility = Visibility.Collapsed;
            row.ProgressBar.IsIndeterminate = true;
            row.IconLbl.Text = "○";
            row.IconLbl.Foreground = Application.Current.Resources["fg3Brush"] as Brush;
            row.StatusLbl.Text = I18n.tr("inst_retry_attempt");
            row.StatusLbl.Foreground = Application.Current.Resources["fgBrush"] as Brush;
            row.State = "active";

            // Spawn inline download task in a separate background thread
            var thread = new Thread(() =>
            {
                bool success = false;
                string errStr = "";
                try
                {
                    // Find installation method: winget, store, or direct download
                    string wid = app.winget_id;
                    string defaultSource = I18n.default_source;
                    bool prefersStore = defaultSource == "store";
                    
                    if (prefersStore && !string.IsNullOrEmpty(app.store_url))
                    {
                        success = InstallViaStore(app, out errStr);
                    }
                    else if (!string.IsNullOrEmpty(wid))
                    {
                        success = InstallViaWinget(app, out errStr);
                    }
                    else
                    {
                        string dlUrl = Installer.ResolveDownloadUrl(app);
                        if (!string.IsNullOrEmpty(dlUrl))
                        {
                            success = DownloadAndInstallDirect(app, dlUrl, out errStr);
                        }
                        else
                        {
                            errStr = I18n.tr("inst_no_url");
                        }
                    }
                }
                catch (Exception ex)
                {
                    errStr = ex.Message;
                }

                Dispatcher.Invoke(() =>
                {
                    row.ProgressBar.IsIndeterminate = false;
                    row.ProgressBar.Value = 1.0;

                    if (success)
                    {
                        row.IconLbl.Text = "✓";
                        row.IconLbl.Foreground = Application.Current.Resources["installedBrush"] as Brush;
                        row.StatusLbl.Text = I18n.tr("inst_done");
                        row.StatusLbl.Foreground = Application.Current.Resources["installedBrush"] as Brush;
                        row.State = "success";
                    }
                    else
                    {
                        row.IconLbl.Text = "✗";
                        row.IconLbl.Foreground = Application.Current.Resources["errorBrush"] as Brush;
                        row.StatusLbl.Text = I18n.tr("inst_error_prefix", "msg", errStr);
                        row.StatusLbl.Foreground = Application.Current.Resources["errorBrush"] as Brush;
                        row.State = "error";
                        row.RetryBtn.Visibility = Visibility.Visible;
                        if (!string.IsNullOrEmpty(app.official_url) || !string.IsNullOrEmpty(app.store_url))
                        {
                            row.SiteBtn.Visibility = Visibility.Visible;
                        }
                    }
                    UpdateGlobalProgress();
                });
            }) { IsBackground = true };
            thread.Start();
        }

        private void RunStoreInstallDirect(AppInfo app)
        {
            string appId = app.id;
            string statusFile = System.IO.Path.Combine(_tempDir, string.Format("app_{0}.status", appId));
            string dest = null;
            try
            {
                string storeId = GetStoreId(app);
                if (string.IsNullOrEmpty(storeId))
                {
                    File.WriteAllText(statusFile, "error: No ProductId");
                    return;
                }

                File.WriteAllText(statusFile, "downloading");
                dest = System.IO.Path.Combine(_tempDir, string.Format("StoreInstaller_{0}.exe", storeId));

                using (var wc = new WebClient())
                {
                    wc.Headers["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
                    wc.DownloadFile(string.Format("https://get.microsoft.com/installer/download/{0}", storeId), dest);
                }

                File.WriteAllText(statusFile, "installing");

                var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = dest,
                    Arguments = "-silent",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                if (proc != null)
                {
                    lock (_activeProcs) _activeProcs.Add(proc);
                    proc.WaitForExit(300000);
                    File.WriteAllText(statusFile, (proc.ExitCode == 0 || proc.ExitCode == 3010)
                        ? "success"
                        : string.Format("error: Code {0}", proc.ExitCode));
                }
                else
                {
                    File.WriteAllText(statusFile, "error: Failed to start installer");
                }
            }
            catch (Exception ex)
            {
                try { File.WriteAllText(statusFile, string.Format("error: {0}", ex.Message)); } catch { }
            }
            finally
            {
                if (dest != null) try { File.Delete(dest); } catch { }
            }
        }

        private static string GetStoreId(AppInfo app)
        {
            var m = Regex.Match(app.store_url ?? "", @"(?:/detail/|ProductId=)([a-zA-Z0-9]{12,14})", RegexOptions.IgnoreCase);
            if (m.Success) return m.Groups[1].Value.ToUpper();
            if (!string.IsNullOrEmpty(app.winget_id)
                && (app.winget_id.Length == 12 || app.winget_id.Length == 14)
                && Regex.IsMatch(app.winget_id, "^[a-zA-Z0-9]+$"))
                return app.winget_id.ToUpper();
            return null;
        }

        private bool InstallViaStore(AppInfo app, out string errStr)
        {
            errStr = "";
            string storeId = GetStoreId(app);
            if (string.IsNullOrEmpty(storeId)) { errStr = "No ProductId"; return false; }

            string dest = System.IO.Path.Combine(System.IO.Path.GetTempPath(), string.Format("StoreInstaller_{0}.exe", storeId));
            try
            {
                using (var wc = new WebClient())
                {
                    wc.Headers["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
                    wc.DownloadFile(string.Format("https://get.microsoft.com/installer/download/{0}", storeId), dest);
                }
                var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = dest,
                    Arguments = "-silent",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (proc != null)
                {
                    lock (_activeProcs) _activeProcs.Add(proc);
                    proc.WaitForExit(300000);
                    if (proc.ExitCode != 0 && proc.ExitCode != 3010) { errStr = string.Format("Code {0}", proc.ExitCode); return false; }
                    return true;
                }
                errStr = "Failed to start";
                return false;
            }
            catch (Exception ex) { errStr = ex.Message; return false; }
            finally { try { File.Delete(dest); } catch { } }
        }

        private bool InstallViaWinget(AppInfo app, out string errStr)
        {
            errStr = "";
            string wingetBin = Detection.FindWingetPath();
            var proc = SpawnProcess(wingetBin, string.Format("install --id \"{0}\" -e --accept-source-agreements --accept-package-agreements --silent", app.winget_id));
            
            // Wait up to 5 minutes
            proc.WaitForExit(300000);
            if (proc.ExitCode != 0 && proc.ExitCode != 3010)
            {
                errStr = string.Format("Exit Code {0}", proc.ExitCode);
                return false;
            }
            return true;
        }

        private bool DownloadAndInstallDirect(AppInfo app, string url, out string errStr)
        {
            errStr = "";
            string ext = ".exe";
            try
            {
                var uri = new Uri(url);
                ext = System.IO.Path.GetExtension(uri.AbsolutePath).ToLower();
            }
            catch { }
            if (string.IsNullOrEmpty(ext))
            {
                ext = url.ToLower().Contains("msi") ? ".msi" : url.ToLower().Contains("msix") ? ".msix" : ".exe";
            }

            string dest = System.IO.Path.Combine(System.IO.Path.GetTempPath(), string.Format("InstallPilot_Temp_{0}{1}", app.id, ext));
            try
            {
                using (var wc = new WebClient())
                {
                    // Update progress callback
                    wc.DownloadProgressChanged += (s, e) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            var row = _rowControls[app.id];
                            row.ProgressBar.IsIndeterminate = false;
                            row.ProgressBar.Value = (double)e.ProgressPercentage / 100;
                            row.StatusLbl.Text = I18n.tr("inst_downloading", "pct", e.ProgressPercentage);
                        });
                    };
                    wc.DownloadFileTaskAsync(new Uri(url), dest).Wait();
                }

                Dispatcher.Invoke(() =>
                {
                    var row = _rowControls[app.id];
                    row.ProgressBar.IsIndeterminate = true;
                    row.StatusLbl.Text = I18n.tr("inst_installing_popup");
                });

                Process proc = null;
                if (ext == ".msix" || ext == ".msixbundle" || ext == ".appx" || ext == ".appxbundle")
                {
                    proc = SpawnProcess("powershell.exe", string.Format("-Command \"Add-AppxPackage -Path '{0}'\"", dest));
                }
                else if (ext == ".msi")
                {
                    proc = SpawnProcess("msiexec.exe", string.Format("/i \"{0}\" /qn /norestart", dest));
                }
                else
                {
                    string[] sargs = Installer.GetSilentArgs(dest, app);
                    string argsStr = string.Join(" ", sargs);
                    proc = SpawnProcess(dest, argsStr);
                }

                proc.WaitForExit(600000);
                if (proc.ExitCode != 0 && proc.ExitCode != 3010)
                {
                    errStr = string.Format("Exit Code {0}", proc.ExitCode);
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                errStr = ex.Message;
                return false;
            }
            finally
            {
                try { File.Delete(dest); } catch { }
            }
        }

        private Process SpawnProcess(string file, string args)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = file,
                Arguments = args,
                CreateNoWindow = true,
                UseShellExecute = false
            };
            var proc = Process.Start(startInfo);
            if (proc != null)
            {
                lock (_activeProcs) _activeProcs.Add(proc);
            }
            return proc;
        }

        private void BtnSaveScript_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Installer.GenerateSaveScriptFile(_exeApps);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, I18n.tr("err_generic"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAction_Click(object sender, RoutedEventArgs e)
        {
            if (_done)
            {
                CloseAndRefresh();
            }
            else
            {
                CancelExecution();
            }
        }

        private void CancelExecution()
        {
            _cancelled = true;

            // Kill running powershell scripts/children
            if (!string.IsNullOrEmpty(_scriptPath))
            {
                try
                {
                    string escapedPath = _scriptPath.Replace("\\", "\\\\").Replace("'", "''");
                    string psCmd = 
                        "function Kill-Tree ($ppid) {" +
                        "  Get-CimInstance Win32_Process -Filter \"ParentProcessId = $ppid\" | ForEach-Object { Kill-Tree $_.ProcessId };" +
                        "  Stop-Process -Id $ppid -Force -ErrorAction SilentlyContinue" +
                        "}" +
                        string.Format("$p = Get-CimInstance Win32_Process | Where-Object {{ $_.CommandLine -like '*{0}*' }};", escapedPath) +
                        "if ($p) { $p | ForEach-Object { Kill-Tree $_.ProcessId } }";

                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = string.Format("-NoProfile -Command \"{0}\"", psCmd),
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    Process procToWait = Process.Start(startInfo);
                    if (procToWait != null)
                    {
                        procToWait.WaitForExit(5000);
                    }
                }
                catch { }
            }

            lock (_activeProcs)
            {
                foreach (var proc in _activeProcs)
                {
                    try { proc.Kill(); } catch { }
                }
                _activeProcs.Clear();
            }

            Close();
        }

        private void CloseAndRefresh()
        {
            Close();
            if (_onDoneCallback != null)
            {
                _onDoneCallback();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _cancelled = true;
            base.OnClosed(e);
        }
    }
}
