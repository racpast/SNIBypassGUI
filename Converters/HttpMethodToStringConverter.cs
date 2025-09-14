using System;
using System.Globalization;
using System.Windows.Data;
using SNIBypassGUI.Enums;

namespace SNIBypassGUI.Converters
{
    public class HttpMethodToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is HttpMethodType method)
            {
                return method switch
                {
                    HttpMethodType.GET => "GET",
                    HttpMethodType.POST => "POST",
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
                    "GET" => HttpMethodType.GET,
                    "POST" => HttpMethodType.POST,
                    _ => HttpMethodType.GET,
                };
            }
            return HttpMethodType.GET;
        }
    }
}
