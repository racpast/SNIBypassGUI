using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows;

namespace SNIBypassGUI.Converters
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool boolValue = false;
            if (value is bool b)
            {
                boolValue = b;
            }

            // 可以反转逻辑
            // string stringParameter = parameter as string;
            // if (!string.IsNullOrEmpty(stringParameter) && stringParameter.ToLowerInvariant() == "inverse")
            // {
            //     boolValue = !boolValue;
            // }

            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility == Visibility.Visible;
            }
            return false;
        }
    }
}
