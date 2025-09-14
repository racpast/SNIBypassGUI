using System;
using System.Globalization;
using System.Windows.Data;
using SNIBypassGUI.Enums;

namespace SNIBypassGUI.Converters
{
    public class DnsServerProtocolToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DnsServerProtocol protocol)
            {
                return protocol switch
                {
                    DnsServerProtocol.UDP => "UDP",
                    DnsServerProtocol.TCP => "TCP",
                    DnsServerProtocol.DoH => "DoH",
                    DnsServerProtocol.SOCKS5 => "SOCKS5",
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
                    "UDP" => DnsServerProtocol.UDP,
                    "TCP" => DnsServerProtocol.TCP,
                    "DoH" => DnsServerProtocol.DoH,
                    "SOCKS5" => DnsServerProtocol.SOCKS5,
                    _ => DnsServerProtocol.UDP,
                };
            }
            return DnsServerProtocol.UDP;
        }
    }
}