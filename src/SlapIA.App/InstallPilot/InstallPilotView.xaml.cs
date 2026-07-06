using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SlapIA.App.Services;

namespace InstallPilot
{
    public partial class InstallPilotView : UserControl
    {
        private List<AppInfo> _allApps = new List<AppInfo>();
        private string _activeCategory = "all";
        private readonly Dictionary<string, bool> _selections = new Dictionary<string, bool>();
        private readonly Dictionary<string, string> _sourceSelections = new Dictionary<string, string>();
        private readonly Dictionary<string, Tuple<bool, string>> _installedStatuses = new Dictionary<string, Tuple<bool, string>>();
        private readonly HashSet<string> _upgradesAvailable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, Border> _navButtons = new Dictionary<string, Border>();
        private readonly Dictionary<string, TextBlock> _navIcons = new Dictionary<string, TextBlock>();
        private readonly Dictionary<string, TextBlock> _navTexts = new Dictionary<string, TextBlock>();
        private readonly Dictionary<string, Border> _navBars = new Dictionary<string, Border>();

        private bool _isScanning = false;
        private bool _initialized = false;

        private static readonly Dictionary<string, string> CategoryIcons = new Dictionary<string, string>
        {
            { "all", "📦" },
            { "only_store", "🛒" },
            { "only_exe", "💾" },
            { "web", "🌐" },
            { "messaging", "💬" },
            { "games", "🎮" },
            { "media", "🎵" },
            { "productivity", "📈" },
            { "security", "🔒" },
            { "utilities", "🛠" },
            { "dev_tools", "⚙️" },
            { "other", "📦" }
        };

        private static readonly string[] CategoryOrder = {
            "web", "messaging", "games", "media", "productivity", "security", "utilities", "dev_tools", "other"
        };

        public InstallPilotView()
        {
            InitializeComponent();
            InitializeWindowIcons();
            TranslateUI();
            Loaded += InstallPilotView_Loaded;
            Unloaded += (_, _) => I18n.Changed -= OnI18nChanged;
            I18n.Changed += OnI18nChanged;
        }

        private void InstallPilotView_Loaded(object sender, RoutedEventArgs e)
        {
            if (_initialized) return;
            _initialized = true;
            RunStartupFlow();
        }

        /// <summary>Keeps InstallPilot's own texts/colors in sync whenever language or theme
        /// changes from ANY source - InstallPilot's own combo, the app-wide Preferences button,
        /// or SlapIA's theme following the OS live.</summary>
        private void OnI18nChanged()
        {
            TranslateUI();
            RebuildSidebar();
            RefreshLayout();
        }

        private void InitializeWindowIcons()
        {
            // Load logo image dynamically
            try
            {
                var logoImg = I18n.LoadImage("icons/logo.png");
                if (logoImg != null)
                {
                    ImgLogo.Source = logoImg;
                    ImgLogo.Visibility = Visibility.Visible;
                    LogoFallbackCircle.Visibility = Visibility.Collapsed;
                }
            }
            catch
            {
                // Fallback is visible by default in XAML
            }
        }

        private void RunStartupFlow()
        {
            I18n.LoadSettings();
            // SlapIA Tool's Preferences button is the single, app-wide language/theme control now
            // - it takes precedence over InstallPilot's own persisted settings so both halves of
            // the app always agree.
            I18n.lang_code = LocalizationService.Instance.CurrentLanguage;
            I18n.theme = SlapIA.App.App.ThemeService.CurrentTheme == SlapIA.App.Services.AppTheme.Dark ? "dark" : "light";
            I18n.ApplyTheme(Application.Current);

            if (!I18n.setup_completed)
            {
                var wizard = new WizardWindow { Owner = Window.GetWindow(this) };
                bool? result = wizard.ShowDialog();
                if (result != true)
                {
                    return;
                }
                I18n.ApplyTheme(Application.Current);
            }

            TranslateUI();

            var startupWin = new StartupWindow();
            startupWin.Show();

            var scanThread = new Thread(() =>
            {
                try
                {
                    var allApps = AppConfig.LoadApps();
                    var statuses = new Dictionary<string, Tuple<bool, string>>();

                    Detection.ClearCaches();
                    Detection.GetRegistryApps();
                    Detection.GetAppxPackages();

                    for (int i = 0; i < allApps.Count; i++)
                    {
                        var app = allApps[i];
                        string name = app.GetName(I18n.lang_code);
                        startupWin.UpdateProgress(i + 1, allApps.Count, name);

                        try
                        {
                            statuses[app.id] = Detection.DetectInstallation(app);
                        }
                        catch
                        {
                            statuses[app.id] = Tuple.Create(false, (string)null);
                        }
                    }

                    startupWin.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            _allApps = allApps;
                            foreach (var pair in statuses)
                            {
                                _installedStatuses[pair.Key] = pair.Value;
                            }

                            ApplyPreferredSourceOptions();
                            RebuildSidebar();
                            RefreshLayout();

                            startupWin.Close();

                            var wingetThread = new Thread(WingetScanTask) { IsBackground = true };
                            wingetThread.Start();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Fatal Error in InstallPilot startup: " + ex.Message + "\n" + ex.StackTrace);
                            startupWin.Close();
                        }
                    });
                }
                catch (Exception ex)
                {
                    startupWin.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show("Fatal Error in background scan: " + ex.Message + "\n" + ex.StackTrace);
                        startupWin.Close();
                    });
                }
            }) { IsBackground = true };
            scanThread.Start();
        }

        private void WingetScanTask()
        {
            try
            {
                Detection.LoadWingetCache();
                Dispatcher.Invoke(() =>
                {
                    // Refresh statuses based on loaded winget caches
                    foreach (var app in _allApps)
                    {
                        var status = Detection.DetectInstallation(app);
                        _installedStatuses[app.id] = status;
                    }
                    RefreshLayout();
                });

                // Check upgrades
                Detection.LoadWingetUpgradesCache();
                var upgrades = Detection.GetWingetUpgrades();
                Dispatcher.Invoke(() =>
                {
                    _upgradesAvailable.Clear();
                    foreach (var up in upgrades)
                    {
                        _upgradesAvailable.Add(up);
                    }
                    RefreshLayout();
                });
            }
            catch { }
        }

        private void TranslateUI()
        {
            TxtSearchPlaceholder.Text = I18n.tr("search_ph");
            BtnPrefs.Content = I18n.tr("preferences");
            BtnUpdateAll.Content = I18n.tr("update_all");
            BtnRefresh.Content = I18n.tr("check_now");
            BtnInstall.Content = I18n.tr("install_selected");
            TxtFooterVersion.Text = "InstallPilot V9";

            if (_activeCategory == "all")
            {
                TxtPageTitle.Text = I18n.tr("step1");
                TxtPageSubtitle.Text = I18n.tr("install_instructions");
            }
            else if (_activeCategory == "only_store")
            {
                TxtPageTitle.Text = I18n.tr("nav_only_store");
                TxtPageSubtitle.Text = I18n.tr("install_instructions");
            }
            else if (_activeCategory == "only_exe")
            {
                TxtPageTitle.Text = I18n.tr("nav_only_exe");
                TxtPageSubtitle.Text = I18n.tr("install_instructions");
            }
            else
            {
                TxtPageTitle.Text = I18n.CategoryTitle(_activeCategory);
                TxtPageSubtitle.Text = I18n.tr("install_instructions");
            }
        }

        private void ApplyPreferredSourceOptions()
        {
            foreach (var app in _allApps)
            {
                string choice = I18n.default_source; // Default: store, exe or null

                // Validate if selected default source is available for this app
                bool hasStore = !string.IsNullOrEmpty(app.store_url);
                bool hasExe = !string.IsNullOrEmpty(app.official_url) || !string.IsNullOrEmpty(app.download_url);

                if (choice == "store" && hasStore)
                {
                    _sourceSelections[app.id] = "store";
                }
                else if (choice == "exe" && hasExe)
                {
                    _sourceSelections[app.id] = "exe";
                }
                else
                {
                    // Choose first available source
                    if (hasStore) _sourceSelections[app.id] = "store";
                    else if (hasExe) _sourceSelections[app.id] = "exe";
                    else _sourceSelections[app.id] = "store";
                }
            }
        }

        private void RebuildSidebar()
        {
            StackNav.Children.Clear();
            _navButtons.Clear();
            _navIcons.Clear();
            _navTexts.Clear();
            _navBars.Clear();

            // 1. Navigation items
            AddNavItem("all", I18n.tr("nav_all"));
            AddNavItem("only_store", I18n.tr("nav_only_store"));
            AddNavItem("only_exe", I18n.tr("nav_only_exe"));

            // Divider
            var divider = new Border
            {
                Height = 1,
                Background = Application.Current.Resources["borderBrush"] as Brush,
                Margin = new Thickness(0, 6, 0, 6)
            };
            StackNav.Children.Add(divider);

            // 2. Category items
            var distinctCategories = new HashSet<string>();
            foreach (var app in _allApps)
            {
                if (!string.IsNullOrEmpty(app.category))
                    distinctCategories.Add(app.category);
            }

            foreach (var cat in CategoryOrder)
            {
                if (distinctCategories.Contains(cat))
                {
                    AddNavItem(cat, I18n.CategoryTitle(cat));
                }
            }

            HighlightActiveNavItem();
        }

        private void AddNavItem(string key, string text)
        {
            var border = new Border
            {
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(6),
                Height = 38,
                Margin = new Thickness(0, 2, 0, 2),
                Cursor = Cursors.Hand
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3) }); // Active indicator bar
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) }); // Icon
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Text

            // Accent bar indicator on the left
            var bar = new Border
            {
                Background = Brushes.Transparent,
                Width = 3,
                CornerRadius = new CornerRadius(1.5),
                Margin = new Thickness(0, 9, 0, 9)
            };
            Grid.SetColumn(bar, 0);
            grid.Children.Add(bar);

            // Icon
            string iconStr = CategoryIcons.ContainsKey(key) ? CategoryIcons[key] : "📦";
            var iconLbl = new TextBlock
            {
                Text = iconStr,
                FontFamily = new FontFamily("Segoe UI Emoji"),
                FontSize = 12,
                Foreground = Application.Current.Resources["fg2Brush"] as Brush,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(iconLbl, 1);
            grid.Children.Add(iconLbl);

            // Text
            var textLbl = new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11,
                Foreground = Application.Current.Resources["fg2Brush"] as Brush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0)
            };
            Grid.SetColumn(textLbl, 2);
            grid.Children.Add(textLbl);

            border.Child = grid;

            // Hover and Click events
            border.MouseEnter += (s, e) =>
            {
                if (_activeCategory != key)
                    border.Background = Application.Current.Resources["sidebar_hoverBrush"] as Brush;
            };
            border.MouseLeave += (s, e) =>
            {
                if (_activeCategory != key)
                    border.Background = Brushes.Transparent;
            };
            border.MouseLeftButtonDown += (s, e) =>
            {
                SetCategory(key);
            };

            StackNav.Children.Add(border);

            _navButtons[key] = border;
            _navIcons[key] = iconLbl;
            _navTexts[key] = textLbl;
            _navBars[key] = bar;
        }

        private void SetCategory(string key)
        {
            _activeCategory = key;
            HighlightActiveNavItem();
            TranslateUI();
            RefreshLayout();
        }

        private void HighlightActiveNavItem()
        {
            foreach (var pair in _navButtons)
            {
                string key = pair.Key;
                var border = pair.Value;
                var icon = _navIcons[key];
                var text = _navTexts[key];
                var bar = _navBars[key];

                if (key == _activeCategory)
                {
                    border.Background = Application.Current.Resources["sidebar_activeBrush"] as Brush;
                    icon.Foreground = Application.Current.Resources["nav_barBrush"] as Brush;
                    text.Foreground = Application.Current.Resources["nav_barBrush"] as Brush;
                    bar.Background = Application.Current.Resources["nav_barBrush"] as Brush;
                }
                else
                {
                    border.Background = Brushes.Transparent;
                    icon.Foreground = Application.Current.Resources["fg2Brush"] as Brush;
                    text.Foreground = Application.Current.Resources["fg2Brush"] as Brush;
                    bar.Background = Brushes.Transparent;
                }
            }
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            TxtSearchPlaceholder.Visibility = string.IsNullOrEmpty(TxtSearch.Text) ? Visibility.Visible : Visibility.Collapsed;
            RefreshLayout();
        }

        private bool MatchesCurrentFilter(AppInfo app)
        {
            string search = TxtSearch.Text.Trim().ToLower();
            if (!string.IsNullOrEmpty(search) && !app.GetName(I18n.lang_code).ToLower().Contains(search))
            {
                return false;
            }

            if (_activeCategory == "only_store" && string.IsNullOrEmpty(app.store_url))
            {
                return false;
            }

            if (_activeCategory == "only_exe" && string.IsNullOrEmpty(app.official_url) && string.IsNullOrEmpty(app.download_url))
            {
                return false;
            }

            if (_activeCategory != "all" && _activeCategory != "only_store" && _activeCategory != "only_exe")
            {
                if (app.category != _activeCategory) return false;
            }

            return true;
        }

        private void RefreshLayout()
        {
            StackContent.Children.Clear();

            // Group apps by category
            var groupedApps = new Dictionary<string, List<AppInfo>>();
            foreach (var app in _allApps)
            {
                if (MatchesCurrentFilter(app))
                {
                    string cat = app.category ?? "other";
                    if (!groupedApps.ContainsKey(cat)) groupedApps[cat] = new List<AppInfo>();
                    groupedApps[cat].Add(app);
                }
            }

            // Render categories and apps
            foreach (var cat in CategoryOrder)
            {
                if (groupedApps.ContainsKey(cat) && groupedApps[cat].Count > 0)
                {
                    RenderCategoryBlock(cat, groupedApps[cat]);
                }
            }

            UpdateSelectionStateText();
        }

        private void RenderCategoryBlock(string categoryKey, List<AppInfo> apps)
        {
            // Category Panel Container
            var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };

            // Category Header Grid
            var headerGrid = new Grid { Margin = new Thickness(4, 0, 4, 4) };
            var title = new TextBlock
            {
                Text = I18n.CategoryTitle(categoryKey),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = Application.Current.Resources["fgBrush"] as Brush,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            headerGrid.Children.Add(title);

            // Select all link for this category
            bool hasUninstalled = false;
            bool allSelected = true;
            foreach (var app in apps)
            {
                bool isInstalled = _installedStatuses.ContainsKey(app.id) && _installedStatuses[app.id].Item1;
                if (!isInstalled)
                {
                    hasUninstalled = true;
                    if (!_selections.ContainsKey(app.id) || !_selections[app.id])
                    {
                        allSelected = false;
                    }
                }
            }

            var toggleLink = new TextBlock
            {
                Text = allSelected && hasUninstalled ? I18n.tr("select_none") : I18n.tr("select_all"),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 10,
                TextDecorations = TextDecorations.Underline,
                Foreground = hasUninstalled ? Application.Current.Resources["accentBrush"] as Brush : Application.Current.Resources["fg3Brush"] as Brush,
                Cursor = hasUninstalled ? Cursors.Hand : Cursors.Arrow,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            if (hasUninstalled)
            {
                toggleLink.MouseLeftButtonDown += (s, e) =>
                {
                    bool targetState = !(allSelected && hasUninstalled);
                    foreach (var app in apps)
                    {
                        bool isInstalled = _installedStatuses.ContainsKey(app.id) && _installedStatuses[app.id].Item1;
                        if (!isInstalled)
                        {
                            _selections[app.id] = targetState;
                        }
                    }
                    RefreshLayout();
                };
            }
            headerGrid.Children.Add(toggleLink);
            panel.Children.Add(headerGrid);

            // Separator Line
            var separator = new Border
            {
                Height = 1,
                Background = Application.Current.Resources["borderBrush"] as Brush,
                Margin = new Thickness(0, 0, 0, 6)
            };
            panel.Children.Add(separator);

            // WrapPanel to list the cards horizontally and wrap natively!
            var wrapPanel = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (var app in apps)
            {
                wrapPanel.Children.Add(BuildAppCard(app));
            }
            panel.Children.Add(wrapPanel);

            StackContent.Children.Add(panel);
        }

        private Border BuildAppCard(AppInfo app)
        {
            bool isInstalled = _installedStatuses.ContainsKey(app.id) && _installedStatuses[app.id].Item1;
            string installedSrc = _installedStatuses.ContainsKey(app.id) ? _installedStatuses[app.id].Item2 : null;
            bool hasUpgrade = _upgradesAvailable.Contains(app.id) || (!string.IsNullOrEmpty(app.winget_id) && _upgradesAvailable.Contains(app.winget_id));
            bool isNvidiaBlocked = app.id == "nvidia_app" && !Detection.HasNvidiaGpu();

            var border = new Border
            {
                Background = Application.Current.Resources["surfaceBrush"] as Brush,
                BorderBrush = Application.Current.Resources["borderBrush"] as Brush,
                Opacity = isNvidiaBlocked ? 0.45 : 1.0,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Width = 370,
                Height = 56,
                Margin = new Thickness(0, 4, 8, 4)
            };

            var grid = new Grid { Margin = new Thickness(10, 6, 10, 6) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) }); // Checkbox
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) }); // Color/Icon
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Name/Desc
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Source Toggle
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Installed badge

            // Selection Checkbox
            var cb = new CheckBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                IsEnabled = !isInstalled && !isNvidiaBlocked
            };
            if (_selections.ContainsKey(app.id))
            {
                cb.IsChecked = _selections[app.id];
            }
            cb.Checked += (s, e) => { _selections[app.id] = true; UpdateSelectionStateText(); };
            cb.Unchecked += (s, e) => { _selections[app.id] = false; UpdateSelectionStateText(); };

            Grid.SetColumn(cb, 0);
            grid.Children.Add(cb);

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
                            Width = 20,
                            Height = 20,
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

            // Name + Description Stack
            var nameStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 6, 0) };

            var nameText = new TextBlock
            {
                Text = app.GetName(I18n.lang_code),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                Foreground = Application.Current.Resources["fgBrush"] as Brush,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            if (hasUpgrade)
            {
                nameText.Text += "  ↑";
                nameText.Foreground = Application.Current.Resources["accentBrush"] as Brush;
            }
            nameStack.Children.Add(nameText);

            var descText = new TextBlock
            {
                Text = app.GetDescription(I18n.lang_code),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 9.5,
                Foreground = Application.Current.Resources["fg2Brush"] as Brush,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 1, 0, 0)
            };
            nameStack.Children.Add(descText);

            Grid.SetColumn(nameStack, 2);
            grid.Children.Add(nameStack);

            // Source segmented selector (Store / EXE)
            bool hasStore = !string.IsNullOrEmpty(app.store_url);
            bool hasExe = !string.IsNullOrEmpty(app.official_url) || !string.IsNullOrEmpty(app.download_url);

            if (!isInstalled && !isNvidiaBlocked && (hasStore || hasExe))
            {
                string chosenSrc = _sourceSelections.ContainsKey(app.id) ? _sourceSelections[app.id] : "store";
                var srcBorder = new Border
                {
                    BorderBrush = Application.Current.Resources["borderBrush"] as Brush,
                    BorderThickness = new Thickness(1),
                    Background = Application.Current.Resources["surface2Brush"] as Brush,
                    CornerRadius = new CornerRadius(4),
                    Height = 24,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(4, 0, 4, 0)
                };

                var srcGrid = new Grid();
                srcGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                srcGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // Store segment
                var storeSeg = new Border
                {
                    Background = (chosenSrc == "store") ? Application.Current.Resources["accentBrush"] as Brush : Brushes.Transparent,
                    CornerRadius = new CornerRadius(3, 0, 0, 3),
                    Padding = new Thickness(8, 2, 8, 2),
                    Cursor = hasStore ? Cursors.Hand : Cursors.Arrow,
                    IsEnabled = hasStore,
                    Opacity = hasStore ? 1.0 : 0.4
                };

                UIElement storeContent = null;
                string storeIconName = (I18n.theme == "dark") ? "ms_store_dark.png" : "ms_store_light.png";
                try
                {
                    var imgSource = I18n.LoadImage("icons/" + storeIconName);
                    if (imgSource != null)
                    {
                        var image = new Image
                        {
                            Width = 12,
                            Height = 12,
                            VerticalAlignment = VerticalAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Stretch = Stretch.Uniform,
                            Source = imgSource
                        };

                        var container = new Grid
                        {
                            Width = 24,
                            Height = 16,
                            VerticalAlignment = VerticalAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Center
                        };
                        container.Children.Add(image);
                        storeContent = container;
                    }
                }
                catch
                {
                    // Fallback
                }

                if (storeContent == null)
                {
                    storeContent = new TextBlock
                    {
                        Text = I18n.tr("src_store"),
                        FontSize = 9.5,
                        FontWeight = FontWeights.Bold,
                        Foreground = (chosenSrc == "store") ? Brushes.White : Application.Current.Resources["fgBrush"] as Brush,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                }

                storeSeg.Child = storeContent;
                Grid.SetColumn(storeSeg, 0);
                srcGrid.Children.Add(storeSeg);

                // EXE segment
                var exeSeg = new Border
                {
                    Background = (chosenSrc == "exe") ? Application.Current.Resources["accentBrush"] as Brush : Brushes.Transparent,
                    CornerRadius = new CornerRadius(0, 3, 3, 0),
                    Padding = new Thickness(8, 2, 8, 2),
                    Cursor = hasExe ? Cursors.Hand : Cursors.Arrow,
                    IsEnabled = hasExe,
                    Opacity = hasExe ? 1.0 : 0.4
                };
                var exeText = new TextBlock
                {
                    Text = I18n.tr("src_exe"),
                    FontSize = 9.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = (chosenSrc == "exe") ? Brushes.White : Application.Current.Resources["fgBrush"] as Brush,
                    VerticalAlignment = VerticalAlignment.Center
                };
                exeSeg.Child = exeText;
                Grid.SetColumn(exeSeg, 1);
                srcGrid.Children.Add(exeSeg);

                // Toggle click events
                if (hasStore)
                {
                    storeSeg.MouseLeftButtonDown += (s, e) =>
                    {
                        _sourceSelections[app.id] = "store";
                        RefreshLayout();
                    };
                }
                if (hasExe)
                {
                    exeSeg.MouseLeftButtonDown += (s, e) =>
                    {
                        _sourceSelections[app.id] = "exe";
                        RefreshLayout();
                    };
                }

                srcBorder.Child = srcGrid;
                Grid.SetColumn(srcBorder, 3);
                grid.Children.Add(srcBorder);
            }

            // Installed badge indicator
            if (isInstalled)
            {
                var badgeBorder = new Border
                {
                    Background = Brushes.Transparent,
                    BorderBrush = Application.Current.Resources["installedBrush"] as Brush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 3, 8, 3),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(4, 0, 4, 0)
                };
                var badgeText = new TextBlock
                {
                    Text = I18n.tr("installed_label"),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 9.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = Application.Current.Resources["installedBrush"] as Brush
                };
                badgeBorder.Child = badgeText;
                Grid.SetColumn(badgeBorder, 4);
                grid.Children.Add(badgeBorder);
            }
            else if (isNvidiaBlocked)
            {
                var badgeBorder = new Border
                {
                    Background = Brushes.Transparent,
                    BorderBrush = Application.Current.Resources["fg3Brush"] as Brush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 3, 8, 3),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(4, 0, 4, 0)
                };
                var badgeText = new TextBlock
                {
                    Text = I18n.tr("no_nvidia_gpu"),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 9.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = Application.Current.Resources["fg3Brush"] as Brush
                };
                badgeBorder.Child = badgeText;
                Grid.SetColumn(badgeBorder, 4);
                grid.Children.Add(badgeBorder);
            }

            border.Child = grid;
            return border;
        }

        private void UpdateSelectionStateText()
        {
            // Calculate selected count
            int count = 0;
            int totalVisibleUninstalled = 0;
            bool allVisibleUninstalledSelected = true;

            foreach (var app in _allApps)
            {
                bool isInstalled = _installedStatuses.ContainsKey(app.id) && _installedStatuses[app.id].Item1;
                bool isSelected = _selections.ContainsKey(app.id) && _selections[app.id];
                if (isSelected) count++;

                if (MatchesCurrentFilter(app) && !isInstalled)
                {
                    totalVisibleUninstalled++;
                    if (!isSelected) allVisibleUninstalledSelected = false;
                }
            }

            TxtGlobalCounter.Text = count > 0 ? I18n.tr("n_selected", "n", count) : "";

            if (totalVisibleUninstalled > 0)
            {
                BtnGlobalToggleAll.Text = allVisibleUninstalledSelected ? I18n.tr("select_none") : I18n.tr("select_all");
                BtnGlobalToggleAll.Foreground = Application.Current.Resources["accentBrush"] as Brush;
                BtnGlobalToggleAll.Cursor = Cursors.Hand;
            }
            else
            {
                BtnGlobalToggleAll.Text = I18n.tr("select_all");
                BtnGlobalToggleAll.Foreground = Application.Current.Resources["fg3Brush"] as Brush;
                BtnGlobalToggleAll.Cursor = Cursors.Arrow;
            }
        }

        private void BtnGlobalToggleAll_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Determine target toggling state
            bool allSelected = true;
            bool hasUninstalled = false;

            foreach (var app in _allApps)
            {
                bool isInstalled = _installedStatuses.ContainsKey(app.id) && _installedStatuses[app.id].Item1;
                if (MatchesCurrentFilter(app) && !isInstalled)
                {
                    hasUninstalled = true;
                    if (!_selections.ContainsKey(app.id) || !_selections[app.id])
                    {
                        allSelected = false;
                    }
                }
            }

            if (!hasUninstalled) return;

            bool targetState = !allSelected;
            foreach (var app in _allApps)
            {
                bool isInstalled = _installedStatuses.ContainsKey(app.id) && _installedStatuses[app.id].Item1;
                if (MatchesCurrentFilter(app) && !isInstalled)
                {
                    _selections[app.id] = targetState;
                }
            }

            RefreshLayout();
        }

        private void BtnPrefs_Click(object sender, RoutedEventArgs e)
        {
            var prefWin = new PreferencesWindow(Window.GetWindow(this), () =>
            {
                // Live settings changed callback - translate and refresh layouts
                TranslateUI();
                RebuildSidebar();
                RefreshLayout();
            });
            prefWin.ShowDialog();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (_isScanning) return;
            _isScanning = true;
            TxtFooterError.Text = I18n.tr("status_checking");

            var refreshThread = new Thread(() =>
            {
                Detection.ClearCaches();
                var newStatuses = new Dictionary<string, Tuple<bool, string>>();
                foreach (var app in _allApps)
                {
                    var status = Detection.DetectInstallation(app);
                    newStatuses[app.id] = status;
                }

                Dispatcher.Invoke(() =>
                {
                    foreach (var pair in newStatuses)
                    {
                        _installedStatuses[pair.Key] = pair.Value;
                    }
                    TxtFooterError.Text = "";
                    _isScanning = false;
                    RefreshLayout();

                    // Search winget caches/upgrades
                    var wingetThread = new Thread(WingetScanTask) { IsBackground = true };
                    wingetThread.Start();
                });
            }) { IsBackground = true };
            refreshThread.Start();
        }

        private void BtnUpdateAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string script = Installer.GenerateUpdateAllScript();
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = string.Format("-ExecutionPolicy Bypass -File \"{0}\"", script),
                    UseShellExecute = true,
                    Verb = "runas"
                };
                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Window.GetWindow(this), ex.Message, I18n.tr("err_generic"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnInstall_Click(object sender, RoutedEventArgs e)
        {
            var selectedApps = new List<Tuple<AppInfo, string>>();
            foreach (var app in _allApps)
            {
                bool isSelected = _selections.ContainsKey(app.id) && _selections[app.id];
                bool isInstalled = _installedStatuses.ContainsKey(app.id) && _installedStatuses[app.id].Item1;

                bool isNvidiaBlocked = app.id == "nvidia_app" && !Detection.HasNvidiaGpu();
                if (isSelected && !isInstalled && !isNvidiaBlocked)
                {
                    string chosenSource = _sourceSelections.ContainsKey(app.id) ? _sourceSelections[app.id] : "store";
                    selectedApps.Add(Tuple.Create(app, chosenSource));
                }
            }

            if (selectedApps.Count == 0)
            {
                TxtFooterError.Text = I18n.tr("select_app");
                return;
            }

            TxtFooterError.Text = "";

            var installerWin = new InstallerWindow(Window.GetWindow(this), selectedApps, () =>
            {
                // Trigger refresh on finish installation
                BtnRefresh_Click(null, null);
            });
            installerWin.ShowDialog();
        }
    }
}
