using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Miside.Camera.Desktop;

/// <summary>Hides absent telemetry without changing the transport's missing-value contract.</summary>
public sealed class DataVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool present = value is string text && !string.IsNullOrWhiteSpace(text) && text != "—";
        if (parameter is "inverse") present = !present;
        return present ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
