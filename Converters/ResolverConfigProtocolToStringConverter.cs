using System;
using System.Globalization;
using System.Windows.Data;
using SNIBypassGUI.Enums;

namespace SNIBypassGUI.Converters
{
    public class ResolverConfigProtocolToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ResolverConfigProtocol protocol)
            {
                return protocol switch
                {
                    ResolverConfigProtocol.Plain => "传统 DNS",
                    ResolverConfigProtocol.DnsOverHttps => "DoH",
                    ResolverConfigProtocol.DnsOverTls => "DoT",
                    ResolverConfigProtocol.DnsOverQuic => "DoQ",
                    ResolverConfigProtocol.DnsCrypt => "DNSCrypt",
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
                    "传统 DNS" => ResolverConfigProtocol.Plain,
                    "DoH" => ResolverConfigProtocol.DnsOverHttps,
                    "DoT" => ResolverConfigProtocol.DnsOverTls,
                    "DoQ" => ResolverConfigProtocol.DnsOverQuic,
                    "DNSCrypt" => ResolverConfigProtocol.DnsCrypt,
                    _ => ResolverConfigProtocol.Plain,
                };
            }
            return ResolverConfigProtocol.Plain;
        }
    }
}
