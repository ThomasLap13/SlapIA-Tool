using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SlapIA.App.Converters;

/// <summary>Collapsed when the bound string is null/empty, Visible otherwise.</summary>
public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
