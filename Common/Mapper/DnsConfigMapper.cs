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
    public class DnsConfigMapper(IMapper<AffinityRule> affinityRuleMapper, IMapper<DnsServer> dnsServerMapper) : IMapper<DnsConfig>
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
                ["dnsServers"] = new JArray(dnsConfig.DnsServers?.Select(dnsServerMapper.ToJObject).OrEmpty()),
                ["sinkholeIPv6Lookups"] = dnsConfig.SinkholeIPv6Lookups,
                ["forwardPrivateReverseLookups"] = dnsConfig.ForwardPrivateReverseLookups,
                ["addressCacheScavengingTime"] = dnsConfig.AddressCacheScavengingTime.OrDefault(),
                ["addressCacheNegativeTime"] = dnsConfig.AddressCacheNegativeTime.OrDefault(),
                ["addressCacheFailureTime"] = dnsConfig.AddressCacheFailureTime.OrDefault(),
                ["addressCacheSilentUpdateTime"] = dnsConfig.AddressCacheSilentUpdateTime.OrDefault(),
                ["addressCachePeriodicPruningTime"] = dnsConfig.AddressCachePeriodicPruningTime.OrDefault(),
                ["cacheDomainMatchingRules"] = new JArray(dnsConfig.CacheDomainMatchingRules?.Select(affinityRuleMapper.ToJObject).OrEmpty()),
                ["limitQueryTypesCache"] = new JArray(dnsConfig.LimitQueryTypesCache.OrEmpty()),
                ["addressCacheInMemoryOnly"] = dnsConfig.AddressCacheInMemoryOnly,
                ["addressCacheDisabled"] = dnsConfig.AddressCacheDisabled,
                ["localIpv4BindingAddress"] = dnsConfig.LocalIpv4BindingAddress.OrDefault(),
                ["localIpv4BindingPort"] = dnsConfig.LocalIpv4BindingPort.OrDefault(),
                ["localIpv6BindingAddress"] = dnsConfig.LocalIpv6BindingAddress.OrDefault(),
                ["localIpv6BindingPort"] = dnsConfig.LocalIpv6BindingPort.OrDefault(),
                ["generatedResponseTimeToLive"] = dnsConfig.GeneratedResponseTimeToLive.OrDefault(),
                ["serverUdpProtocolResponseTimeout"] = dnsConfig.ServerUdpProtocolResponseTimeout.OrDefault(),
                ["serverTcpProtocolResponseTimeout"] = dnsConfig.ServerTcpProtocolResponseTimeout.OrDefault(),
                ["serverTcpProtocolInternalTimeout"] = dnsConfig.ServerTcpProtocolInternalTimeout.OrDefault(),
                ["serverSocks5ProtocolProxyRemoteConnectTimeout"] = dnsConfig.ServerSocks5ProtocolProxyRemoteConnectTimeout.OrDefault(),
                ["serverSocks5ProtocolProxyRemoteResponseTimeout"] = dnsConfig.ServerSocks5ProtocolProxyRemoteResponseTimeout.OrDefault(),
                ["serverSocks5ProtocolProxyFirstByteTimeout"] = dnsConfig.ServerSocks5ProtocolProxyFirstByteTimeout.OrDefault(),
                ["serverSocks5ProtocolProxyOtherBytesTimeout"] = dnsConfig.ServerSocks5ProtocolProxyOtherBytesTimeout.OrDefault(),
                ["enableHitLog"] = dnsConfig.EnableHitLog,
                ["logEvents"] = new JArray(dnsConfig.LogEvents.OrEmpty()),
                ["hitLogFullDump"] = dnsConfig.HitLogFullDump,
                ["hitLogMaxPendingHits"] = dnsConfig.HitLogMaxPendingHits.OrDefault()
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

            try
            {
                if (!jObject.TryGetGuid("id", out Guid id) ||
                !jObject.TryGetString("configName", out string configName) ||
                !jObject.TryGetBool("isBuiltIn", out bool isBuiltIn) ||
                !jObject.TryGetArray("dnsServers", out IReadOnlyList<JObject> dnsServerObjects) ||
                !jObject.TryGetBool("sinkholeIPv6Lookups", out bool sinkholeIPv6Lookups) ||
                !jObject.TryGetBool("forwardPrivateReverseLookups", out bool forwardPrivateReverseLookups) ||
                !jObject.TryGetString("addressCacheScavengingTime", out string addressCacheScavengingTime) ||
                !jObject.TryGetString("addressCacheNegativeTime", out string addressCacheNegativeTime) ||
                !jObject.TryGetString("addressCacheFailureTime", out string addressCacheFailureTime) ||
                !jObject.TryGetString("addressCacheSilentUpdateTime", out string addressCacheSilentUpdateTime) ||
                !jObject.TryGetString("addressCachePeriodicPruningTime", out string addressCachePeriodicPruningTime) ||
                !jObject.TryGetArray("cacheDomainMatchingRules", out IReadOnlyList<JObject> cacheDomainMatchingRuleObjects) ||
                !jObject.TryGetArray("limitQueryTypesCache", out IReadOnlyList<string> limitQueryTypesCache) ||
                !jObject.TryGetBool("addressCacheInMemoryOnly", out bool addressCacheInMemoryOnly) ||
                !jObject.TryGetBool("addressCacheDisabled", out bool addressCacheDisabled) ||
                !jObject.TryGetString("localIpv4BindingAddress", out string localIpv4BindingAddress) ||
                !jObject.TryGetString("localIpv4BindingPort", out string localIpv4BindingPort) ||
                !jObject.TryGetString("localIpv6BindingAddress", out string localIpv6BindingAddress) ||
                !jObject.TryGetString("localIpv6BindingPort", out string localIpv6BindingPort) ||
                !jObject.TryGetString("generatedResponseTimeToLive", out string generatedResponseTimeToLive) ||
                !jObject.TryGetString("serverUdpProtocolResponseTimeout", out string serverUdpProtocolResponseTimeout) ||
                !jObject.TryGetString("serverTcpProtocolResponseTimeout", out string serverTcpProtocolResponseTimeout) ||
                !jObject.TryGetString("serverTcpProtocolInternalTimeout", out string serverTcpProtocolInternalTimeout) ||
                !jObject.TryGetString("serverSocks5ProtocolProxyRemoteConnectTimeout", out string serverSocks5ProtocolProxyRemoteConnectTimeout) ||
                !jObject.TryGetString("serverSocks5ProtocolProxyRemoteResponseTimeout", out string serverSocks5ProtocolProxyRemoteResponseTimeout) ||
                !jObject.TryGetString("serverSocks5ProtocolProxyFirstByteTimeout", out string serverSocks5ProtocolProxyFirstByteTimeout) ||
                !jObject.TryGetString("serverSocks5ProtocolProxyOtherBytesTimeout", out string serverSocks5ProtocolProxyOtherBytesTimeout) ||
                !jObject.TryGetArray("logEvents", out IReadOnlyList<string> logEvents) ||
                !jObject.TryGetBool("hitLogFullDump", out bool hitLogFullDump) ||
                !jObject.TryGetString("hitLogMaxPendingHits", out string hitLogMaxPendingHits) ||
                !jObject.TryGetBool("enableHitLog", out bool enableHitLog))
                    return ParseResult<DnsConfig>.Failure("一个或多个通用字段缺失或类型错误。");

                ObservableCollection<DnsServer> dnsServers = [];
                foreach (var item in dnsServerObjects.OfType<JObject>())
                {
                    var parsed = dnsServerMapper.FromJObject(item);
                    if (!parsed.IsSuccess)
                        return ParseResult<DnsConfig>.Failure($"解析 dnsServers 时遇到异常：{parsed.ErrorMessage}");
                    dnsServers.Add(parsed.Value);
                }

                ObservableCollection<AffinityRule> cacheDomainMatchingRules = [];
                foreach (var item in cacheDomainMatchingRuleObjects.OfType<JObject>())
                {
                    var parsed = affinityRuleMapper.FromJObject(item);
                    if (!parsed.IsSuccess)
                        return ParseResult<DnsConfig>.Failure($"解析 cacheDomainMatchingRules 时遇到异常：{parsed.ErrorMessage}");
                    cacheDomainMatchingRules.Add(parsed.Value);
                }

                var config = new DnsConfig
                {
                    Id = id,
                    ConfigName = configName,
                    IsBuiltIn = isBuiltIn,
                    DnsServers = dnsServers,
                    SinkholeIPv6Lookups = sinkholeIPv6Lookups,
                    ForwardPrivateReverseLookups = forwardPrivateReverseLookups,
                    AddressCacheScavengingTime = addressCacheScavengingTime,
                    AddressCacheNegativeTime = addressCacheNegativeTime,
                    AddressCacheFailureTime = addressCacheFailureTime,
                    AddressCacheSilentUpdateTime = addressCacheSilentUpdateTime,
                    AddressCachePeriodicPruningTime = addressCachePeriodicPruningTime,
                    CacheDomainMatchingRules = cacheDomainMatchingRules,
                    LimitQueryTypesCache = [.. limitQueryTypesCache],
                    AddressCacheInMemoryOnly = addressCacheInMemoryOnly,
                    AddressCacheDisabled = addressCacheDisabled,
                    LocalIpv4BindingAddress = localIpv4BindingAddress,
                    LocalIpv4BindingPort = localIpv4BindingPort,
                    LocalIpv6BindingAddress = localIpv6BindingAddress,
                    LocalIpv6BindingPort = localIpv6BindingPort,
                    GeneratedResponseTimeToLive = generatedResponseTimeToLive,
                    ServerUdpProtocolResponseTimeout = serverUdpProtocolResponseTimeout,
                    ServerTcpProtocolResponseTimeout = serverTcpProtocolResponseTimeout,
                    ServerTcpProtocolInternalTimeout = serverTcpProtocolInternalTimeout,
                    ServerSocks5ProtocolProxyRemoteConnectTimeout = serverSocks5ProtocolProxyRemoteConnectTimeout,
                    ServerSocks5ProtocolProxyRemoteResponseTimeout = serverSocks5ProtocolProxyRemoteResponseTimeout,
                    ServerSocks5ProtocolProxyFirstByteTimeout = serverSocks5ProtocolProxyFirstByteTimeout,
                    ServerSocks5ProtocolProxyOtherBytesTimeout = serverSocks5ProtocolProxyOtherBytesTimeout,
                    EnableHitLog = enableHitLog,
                    LogEvents = [.. logEvents],
                    HitLogFullDump = hitLogFullDump,
                    HitLogMaxPendingHits = hitLogMaxPendingHits,
                };

                return ParseResult<DnsConfig>.Success(config);

            }
            catch (Exception ex)
            {
                return ParseResult<DnsConfig>.Failure($"解析 DnsConfig 时遇到异常：{ex.Message}");
            }
        }
    }
}
