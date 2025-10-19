using System;
using SNIBypassGUI.Enums;
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
            // Configuration.pas，有修改
            return new DnsConfig
            {
                Id = Guid.NewGuid(),
                ConfigName = "新 DNS 配置",
                IsBuiltIn = false,
                SinkholeIPv6Lookups = false,
                ForwardPrivateReverseLookups = false,
                AddressCacheScavengingTime = "360",
                AddressCacheNegativeTime = "60",
                AddressCacheFailureTime = "0",
                AddressCacheSilentUpdateTime = "240",
                AddressCachePeriodicPruningTime = "60",
                CacheDomainMatchingRules = [new() { Pattern = "dns.msftncsi.com",           Mode = AffinityRuleMatchMode.Exclude },
                                            new() { Pattern = "ipv6.msftconnecttest.com",   Mode = AffinityRuleMatchMode.Exclude },
                                            new() { Pattern = "ipv6.msftncsi.com",          Mode = AffinityRuleMatchMode.Exclude },
                                            new() { Pattern = "www.msftconnecttest.com",    Mode = AffinityRuleMatchMode.Exclude },
                                            new() { Pattern = "www.msftncsi.com",           Mode = AffinityRuleMatchMode.Exclude },
                                            new() { Pattern = "*",                          Mode = AffinityRuleMatchMode.Include }],
                LimitQueryTypesCache = ["A", "AAAA", "CNAME", "HTTPS", "MX", "NS", "PTR", "SOA", "SRV", "TXT"],
                AddressCacheInMemoryOnly = false,
                AddressCacheDisabled = false,
                LocalIpv4BindingAddress = "0.0.0.0",
                LocalIpv4BindingPort = "53",
                LocalIpv6BindingAddress = "::",
                LocalIpv6BindingPort = "53",             
                GeneratedResponseTimeToLive = "0",
                ServerUdpProtocolResponseTimeout = "3989",
                ServerTcpProtocolResponseTimeout = "3989",
                ServerTcpProtocolInternalTimeout = "3989",
                ServerSocks5ProtocolProxyFirstByteTimeout = "3989",
                ServerSocks5ProtocolProxyOtherBytesTimeout = "3989",
                ServerSocks5ProtocolProxyRemoteConnectTimeout = "3989",
                ServerSocks5ProtocolProxyRemoteResponseTimeout = "3989",
                EnableHitLog = false,
                LogEvents = ["X", "H", "C", "F"],
                HitLogFullDump = false,
                HitLogMaxPendingHits = "0"
            };
        }
    }
}
