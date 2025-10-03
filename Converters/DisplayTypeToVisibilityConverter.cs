using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using SNIBypassGUI.Enums;

namespace SNIBypassGUI.Converters
{
    public class DisplayTypeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DisplayType displayType && parameter is string expectedType)
            {
                var expectedDisplayType = (DisplayType)Enum.Parse(typeof(DisplayType), expectedType);
                return displayType == expectedDisplayType ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
