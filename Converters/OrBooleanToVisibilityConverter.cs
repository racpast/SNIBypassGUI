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
            Console.WriteLine($"转换器接收到的值: {string.Join(", ", values)}");
            bool result = values.OfType<bool>().Any(b => b);
            Console.WriteLine($"转换结果: {result}");
            return result ? Visibility.Visible : Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}