using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SNIBypassGUI.Converters
{
    public class StringIsNullOrWhiteSpaceToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            string.IsNullOrWhiteSpace(value as string) ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            new NotSupportedException();
    }
}
