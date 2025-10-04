using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Newtonsoft.Json.Linq;
using SNIBypassGUI.Common.Extensions;
using SNIBypassGUI.Common.Results;
using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Models;

namespace SNIBypassGUI.Common.Mapper
{
    public class DnsConfigMapper(IMapper<DnsServer> dnsServerMapper) : IMapper<DnsConfig>
    {
        /// <summary>
        /// 将 <see cref="DnsConfig"/> 类型的 <paramref name="dnsConfig"/> 实例转换为 JSON 对象。
        /// </summary>
        public JObject ToJObject(DnsConfig dnsConfig)
        {
            var jObject = new JObject
            {
                ["id"] = dnsConfig.Id.ToString(),
                ["configName"] = dnsConfig.ConfigName.OrDefault(),
                ["isBuiltIn"] = dnsConfig.IsBuiltIn,
                ["interceptIpv6Queries"] = dnsConfig.InterceptIpv6Queries,
                ["forwardPrivateReverseLookups"] = dnsConfig.ForwardPrivateReverseLookups,
                ["positiveResponseCacheTime"] = dnsConfig.PositiveResponseCacheTime.OrDefault(),
                ["negativeResponseCacheTime"] = dnsConfig.NegativeResponseCacheTime.OrDefault(),
                ["failedResponseCacheTime"] = dnsConfig.FailedResponseCacheTime.OrDefault(),
                ["silentCacheUpdateTime"] = dnsConfig.SilentCacheUpdateTime.OrDefault(),
                ["cacheAutoCleanupTime"] = dnsConfig.CacheAutoCleanupTime.OrDefault(),
                ["cacheDomainMatchingRule"] = dnsConfig.CacheDomainMatchingRule.OrDefault(),
                ["limitQueryTypesCache"] = new JArray(dnsConfig.LimitQueryTypesCache.OrEmpty()),
                ["useMemoryCacheOnly"] = dnsConfig.UseMemoryCacheOnly,
                ["disableAddressCache"] = dnsConfig.DisableAddressCache,
                ["localIpv4BindingAddress"] = dnsConfig.LocalIpv4BindingAddress.OrDefault(),
                ["localIpv4BindingPort"] = dnsConfig.LocalIpv4BindingPort.OrDefault(),
                ["localIpv6BindingAddress"] = dnsConfig.LocalIpv6BindingAddress.OrDefault(),
                ["localIpv6BindingPort"] = dnsConfig.LocalIpv6BindingPort.OrDefault(),
                ["generatedResponseTtl"] = dnsConfig.GeneratedResponseTtl.OrDefault(),
                ["udpResponseTimeout"] = dnsConfig.UdpResponseTimeout.OrDefault(),
                ["tcpFirstByteTimeout"] = dnsConfig.TcpFirstByteTimeout.OrDefault(),
                ["tcpInternalTimeout"] = dnsConfig.TcpInternalTimeout.OrDefault(),
                ["socks5ConnectTimeout"] = dnsConfig.Socks5ConnectTimeout.OrDefault(),
                ["socks5ResponseTimeout"] = dnsConfig.Socks5ResponseTimeout.OrDefault(),
                ["socks5FirstByteTimeout"] = dnsConfig.Socks5FirstByteTimeout.OrDefault(),
                ["socks5OtherByteTimeout"] = dnsConfig.Socks5OtherByteTimeout.OrDefault(),
                ["logEvents"] = new JArray(dnsConfig.LogEvents.OrEmpty()),
                ["enableFullLogDump"] = dnsConfig.EnableFullLogDump,
                ["logMemoryBufferSize"] = dnsConfig.LogMemoryBufferSize.OrDefault(),
                ["dnsServers"] = new JArray(dnsConfig.DnsServers?.Select(dnsServerMapper.ToJObject).OrEmpty())
            };

            return jObject;
        }

        /// <summary>
        /// 从 JSON 对象创建一个新的 <see cref="DnsConfig"/> 实例。
        /// </summary>
        public ParseResult<DnsConfig> FromJObject(JObject jObject)
        {
            if (jObject == null)
                return ParseResult<DnsConfig>.Failure("JSON 对象为空。");

            if (!jObject.TryGetGuid("id", out Guid id) ||
                !jObject.TryGetString("configName", out string configName) ||
                !jObject.TryGetBool("isBuiltIn", out bool isBuiltIn) ||
                !jObject.TryGetBool("interceptIpv6Queries", out bool interceptIpv6Queries) ||
                !jObject.TryGetBool("forwardPrivateReverseLookups", out bool forwardPrivateReverseLookups) ||
                !jObject.TryGetString("positiveResponseCacheTime", out string positiveResponseCacheTime) ||
                !jObject.TryGetString("negativeResponseCacheTime", out string negativeResponseCacheTime) ||
                !jObject.TryGetString("failedResponseCacheTime", out string failedResponseCacheTime) ||
                !jObject.TryGetString("silentCacheUpdateTime", out string silentCacheUpdateTime) ||
                !jObject.TryGetString("cacheAutoCleanupTime", out string cacheAutoCleanupTime) ||
                !jObject.TryGetString("cacheDomainMatchingRule", out string cacheDomainMatchingRule) ||
                !jObject.TryGetArray("limitQueryTypesCache", out IReadOnlyList<string> limitQueryTypesCache) ||
                !jObject.TryGetBool("useMemoryCacheOnly", out bool useMemoryCacheOnly) ||
                !jObject.TryGetBool("disableAddressCache", out bool disableAddressCache) ||
                !jObject.TryGetString("localIpv4BindingAddress", out string localIpv4BindingAddress) ||
                !jObject.TryGetString("localIpv4BindingPort", out string localIpv4BindingPort) ||
                !jObject.TryGetString("localIpv6BindingAddress", out string localIpv6BindingAddress) ||
                !jObject.TryGetString("localIpv6BindingPort", out string localIpv6BindingPort) ||
                !jObject.TryGetString("generatedResponseTtl", out string generatedResponseTtl) ||
                !jObject.TryGetString("udpResponseTimeout", out string udpResponseTimeout) ||
                !jObject.TryGetString("tcpFirstByteTimeout", out string tcpFirstByteTimeout) ||
                !jObject.TryGetString("tcpInternalTimeout", out string tcpInternalTimeout) ||
                !jObject.TryGetString("socks5ConnectTimeout", out string socks5ConnectTimeout) ||
                !jObject.TryGetString("socks5ResponseTimeout", out string socks5ResponseTimeout) ||
                !jObject.TryGetString("socks5FirstByteTimeout", out string socks5FirstByteTimeout) ||
                !jObject.TryGetString("socks5OtherByteTimeout", out string socks5OtherByteTimeout) ||
                !jObject.TryGetArray("logEvents", out IReadOnlyList<string> logEvents) ||
                !jObject.TryGetBool("enableFullLogDump", out bool enableFullLogDump) ||
                !jObject.TryGetString("logMemoryBufferSize", out string logMemoryBufferSize) ||
                !jObject.TryGetArray("dnsServers", out IReadOnlyList<JObject> dnsServerObjects))
                return ParseResult<DnsConfig>.Failure("一个或多个通用字段缺失或类型错误。");

            ObservableCollection<DnsServer> dnsServers = [];
            foreach (var item in dnsServerObjects.OfType<JObject>())
            {
                var parsed = dnsServerMapper.FromJObject(item);
                if (!parsed.IsSuccess)
                    return ParseResult<DnsConfig>.Failure($"解析 dnsServers 时出错：{parsed.ErrorMessage}");
                dnsServers.Add(parsed.Value);
            }

            var config = new DnsConfig
            {
                Id = id,
                ConfigName = configName,
                IsBuiltIn = isBuiltIn,
                InterceptIpv6Queries = interceptIpv6Queries,
                ForwardPrivateReverseLookups = forwardPrivateReverseLookups,
                PositiveResponseCacheTime = positiveResponseCacheTime,
                NegativeResponseCacheTime = negativeResponseCacheTime,
                FailedResponseCacheTime = failedResponseCacheTime,
                SilentCacheUpdateTime = silentCacheUpdateTime,
                CacheAutoCleanupTime = cacheAutoCleanupTime,
                CacheDomainMatchingRule = cacheDomainMatchingRule,
                LimitQueryTypesCache = [.. limitQueryTypesCache],
                UseMemoryCacheOnly = useMemoryCacheOnly,
                DisableAddressCache = disableAddressCache,
                LocalIpv4BindingAddress = localIpv4BindingAddress,
                LocalIpv4BindingPort = localIpv4BindingPort,
                LocalIpv6BindingAddress = localIpv6BindingAddress,
                LocalIpv6BindingPort = localIpv6BindingPort,
                GeneratedResponseTtl = generatedResponseTtl,
                UdpResponseTimeout = udpResponseTimeout,
                TcpFirstByteTimeout = tcpFirstByteTimeout,
                TcpInternalTimeout = tcpInternalTimeout,
                Socks5ConnectTimeout = socks5ConnectTimeout,
                Socks5ResponseTimeout = socks5ResponseTimeout,
                Socks5FirstByteTimeout = socks5FirstByteTimeout,
                Socks5OtherByteTimeout = socks5OtherByteTimeout,
                LogEvents = [.. logEvents],
                EnableFullLogDump = enableFullLogDump,
                LogMemoryBufferSize = logMemoryBufferSize,
                DnsServers = dnsServers
            };

            return ParseResult<DnsConfig>.Success(config);
        }
    }
}
