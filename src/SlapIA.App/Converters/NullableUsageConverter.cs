using System.Globalization;
using System.Windows.Data;

namespace SlapIA.App.Converters;

/// <summary>Formats a nullable percentage as "42 %" or "N/A" when the source (e.g. GPU) is unavailable.</summary>
public class NullableUsageConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is float f ? $"{f:0} %" : "N/A";

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
