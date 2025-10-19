using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Models;
using SNIBypassGUI.Enums;

namespace SNIBypassGUI.Factories
{
    public class DnsServerFactory : IFactory<DnsServer>
    {
        /// <summary>
        /// 新建上游 DNS 服务器。
        /// </summary>
        public DnsServer CreateDefault()
        {
            // Configuration.pas
            return new DnsServer
            {
                ServerAddress = string.Empty,
                ServerPort = "53",
                ProtocolType = DnsServerProtocol.UDP,
                DomainMatchingRules = [],
                Socks5ProxyAddress = "127.0.0.1",
                Socks5ProxyPort = "10808",
                DohHostname = string.Empty,
                DohUseWinHttp = true,
                DohReuseConnection = true,
                LimitQueryTypes = [],
                DohConnectionType = DohConnectionType.SystemProxy,
                DohQueryPath = "dns-query",
                IgnoreFailureResponses = false,
                IgnoreNegativeResponses = false
            };
        }
    }
}
