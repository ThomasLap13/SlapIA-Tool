namespace SlapIA.App.ViewModels;

/// <summary>
/// Marker view-model selected by MainWindow's DataTemplate to host the ported InstallPilot tab.
/// InstallPilot keeps its own code-behind-driven state (it isn't MVVM), so this type carries no
/// bindable data - it only exists so the nav system has a distinct CurrentView value to switch on.
/// </summary>
public sealed class InstallPilotHostViewModel
{
}
