using System;
using System.Windows;

namespace InstallPilot
{
    public partial class StartupWindow : Window
    {
        public StartupWindow()
        {
            InitializeComponent();
            I18n.SetWindowIcon(this);
            TxtTitle.Text = I18n.tr("scan_popup_title");
        }

        public void UpdateProgress(int current, int total, string name)
        {
            Dispatcher.Invoke(() =>
            {
                string shortName = name.Length <= 26 ? name : name.Substring(0, 23) + "...";
                TxtProgress.Text = I18n.tr("scan_progress", "n", current, "total", total, "name", shortName);
                ProgBar.Value = total > 0 ? (double)current / total : 0;
            });
        }
    }
}
