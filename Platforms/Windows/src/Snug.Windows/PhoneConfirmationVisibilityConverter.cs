using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Snug.Windows.Core;

namespace Snug.Windows;

/// <summary>
/// PhoneHomeView.Confirm equivalent: a single inline confirmation lives in
/// the corresponding app card or window row, never in a global modal dialog.
/// </summary>
public sealed class PhoneConfirmationVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] == DependencyProperty.UnsetValue)
            return Visibility.Collapsed;

        var current = values[0] switch
        {
            RunningApp app => "quit:" + app.ProcessId,
            WindowInfo window => "close:" + window.Key.ProcessId + ":" + window.Key.Handle,
            _ => string.Empty
        };
        var selected = values[1] as string ?? string.Empty;
        var isSelected = string.Equals(current, selected, StringComparison.Ordinal);
        var invert = string.Equals(parameter as string, "normal", StringComparison.OrdinalIgnoreCase);
        return (isSelected != invert) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
