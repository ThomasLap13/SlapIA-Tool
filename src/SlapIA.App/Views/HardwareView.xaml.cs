using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace SlapIA.App.Views;

public partial class HardwareView : UserControl
{
    public HardwareView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Swaps a copy button's icon to a checkmark for ~1s as feedback that the click was
    /// registered and the clipboard was updated - there's otherwise no visible confirmation.
    /// </summary>
    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Content: Path icon } button)
            return;

        var copyGeometry = icon.Data;
        var checkGeometry = (Geometry)FindResource("CheckIconGeometry");
        if (ReferenceEquals(icon.Data, checkGeometry))
            return; // Animation already in progress from a rapid double-click.

        icon.Data = checkGeometry;
        button.IsEnabled = false;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
        timer.Tick += (_, _) =>
        {
            icon.Data = copyGeometry;
            button.IsEnabled = true;
            timer.Stop();
        };
        timer.Start();
    }
}
