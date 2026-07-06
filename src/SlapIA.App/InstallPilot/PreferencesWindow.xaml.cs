using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using SlapIA.App.Services;

namespace InstallPilot
{
    public partial class PreferencesWindow : Window
    {
        private readonly Action _onChangedCallback;
        private bool _isInitializing = true;

        public PreferencesWindow(Window owner, Action onChangedCallback)
        {
            InitializeComponent();
            I18n.SetWindowIcon(this);
            Owner = owner;
            _onChangedCallback = onChangedCallback;

            TranslateUI();
            PopulateCombos();
            LoadCurrentValues();

            _isInitializing = false;
        }

        private void TranslateUI()
        {
            Title = I18n.tr("preferences");
            TxtHeader.Text = I18n.tr("wiz_pref_title");
            LblLang.Text = I18n.tr("wiz_pref_lang");
            LblTheme.Text = I18n.tr("wiz_pref_theme");
            LblSource.Text = I18n.tr("wiz_pref_source");
            BtnClose.Content = I18n.tr("inst_close");
        }

        private void PopulateCombos()
        {
            // Language
            ComboLang.Items.Clear();
            foreach (var name in I18n.LanguageNames.Values)
            {
                ComboLang.Items.Add(name);
            }

            // Theme
            ComboTheme.Items.Clear();
            ComboTheme.Items.Add(I18n.tr("dark_mode"));
            ComboTheme.Items.Add(I18n.tr("light_mode"));

            // Preferred Source
            ComboSource.Items.Clear();
            ComboSource.Items.Add(I18n.tr("wiz_source_store"));
            ComboSource.Items.Add(I18n.tr("wiz_source_exe"));
            ComboSource.Items.Add(I18n.tr("wiz_source_none"));
        }

        private void LoadCurrentValues()
        {
            // Language selection
            string currentLangName = I18n.LanguageNames.ContainsKey(I18n.lang_code) 
                ? I18n.LanguageNames[I18n.lang_code] 
                : "English";
            ComboLang.SelectedItem = currentLangName;

            // Theme selection
            string currentThemeName = I18n.theme == "dark" 
                ? I18n.tr("dark_mode") 
                : I18n.tr("light_mode");
            ComboTheme.SelectedItem = currentThemeName;

            // Preferred source
            string currentSrcName = I18n.tr("wiz_source_none");
            if (I18n.default_source == "store")
                currentSrcName = I18n.tr("wiz_source_store");
            else if (I18n.default_source == "exe")
                currentSrcName = I18n.tr("wiz_source_exe");
            ComboSource.SelectedItem = currentSrcName;
        }

        private void ComboLang_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;

            string selectedName = ComboLang.SelectedItem != null ? ComboLang.SelectedItem.ToString() : null;
            if (selectedName != null && I18n.LanguageLabels.ContainsKey(selectedName))
            {
                string newCode = I18n.LanguageLabels[selectedName];
                if (newCode != I18n.lang_code)
                {
                    I18n.SetLanguage(newCode);
                    // Best-effort: SlapIA's own pages only translate fr/en, so ru/zh only
                    // affect InstallPilot's own texts (handled by I18n.SetLanguage above).
                    LocalizationService.Instance.SetLanguage(newCode);
                    SaveAndNotify();
                }
            }
        }

        private void ComboTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;

            string selected = ComboTheme.SelectedItem != null ? ComboTheme.SelectedItem.ToString() : null;
            string newTheme = selected == I18n.tr("light_mode") ? "light" : "dark";
            if (newTheme != I18n.theme)
            {
                I18n.SetTheme(newTheme);
                // Drives SlapIA's own Light/Dark resource dictionaries too, so the whole app
                // (not just the InstallPilot tab) follows this single theme control.
                SlapIA.App.App.ThemeService.SetTheme(newTheme == "dark"
                    ? SlapIA.App.Services.AppTheme.Dark
                    : SlapIA.App.Services.AppTheme.Light);
                SaveAndNotify();
            }
        }

        private void ComboSource_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;

            string selected = ComboSource.SelectedItem != null ? ComboSource.SelectedItem.ToString() : null;
            string newSrc = null;
            if (selected == I18n.tr("wiz_source_store"))
                newSrc = "store";
            else if (selected == I18n.tr("wiz_source_exe"))
                newSrc = "exe";

            if (newSrc != I18n.default_source)
            {
                I18n.default_source = newSrc;
                SaveAndNotify();
            }
        }

        private void SaveAndNotify()
        {
            I18n.SaveSettings();
            TranslateUI();
            
            // Re-populate and select proper localized titles for dropdown elements
            _isInitializing = true;
            PopulateCombos();
            LoadCurrentValues();
            _isInitializing = false;

            if (_onChangedCallback != null)
            {
                _onChangedCallback();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
