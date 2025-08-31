using System;
using SNIBypassGUI.Enums;
using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Models;

namespace SNIBypassGUI.Factories
{
    public class ResolverConfigFactory : IFactory<ResolverConfig>
    {
        /// <summary>
        /// 新建 DNS 解析器配置。
        /// </summary>
        public ResolverConfig CreateDefault()
        {
            return new ResolverConfig
            {
                Id = Guid.NewGuid(),
                ConfigName = "新解析器配置",
                ProtocolType = ResolverConfigProtocol.Plain,
                IsBuiltIn = false,
                ServerAddress = string.Empty,
                QueryTimeout = "6s",
                ClientSubnet = string.Empty,
                UdpBufferSize = "1232",
                ReuseConnection = true,
                BootstrapServer = string.Empty,
                BootstrapTimeout = "4s",
                TlsServerName = string.Empty,
                TlsClientCertPath =string.Empty,
                TlsClientKeyPath = string.Empty,
                TlsMinVersion = 1.0m,
                TlsMaxVersion = 1.3m,
                TlsCipherSuites = [],
                TlsCurvePreferences = [],
                TlsNextProtos = [],
                QuicAlpnTokens = ["doq", "doq-i11"],
                HttpUserAgent = string.Empty,
                HttpHeaders = [],
                HttpMethod = HttpMethodType.GET,
                HttpVersionMode = HttpVersionMode.Auto,
                EnablePmtud = true,
                QuicLengthPrefix = true,
                DnsCookie = string.Empty,
                DnsCryptUdpSize = "0",
                DnsCryptProvider = string.Empty,
                DnsCryptPublicKey = string.Empty
            };
        }
    }
}
