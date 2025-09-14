using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SNIBypassGUI.Enums;
using System.Windows.Data;
using System.Windows;

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
            new NotSupportedException();
    }
}
