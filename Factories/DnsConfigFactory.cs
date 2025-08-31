using System;
using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Models;

namespace SNIBypassGUI.Factories
{
    public class DnsConfigFactory : IFactory<DnsConfig>
    {
        /// <summary>
        /// 新建 DNS 配置。
        /// </summary>
        public DnsConfig CreateDefault()
        {
            return new DnsConfig
            {
                Id = Guid.NewGuid(),
                ConfigName = "新 DNS 配置",
                IsBuiltIn = false,
                DnsServers = [],
                InterceptIpv6Queries = false,
                ForwardPrivateReverseLookups = false,
                PositiveResponseCacheTime = "240",
                NegativeResponseCacheTime = "60",
                FailedResponseCacheTime = "0",
                SilentCacheUpdateTime = "60",
                CacheAutoCleanupTime = "720",
                CacheDomainMatchingRule = "^dns.msftncsi.com;^ipv6.msftconnecttest.com;^ipv6.msftncsi.com;^www.msftconnecttest.com;^www.msftncsi.com;*",
                LimitQueryTypesCache = ["A", "AAAA", "CNAME", "HTTPS", "MX", "NS", "PTR", "SOA", "SRV", "TXT"],
                UseMemoryCacheOnly = false,
                DisableAddressCache = false,
                LocalIpv4BindingAddress = "0.0.0.0",
                LocalIpv4BindingPort = "53",
                LocalIpv6BindingAddress = "0:0:0:0:0:0:0:0",
                LocalIpv6BindingPort = "53",
                GeneratedResponseTtl = "300",
                UdpResponseTimeout = "3989",
                TcpFirstByteTimeout = "3989",
                TcpInternalTimeout = "3989",
                Socks5FirstByteTimeout = "3989",
                Socks5OtherByteTimeout = "3989",
                Socks5ConnectTimeout = "3989",
                Socks5ResponseTimeout = "3989",
                LogEvents = ["X", "H", "C", "F"],
                EnableFullLogDump = false,
                LogMemoryBufferSize = "0"
            };
        }
    }
}
