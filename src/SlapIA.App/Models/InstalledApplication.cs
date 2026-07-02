namespace SlapIA.App.Models;

public record InstalledApplication(
    string Name,
    string? Version,
    string? Publisher,
    DateTime? InstallDate,
    string? InstallLocation);
