using System;
using System.Collections.ObjectModel;
using System.Linq;
using MaterialDesignThemes.Wpf;
using SNIBypassGUI.Common;
using SNIBypassGUI.Common.Extensions;
using SNIBypassGUI.Interfaces;

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
        private ObservableCollection<string> _limitQueryTypesCache = [];
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
        private ObservableCollection<string> _logEvents = [];
        private bool _enableFullLogDump;
        private string _logMemoryBufferSize;
        private ObservableCollection<DnsServer> _dnsServers = [];
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
        #endregion
    }
}