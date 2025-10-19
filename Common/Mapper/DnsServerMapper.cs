using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Newtonsoft.Json.Linq;
using SNIBypassGUI.Common.Extensions;
using SNIBypassGUI.Common.Results;
using SNIBypassGUI.Enums;
using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Models;

namespace SNIBypassGUI.Common.Mapper
{
    public class DnsServerMapper(IMapper<AffinityRule> affinityRuleMapper) : IMapper<DnsServer>
    {
        /// <summary>
        /// 将 <see cref="DnsServer"/> 类型的 <paramref name="dnsServer"/> 实例转换为 JSON 对象。
        /// </summary>
        public JObject ToJObject(DnsServer dnsServer)
        {
            var jObject = new JObject
            {
                ["serverAddress"] = dnsServer.ServerAddress.OrDefault(),
                ["serverPort"] = dnsServer.ServerPort.OrDefault(),
                ["protocolType"] = dnsServer.ProtocolType.ToString(),
                ["domainMatchingRules"] = new JArray(dnsServer.DomainMatchingRules?.Select(affinityRuleMapper.ToJObject).OrEmpty()),
                ["limitQueryTypes"] = new JArray(dnsServer.LimitQueryTypes.OrEmpty()),
                ["ignoreFailureResponses"] = dnsServer.IgnoreFailureResponses,
                ["ignoreNegativeResponses"] = dnsServer.IgnoreNegativeResponses
            };

            switch (dnsServer.ProtocolType)
            {
                case DnsServerProtocol.DoH:
                    jObject["dohHostname"] = dnsServer.DohHostname.OrDefault();
                    jObject["dohQueryPath"] = dnsServer.DohQueryPath.OrDefault();
                    jObject["dohConnectionType"] = dnsServer.DohConnectionType.ToString();
                    jObject["dohReuseConnection"] = dnsServer.DohReuseConnection;
                    jObject["dohUseWinHttp"] = dnsServer.DohUseWinHttp;
                    break;
                case DnsServerProtocol.SOCKS5:
                    jObject["socks5ProxyAddress"] = dnsServer.Socks5ProxyAddress.OrDefault();
                    jObject["socks5ProxyPort"] = dnsServer.Socks5ProxyPort.OrDefault();
                    break;
            }

            return jObject;
        }

        /// <summary>
        /// 从 JSON 对象创建一个新的 <see cref="DnsServer"/> 实例。
        /// </summary>
        public ParseResult<DnsServer> FromJObject(JObject jObject)
        {
            if (jObject == null)
                return ParseResult<DnsServer>.Failure("JSON 对象为空。");

            if (!jObject.TryGetString("serverAddress", out string serverAddress) ||
                !jObject.TryGetString("serverPort", out string serverPort) ||
                !jObject.TryGetEnum("protocolType", out DnsServerProtocol protocolType) ||
                !jObject.TryGetBool("ignoreFailureResponses", out bool ignoreFailureResponses) ||
                !jObject.TryGetBool("ignoreNegativeResponses", out bool ignoreNegativeResponses) ||
                !jObject.TryGetArray("domainMatchingRules", out IReadOnlyList<JObject> domainMatchingRuleObjects) ||
                !jObject.TryGetArray("limitQueryTypes", out IReadOnlyList<string> limitQueryTypes))
                return ParseResult<DnsServer>.Failure("一个或多个通用字段缺失或类型错误。");

            ObservableCollection<AffinityRule> domainMatchingRules = [];
            foreach (var item in domainMatchingRuleObjects.OfType<JObject>())
            {
                var parsed = affinityRuleMapper.FromJObject(item);
                if (!parsed.IsSuccess)
                    return ParseResult<DnsServer>.Failure($"解析 domainMatchingRules 时遇到异常：{parsed.ErrorMessage}");
                domainMatchingRules.Add(parsed.Value);
            }

            var server = new DnsServer
            {
                ServerAddress = serverAddress,
                ServerPort = serverPort,
                ProtocolType = protocolType,
                IgnoreFailureResponses = ignoreFailureResponses,
                IgnoreNegativeResponses = ignoreNegativeResponses,
                DomainMatchingRules = domainMatchingRules,
                LimitQueryTypes = [.. limitQueryTypes]
            };

            switch (protocolType)
            {
                case DnsServerProtocol.DoH:
                    if (!jObject.TryGetString("dohHostname", out string dohHostname) ||
                        !jObject.TryGetString("dohQueryPath", out string dohQueryPath) ||
                        !jObject.TryGetEnum("dohConnectionType", out DohConnectionType dohConnectionType) ||
                        !jObject.TryGetBool("dohReuseConnection", out bool dohReuseConnection) ||
                        !jObject.TryGetBool("dohUseWinHttp", out bool dohUseWinHttp))
                        return ParseResult<DnsServer>.Failure("DoH 协议所需的字段缺失或类型错误。");
                    server.DohHostname = dohHostname;
                    server.DohQueryPath = dohQueryPath;
                    server.DohConnectionType = dohConnectionType;
                    server.DohReuseConnection = dohReuseConnection;
                    server.DohUseWinHttp = dohUseWinHttp;
                    break;
                case DnsServerProtocol.SOCKS5:
                    if (!jObject.TryGetString("socks5ProxyAddress", out string socks5ProxyAddress) ||
                        !jObject.TryGetString("socks5ProxyPort", out string socks5ProxyPort))
                        return ParseResult<DnsServer>.Failure("SOCKS5 协议所需的字段缺失或类型错误。");
                    server.Socks5ProxyAddress = socks5ProxyAddress;
                    server.Socks5ProxyPort = socks5ProxyPort;
                    break;
            }

            return ParseResult<DnsServer>.Success(server);
        }
    }
}
