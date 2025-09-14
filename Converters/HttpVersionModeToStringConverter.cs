using System;
using System.Globalization;
using System.Windows.Data;
using SNIBypassGUI.Enums;

namespace SNIBypassGUI.Converters
{
    public class HttpVersionModeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is HttpVersionMode mode)
            {
                return mode switch
                {
                    HttpVersionMode.Auto => "自动协商",
                    HttpVersionMode.Http2 => "强制 HTTP/2",
                    HttpVersionMode.Http3 => "强制 HTTP/3",
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
                    "自动协商" => HttpVersionMode.Auto,
                    "强制 HTTP/2" => HttpVersionMode.Http2,
                    "强制 HTTP/3" => HttpVersionMode.Http3,
                    _ => HttpVersionMode.Auto,
                };
            }
            return HttpVersionMode.Auto;
        }
    }
}
