using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Windows;

namespace SNIBypassGUI.Converters
{
    public class OrBooleanToVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            bool result = values.OfType<bool>().Any(b => b);
            return result ? Visibility.Visible : Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}