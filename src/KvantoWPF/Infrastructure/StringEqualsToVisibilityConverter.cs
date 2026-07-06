using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace KvantoWPF.Infrastructure;

[ValueConversion(typeof(string), typeof(Visibility))]
public sealed class StringEqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value?.ToString() == parameter?.ToString() ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
