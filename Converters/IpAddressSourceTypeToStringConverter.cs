using System;
using System.Globalization;
using System.Windows.Data;
using SNIBypassGUI.Enums;

namespace SNIBypassGUI.Converters
{
    class IpAddressSourceTypeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IpAddressSourceType type)
            {
                return type switch
                {
                    IpAddressSourceType.Static => "直接指定",
                    IpAddressSourceType.Dynamic => "解析获取",
                    _ => string.Empty,
                };
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                return str switch
                {
                    "直接指定" => IpAddressSourceType.Static,
                    "解析获取" => IpAddressSourceType.Dynamic,
                    _ => IpAddressSourceType.Static,
                };
            }
            return IpAddressSourceType.Static;
        }
    }
}
