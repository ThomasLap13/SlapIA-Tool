using System.Windows;
using System.Windows.Controls;
using SlapIA.App.Services;

namespace InstallPilot
{
    public partial class WizardWindow : Window
    {
        private bool _isInitializing = true;

        public WizardWindow()
        {
            InitializeComponent();
            I18n.SetWindowIcon(this);
            LoadLogo();
            TranslateUI();
            PopulateCombos();
            LoadCurrentValues();
            _isInitializing = false;
        }

        private void LoadLogo()
        {
            try
            {
                var img = I18n.LoadImage("icons/logo.png");
                if (img != null)
                {
                    WizLogoImg.Source = img;
                    WizLogoImg.Visibility = Visibility.Visible;
                    WizLogoFallback.Visibility = Visibility.Collapsed;
                }
            }
            catch { }
        }

        private void TranslateUI()
        {
            Title = "InstallPilot";
            TxtWelcomeTitle.Text = I18n.tr("wiz_welcome_title");
            TxtWelcomeDesc.Text = I18n.tr("wiz_welcome_desc");
            TxtFeature1.Text = I18n.tr("wiz_welcome_f1");
            TxtFeature2.Text = I18n.tr("wiz_welcome_f2");
            TxtFeature3.Text = I18n.tr("wiz_welcome_f3");
            BtnStart.Content = I18n.tr("wiz_btn_start");

            TxtPrefTitle.Text = I18n.tr("wiz_pref_title");
            LblPrefLang.Text = I18n.tr("wiz_pref_lang");
            LblPrefTheme.Text = I18n.tr("wiz_pref_theme");
            LblPrefSource.Text = I18n.tr("wiz_pref_source");
            BtnBack.Content = I18n.tr("wiz_btn_back");
            BtnFinish.Content = I18n.tr("wiz_btn_finish");
        }

        private void PopulateCombos()
        {
            ComboLang.Items.Clear();
            foreach (var name in I18n.LanguageNames.Values)
                ComboLang.Items.Add(name);

            ComboTheme.Items.Clear();
            ComboTheme.Items.Add(I18n.tr("dark_mode"));
            ComboTheme.Items.Add(I18n.tr("light_mode"));

            ComboSource.Items.Clear();
            ComboSource.Items.Add(I18n.tr("wiz_source_store"));
            ComboSource.Items.Add(I18n.tr("wiz_source_exe"));
            ComboSource.Items.Add(I18n.tr("wiz_source_none"));
        }

        private void LoadCurrentValues()
        {
            string langName = I18n.LanguageNames.ContainsKey(I18n.lang_code)
                ? I18n.LanguageNames[I18n.lang_code] : "English";
            ComboLang.SelectedItem = langName;

            ComboTheme.SelectedItem = I18n.theme == "dark" ? I18n.tr("dark_mode") : I18n.tr("light_mode");

            string srcName = I18n.tr("wiz_source_none");
            if (I18n.default_source == "store") srcName = I18n.tr("wiz_source_store");
            else if (I18n.default_source == "exe") srcName = I18n.tr("wiz_source_exe");
            ComboSource.SelectedItem = srcName;
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            PageWelcome.Visibility = Visibility.Collapsed;
            PagePrefs.Visibility = Visibility.Visible;
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            PagePrefs.Visibility = Visibility.Collapsed;
            PageWelcome.Visibility = Visibility.Visible;
        }

        private void BtnFinish_Click(object sender, RoutedEventArgs e)
        {
            I18n.setup_completed = true;
            I18n.SaveSettings();
            DialogResult = true;
            Close();
        }

        private void ComboLang_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            string selected = ComboLang.SelectedItem != null ? ComboLang.SelectedItem.ToString() : null;
            if (selected != null && I18n.LanguageLabels.ContainsKey(selected))
            {
                I18n.SetLanguage(I18n.LanguageLabels[selected]);
                LocalizationService.Instance.SetLanguage(I18n.lang_code);
                I18n.SaveSettings();
                _isInitializing = true;
                TranslateUI();
                PopulateCombos();
                LoadCurrentValues();
                _isInitializing = false;
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
                SlapIA.App.App.ThemeService.SetTheme(newTheme == "dark"
                    ? SlapIA.App.Services.AppTheme.Dark
                    : SlapIA.App.Services.AppTheme.Light);
                I18n.SaveSettings();
            }
        }

        private void ComboSource_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            string selected = ComboSource.SelectedItem != null ? ComboSource.SelectedItem.ToString() : null;
            string newSrc = null;
            if (selected == I18n.tr("wiz_source_store")) newSrc = "store";
            else if (selected == I18n.tr("wiz_source_exe")) newSrc = "exe";
            I18n.default_source = newSrc;
            I18n.SaveSettings();
        }
    }
}
