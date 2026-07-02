using System.Globalization;
using System.Windows.Data;
using SlapIA.App.Models;

namespace SlapIA.App.Converters;

/// <summary>Formats a single GraphicsCardInfo row into the same text produced for the "copy all" button.</summary>
public class GraphicsCardCopyTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is GraphicsCardInfo gpu
            ? $"{gpu.Name}{(gpu.VramGB is { } v ? $" - {v.ToString("0.#", culture)} Go VRAM" : "")}"
            : string.Empty;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Formats a single NetworkAdapterInfo row into the same text produced for the "copy all" button.</summary>
public class NetworkAdapterCopyTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is NetworkAdapterInfo n
            ? $"{n.Name} - {n.IPv4Address ?? "-"} - {n.MacAddress ?? "-"}"
            : string.Empty;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Formats a single DiskInfo row into copyable text.</summary>
public class DiskCopyTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DiskInfo d
            ? $"{d.Model} - {d.SizeGB.ToString("0.#", culture)} Go - {d.MediaType} - {d.InterfaceType}"
            : string.Empty;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
