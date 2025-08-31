using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MaterialDesignThemes.Wpf;
using Newtonsoft.Json.Linq;
using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Utils.Extensions;
using SNIBypassGUI.Utils.Results;

namespace SNIBypassGUI.Models
{
    /// <summary>
    /// 表示一个 DNS 服务配置。
    /// </summary>
    public class DnsConfig : NotifyPropertyChangedBase, IStorable
    {
        #region Fields
        private Guid _id;
        private string _configName;
        private bool _isBuiltIn;
        private bool _interceptIpv6Queries = true;
        private bool _forwardPrivateReverseLookups = true;
        private string _positiveResponseCacheTime;
        private string _negativeResponseCacheTime;
        private string _failedResponseCacheTime;
        private string _silentCacheUpdateTime;
        private string _cacheAutoCleanupTime;
        private string _cacheDomainMatchingRule;
        private ObservableCollection<string> _limitQueryTypesCache;
        private bool _useMemoryCacheOnly;
        private bool _disableAddressCache;
        private string _localIpv4BindingAddress;
        private string _localIpv4BindingPort;
        private string _localIpv6BindingAddress;
        private string _localIpv6BindingPort;
        private string _generatedResponseTtl;
        private string _udpResponseTimeout;
        private string _tcpFirstByteTimeout;
        private string _tcpInternalTimeout;
        private string _socks5ConnectTimeout;
        private string _socks5ResponseTimeout;
        private string _socks5FirstByteTimeout;
        private string _socks5OtherByteTimeout;
        private ObservableCollection<string> _logEvents;
        private bool _enableFullLogDump;
        private string _logMemoryBufferSize;
        private ObservableCollection<DnsServer> _dnsServers;
        #endregion

        #region Properties
        /// <summary>
        /// 此配置的唯一标识符。
        /// </summary>
        public Guid Id
        {
            get => _id;
            internal set => SetProperty(ref _id, value);
        }

        /// <summary>
        /// 此配置的名称。
        /// </summary>
        public string ConfigName { get => _configName; set => SetProperty(ref _configName, value); }

        /// <summary>
        /// 此配置是否为内置的。
        /// </summary>
        public bool IsBuiltIn
        {
            get => _isBuiltIn;
            set
            {
                if (SetProperty(ref _isBuiltIn, value))
                {
                    OnPropertyChanged(nameof(ListIconKind));
                    OnPropertyChanged(nameof(ListTypeDescription));
                }
            }
        }

        /// <summary>
        /// 此配置的 DNS 服务器列表。
        /// </summary>
        public ObservableCollection<DnsServer> DnsServers { get => _dnsServers; set => SetProperty(ref _dnsServers, value); }

        /// <summary>
        /// 是否拦截 IPv6 查询。
        /// </summary>
        public bool InterceptIpv6Queries { get => _interceptIpv6Queries; set => SetProperty(ref _interceptIpv6Queries, value); }

        /// <summary>
        /// 是否转发私有反向查询。
        /// </summary>
        public bool ForwardPrivateReverseLookups { get => _forwardPrivateReverseLookups; set => SetProperty(ref _forwardPrivateReverseLookups, value); }

        /// <summary>
        /// 肯定响应缓存时间。
        /// </summary>
        public string PositiveResponseCacheTime { get => _positiveResponseCacheTime; set => SetProperty(ref _positiveResponseCacheTime, value); }

        /// <summary>
        /// 否定响应缓存时间。
        /// </summary>
        public string NegativeResponseCacheTime { get => _negativeResponseCacheTime; set => SetProperty(ref _negativeResponseCacheTime, value); }

        /// <summary>
        /// 失败响应缓存时间。
        /// </summary>
        public string FailedResponseCacheTime { get => _failedResponseCacheTime; set => SetProperty(ref _failedResponseCacheTime, value); }

        /// <summary>
        /// 缓存静默更新阈值。
        /// </summary>
        public string SilentCacheUpdateTime { get => _silentCacheUpdateTime; set => SetProperty(ref _silentCacheUpdateTime, value); }

        /// <summary>
        /// 缓存自动清理时间。
        /// </summary>
        public string CacheAutoCleanupTime { get => _cacheAutoCleanupTime; set => SetProperty(ref _cacheAutoCleanupTime, value); }

        /// <summary>
        /// 缓存域名匹配规则。
        /// </summary>
        public string CacheDomainMatchingRule { get => _cacheDomainMatchingRule; set => SetProperty(ref _cacheDomainMatchingRule, value); }

        /// <summary>
        /// 缓存查询类型限制列表。
        /// </summary>
        public ObservableCollection<string> LimitQueryTypesCache { get => _limitQueryTypesCache; set => SetProperty(ref _limitQueryTypesCache, value); }

        /// <summary>
        /// 是否仅使用内存缓存，不使用磁盘缓存。
        /// </summary>
        public bool UseMemoryCacheOnly { get => _useMemoryCacheOnly; set => SetProperty(ref _useMemoryCacheOnly, value); }

        /// <summary>
        /// 是否禁用地址缓存。
        /// </summary>
        public bool DisableAddressCache { get => _disableAddressCache; set => SetProperty(ref _disableAddressCache, value); }

        /// <summary>
        /// 本地 IPv4 绑定地址。
        /// </summary>
        public string LocalIpv4BindingAddress { get => _localIpv4BindingAddress; set => SetProperty(ref _localIpv4BindingAddress, value); }

        /// <summary>
        /// 本地 IPv4 绑定端口。
        /// </summary>
        public string LocalIpv4BindingPort { get => _localIpv4BindingPort; set => SetProperty(ref _localIpv4BindingPort, value); }

        /// <summary>
        /// 本地 IPv6 绑定地址。
        /// </summary>
        public string LocalIpv6BindingAddress { get => _localIpv6BindingAddress; set => SetProperty(ref _localIpv6BindingAddress, value); }

        /// <summary>
        /// 本地 IPv6 绑定端口。
        /// </summary>
        public string LocalIpv6BindingPort { get => _localIpv6BindingPort; set => SetProperty(ref _localIpv6BindingPort, value); }

        /// <summary>
        /// 本地生成响应 TTL。
        /// </summary>
        public string GeneratedResponseTtl { get => _generatedResponseTtl; set => SetProperty(ref _generatedResponseTtl, value); }

        /// <summary>
        /// UDP 响应超时时间。
        /// </summary>
        public string UdpResponseTimeout { get => _udpResponseTimeout; set => SetProperty(ref _udpResponseTimeout, value); }

        /// <summary>
        /// TCP 首字节超时时间。
        /// </summary>
        public string TcpFirstByteTimeout { get => _tcpFirstByteTimeout; set => SetProperty(ref _tcpFirstByteTimeout, value); }

        /// <summary>
        /// TCP 内部超时时间。
        /// </summary>
        public string TcpInternalTimeout { get => _tcpInternalTimeout; set => SetProperty(ref _tcpInternalTimeout, value); }

        /// <summary>
        /// SOCKS5 连接超时时间。
        /// </summary>
        public string Socks5ConnectTimeout { get => _socks5ConnectTimeout; set => SetProperty(ref _socks5ConnectTimeout, value); }

        /// <summary>
        /// SOCKS5 响应超时时间。
        /// </summary>
        public string Socks5ResponseTimeout { get => _socks5ResponseTimeout; set => SetProperty(ref _socks5ResponseTimeout, value); }

        /// <summary>
        /// SOCKS5 首字节超时时间。
        /// </summary>
        public string Socks5FirstByteTimeout { get => _socks5FirstByteTimeout; set => SetProperty(ref _socks5FirstByteTimeout, value); }

        /// <summary>
        /// SOCKS5 其他字节超时时间。
        /// </summary>
        public string Socks5OtherByteTimeout { get => _socks5OtherByteTimeout; set => SetProperty(ref _socks5OtherByteTimeout, value); }

        /// <summary>
        /// 日志记录事件列表。
        /// </summary>
        public ObservableCollection<string> LogEvents { get => _logEvents; set => SetProperty(ref _logEvents, value); }

        /// <summary>
        /// 是否启用完整日志转储。
        /// </summary>
        public bool EnableFullLogDump { get => _enableFullLogDump; set => SetProperty(ref _enableFullLogDump, value); }

        /// <summary>
        /// 日志内存缓冲区大小。
        /// </summary>
        public string LogMemoryBufferSize { get => _logMemoryBufferSize; set => SetProperty(ref _logMemoryBufferSize, value); }

        /// <summary>
        /// 此配置的图标类型，供 UI 使用。
        /// </summary>
        public PackIconKind ListIconKind => IsBuiltIn ? PackIconKind.ArchiveLockOutline : PackIconKind.FileDocumentEditOutline;

        /// <summary>
        /// 此配置的类型描述，供 UI 使用。
        /// </summary>
        public string ListTypeDescription => IsBuiltIn ? "(内置)" : "(用户)";
        #endregion

        #region Methods
        /// <summary>
        /// 创建当前 <see cref="DnsConfig"/> 实例的完整副本。
        /// </summary>
        /// <returns>当前对象的一个完整副本。</returns>
        public DnsConfig Clone()
        {
            var clone = new DnsConfig
            {
                Id = Id,
                ConfigName = ConfigName,
                IsBuiltIn = IsBuiltIn,
                InterceptIpv6Queries = InterceptIpv6Queries,
                ForwardPrivateReverseLookups = ForwardPrivateReverseLookups,
                PositiveResponseCacheTime = PositiveResponseCacheTime,
                NegativeResponseCacheTime = NegativeResponseCacheTime,
                FailedResponseCacheTime = FailedResponseCacheTime,
                SilentCacheUpdateTime = SilentCacheUpdateTime,
                CacheAutoCleanupTime = CacheAutoCleanupTime,
                CacheDomainMatchingRule = CacheDomainMatchingRule,
                UseMemoryCacheOnly = UseMemoryCacheOnly,
                DisableAddressCache = DisableAddressCache,
                LocalIpv4BindingAddress = LocalIpv4BindingAddress,
                LocalIpv4BindingPort = LocalIpv4BindingPort,
                LocalIpv6BindingAddress = LocalIpv6BindingAddress,
                LocalIpv6BindingPort = LocalIpv6BindingPort,
                GeneratedResponseTtl = GeneratedResponseTtl,
                UdpResponseTimeout = UdpResponseTimeout,
                TcpFirstByteTimeout = TcpFirstByteTimeout,
                TcpInternalTimeout = TcpInternalTimeout,
                Socks5ConnectTimeout = Socks5ConnectTimeout,
                Socks5ResponseTimeout = Socks5ResponseTimeout,
                Socks5FirstByteTimeout = Socks5FirstByteTimeout,
                Socks5OtherByteTimeout = Socks5OtherByteTimeout,
                EnableFullLogDump = EnableFullLogDump,
                LogMemoryBufferSize = LogMemoryBufferSize,
                LimitQueryTypesCache = [.. LimitQueryTypesCache.OrEmpty()],
                LogEvents = [.. LogEvents.OrEmpty()],
                DnsServers = [.. DnsServers.OrEmpty().Select(server => server.Clone())]
            };

            return clone;
        }

        /// <summary>
        /// 使用指定的 <see cref="DnsConfig"/> 实例的属性值更新当前实例的内容。
        /// </summary>
        public void UpdateFrom(DnsConfig source)
        {
            if (source == null) return;
            ConfigName = source.ConfigName;
            IsBuiltIn = source.IsBuiltIn;
            InterceptIpv6Queries = source.InterceptIpv6Queries;
            ForwardPrivateReverseLookups = source.ForwardPrivateReverseLookups;
            PositiveResponseCacheTime = source.PositiveResponseCacheTime;
            NegativeResponseCacheTime = source.NegativeResponseCacheTime;
            FailedResponseCacheTime = source.FailedResponseCacheTime;
            SilentCacheUpdateTime = source.SilentCacheUpdateTime;
            CacheAutoCleanupTime = source.CacheAutoCleanupTime;
            CacheDomainMatchingRule = source.CacheDomainMatchingRule;
            LimitQueryTypesCache = [.. source.LimitQueryTypesCache.OrEmpty()];
            UseMemoryCacheOnly = source.UseMemoryCacheOnly;
            DisableAddressCache = source.DisableAddressCache;
            LocalIpv4BindingAddress = source.LocalIpv4BindingAddress;
            LocalIpv4BindingPort = source.LocalIpv4BindingPort;
            LocalIpv6BindingAddress = source.LocalIpv6BindingAddress;
            LocalIpv6BindingPort = source.LocalIpv6BindingPort;
            GeneratedResponseTtl = source.GeneratedResponseTtl;
            UdpResponseTimeout = source.UdpResponseTimeout;
            TcpFirstByteTimeout = source.TcpFirstByteTimeout;
            TcpInternalTimeout = source.TcpInternalTimeout;
            Socks5ConnectTimeout = source.Socks5ConnectTimeout;
            Socks5ResponseTimeout = source.Socks5ResponseTimeout;
            Socks5FirstByteTimeout = source.Socks5FirstByteTimeout;
            Socks5OtherByteTimeout = source.Socks5OtherByteTimeout;
            LogEvents = [.. source.LogEvents.OrEmpty()];
            EnableFullLogDump = source.EnableFullLogDump;
            LogMemoryBufferSize = source.LogMemoryBufferSize;
            DnsServers = [.. source.DnsServers?.Select(w => w.Clone()).OrEmpty()];
        }

        /// <summary>
        /// 将当前 <see cref="DnsConfig"/> 实例转换为 JSON 对象。
        /// </summary>
        public JObject ToJObject()
        {
            var jObject = new JObject
            {
                ["id"] = Id.ToString(),
                ["configName"] = ConfigName.OrDefault(),
                ["isBuiltIn"] = IsBuiltIn,
                ["interceptIpv6Queries"] = InterceptIpv6Queries,
                ["forwardPrivateReverseLookups"] = ForwardPrivateReverseLookups,
                ["positiveResponseCacheTime"] = PositiveResponseCacheTime.OrDefault(),
                ["negativeResponseCacheTime"] = NegativeResponseCacheTime.OrDefault(),
                ["failedResponseCacheTime"] = FailedResponseCacheTime.OrDefault(),
                ["silentCacheUpdateTime"] = SilentCacheUpdateTime.OrDefault(),
                ["cacheAutoCleanupTime"] = CacheAutoCleanupTime.OrDefault(),
                ["cacheDomainMatchingRule"] = CacheDomainMatchingRule.OrDefault(),
                ["limitQueryTypesCache"] = new JArray(LimitQueryTypesCache.OrEmpty()),
                ["useMemoryCacheOnly"] = UseMemoryCacheOnly,
                ["disableAddressCache"] = DisableAddressCache,
                ["localIpv4BindingAddress"] = LocalIpv4BindingAddress.OrDefault(),
                ["localIpv4BindingPort"] = LocalIpv4BindingPort.OrDefault(),
                ["localIpv6BindingAddress"] = LocalIpv6BindingAddress.OrDefault(),
                ["localIpv6BindingPort"] = LocalIpv6BindingPort.OrDefault(),
                ["generatedResponseTtl"] = GeneratedResponseTtl.OrDefault(),
                ["udpResponseTimeout"] = UdpResponseTimeout.OrDefault(),
                ["tcpFirstByteTimeout"] = TcpFirstByteTimeout.OrDefault(),
                ["tcpInternalTimeout"] = TcpInternalTimeout.OrDefault(),
                ["socks5ConnectTimeout"] = Socks5ConnectTimeout.OrDefault(),
                ["socks5ResponseTimeout"] = Socks5ResponseTimeout.OrDefault(),
                ["socks5FirstByteTimeout"] = Socks5FirstByteTimeout.OrDefault(),
                ["socks5OtherByteTimeout"] = Socks5OtherByteTimeout.OrDefault(),
                ["logEvents"] = new JArray(LogEvents.OrEmpty()),
                ["enableFullLogDump"] = EnableFullLogDump,
                ["logMemoryBufferSize"] = LogMemoryBufferSize.OrDefault(),
                ["dnsServers"] = new JArray(DnsServers?.Select(s => s.ToJObject()).OrEmpty())
            };

            return jObject;
        }

        /// <summary>
        /// 从 JSON 对象创建一个新的 <see cref="DnsConfig"/> 实例。
        /// </summary>
        public static ParseResult<DnsConfig> FromJObject(JObject jObject)
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
                var parsed = DnsServer.FromJObject(item);
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
        #endregion
    }
}