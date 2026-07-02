using System.Globalization;
using System.Windows.Data;

namespace SlapIA.App.Converters;

/// <summary>True when the bound nav key matches the converter parameter; drives RadioButton.IsChecked for nav highlighting.</summary>
public class NavSelectionConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.Equals(value as string, parameter as string, StringComparison.OrdinalIgnoreCase);

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
