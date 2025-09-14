using System;
using System.Globalization;
using System.Windows.Data;
using SNIBypassGUI.Enums;


namespace SNIBypassGUI.Converters
{
    class DohConnectionTypeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DohConnectionType protocol)
            {
                return protocol switch
                {
                    DohConnectionType.SystemProxy => "系统代理",
                    DohConnectionType.DirectConnection => "直接连接",
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
                    "系统代理" => DohConnectionType.SystemProxy,
                    "直接连接" => DohConnectionType.DirectConnection,
                    _ => DohConnectionType.SystemProxy,
                };
            }
            return DohConnectionType.SystemProxy;
        }
    }
}
