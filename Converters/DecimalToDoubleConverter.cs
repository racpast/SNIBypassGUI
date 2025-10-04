using System;
using System.Globalization;
using System.Windows.Data;

namespace SNIBypassGUI.Converters
{
    public class DecimalToDoubleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal decValue)
                return (double)decValue;
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double dblValue)
                return (decimal)dblValue;
            return 0m;
        }
    }
}
