using System;
using System.Globalization;
using System.Windows.Data;
using SNIBypassGUI.Enums;

namespace SNIBypassGUI.Converters
{
    public class IpAddressTypeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IpAddressType type)
            {
                return type switch
                {
                    IpAddressType.IPv4Only => "仅 IPv4",
                    IpAddressType.IPv6Only => "仅 IPv6",
                    IpAddressType.IPv4AndIPv6 => "IPv4 和 IPv6",
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
                    "仅 IPv4" => IpAddressType.IPv4Only,
                    "仅 IPv6" => IpAddressType.IPv6Only,
                    "IPv4 和 IPv6" => IpAddressType.IPv4AndIPv6,
                    _ => IpAddressType.IPv4Only,
                };
            }
            return IpAddressType.IPv4Only;
        }
    }
}
