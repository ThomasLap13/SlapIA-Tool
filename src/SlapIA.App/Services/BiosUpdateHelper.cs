namespace SlapIA.App.Services;

public enum BiosVendor { Asus, Msi, Gigabyte, ASRock, Dell, Hp, Lenovo, Unknown }

/// <summary>
/// There is no universal API to check whether a newer BIOS exists for a given motherboard -
/// every vendor has its own support site with no public feed, and version strings aren't even
/// comparable across brands. Rather than guess/scrape vendor-internal pages (fragile, liable to
/// break silently and mislead the user), this just identifies the vendor from the WMI
/// manufacturer string and builds a search-engine link scoped to their official domain, plus
/// generic (accurate, vendor-wide, not model-specific) flashing instructions.
/// </summary>
public static class BiosUpdateHelper
{
    public static (BiosVendor Vendor, string DisplayName, string Domain) DetectVendor(string? manufacturer)
    {
        var m = manufacturer?.ToLowerInvariant() ?? "";
        if (m.Contains("asus")) return (BiosVendor.Asus, "ASUS", "asus.com");
        if (m.Contains("msi") || m.Contains("micro-star")) return (BiosVendor.Msi, "MSI", "msi.com");
        if (m.Contains("gigabyte")) return (BiosVendor.Gigabyte, "Gigabyte", "gigabyte.com");
        if (m.Contains("asrock")) return (BiosVendor.ASRock, "ASRock", "asrock.com");
        if (m.Contains("dell")) return (BiosVendor.Dell, "Dell", "dell.com");
        if (m.Contains("hewlett") || m.Contains("hp")) return (BiosVendor.Hp, "HP", "hp.com");
        if (m.Contains("lenovo")) return (BiosVendor.Lenovo, "Lenovo", "lenovo.com");
        return (BiosVendor.Unknown, string.IsNullOrWhiteSpace(manufacturer) ? "-" : manufacturer.Trim(), "");
    }

    /// <summary>DuckDuckGo has a stable, simple query URL format and needs no API key - this
    /// scopes the search to the vendor's official domain when known, so results point at their
    /// real support/download page for this exact model instead of third-party mirrors.</summary>
    public static string BuildSearchUrl(string domain, string manufacturer, string? model)
    {
        var query = string.IsNullOrEmpty(domain)
            ? $"{manufacturer} {model} bios update download"
            : $"site:{domain} {model} bios update download";
        return "https://duckduckgo.com/?q=" + Uri.EscapeDataString(query);
    }

    public static string InstructionsKey(BiosVendor vendor) => vendor switch
    {
        BiosVendor.Asus => "Bios_Instructions_Asus",
        BiosVendor.Msi => "Bios_Instructions_Msi",
        BiosVendor.Gigabyte => "Bios_Instructions_Gigabyte",
        BiosVendor.ASRock => "Bios_Instructions_ASRock",
        BiosVendor.Dell => "Bios_Instructions_Dell",
        BiosVendor.Hp => "Bios_Instructions_Hp",
        BiosVendor.Lenovo => "Bios_Instructions_Lenovo",
        _ => "Bios_Instructions_Generic",
    };
}
