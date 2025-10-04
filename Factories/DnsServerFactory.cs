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
            return new DnsServer
            {
                ServerAddress = string.Empty,
                ServerPort = string.Empty,
                ProtocolType = DnsServerProtocol.UDP,
                DomainMatchingRule = string.Empty,
                IgnoreFailureResponses = false,
                IgnoreNegativeResponses = false
            };
        }
    }
}
