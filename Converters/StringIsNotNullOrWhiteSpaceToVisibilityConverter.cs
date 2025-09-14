using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows;

namespace SNIBypassGUI.Converters
{
    public class StringIsNotNullOrWhiteSpaceToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            !string.IsNullOrWhiteSpace(value as string) ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            new NotSupportedException();
    }
}
