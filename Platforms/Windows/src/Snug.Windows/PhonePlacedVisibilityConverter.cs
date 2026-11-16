using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Snug.Windows.Core;

namespace Snug.Windows;

/// <summary>
/// Equivalent to upstream PhoneAppCard.WindowRow.onPhone: the badge, restore,
/// and zoom controls belong to exactly the window currently on the phone.
/// </summary>
public sealed class PhonePlacedVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not WindowInfo window ||
            values[1] is not WindowKey active || window.Key != active)
            return Visibility.Collapsed;

        if (string.Equals(parameter as string, "zoom", StringComparison.OrdinalIgnoreCase) &&
            !ZoomBook.IsZoomable(window.ExecutablePath, window.AppName))
            return Visibility.Collapsed;

        return Visibility.Visible;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
