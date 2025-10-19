using System;
using System.Globalization;
using System.Windows.Data;
using SNIBypassGUI.Enums;

namespace SNIBypassGUI.Converters
{
    public class ResolverProtocolToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ResolverProtocol protocol)
            {
                return protocol switch
                {
                    ResolverProtocol.Plain => "传统 DNS",
                    ResolverProtocol.DnsOverHttps => "DoH",
                    ResolverProtocol.DnsOverTls => "DoT",
                    ResolverProtocol.DnsOverQuic => "DoQ",
                    ResolverProtocol.DnsCrypt => "DNSCrypt",
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
                    "传统 DNS" => ResolverProtocol.Plain,
                    "DoH" => ResolverProtocol.DnsOverHttps,
                    "DoT" => ResolverProtocol.DnsOverTls,
                    "DoQ" => ResolverProtocol.DnsOverQuic,
                    "DNSCrypt" => ResolverProtocol.DnsCrypt,
                    _ => ResolverProtocol.Plain,
                };
            }
            return ResolverProtocol.Plain;
        }
    }
}
