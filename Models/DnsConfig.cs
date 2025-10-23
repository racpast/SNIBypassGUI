using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
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
        private bool _sinkholeIPv6Lookups = true;
        private bool _forwardPrivateReverseLookups = true;
        private string _addressCacheScavengingTime;
        private string _addressCacheNegativeTime;
        private string _addressCacheFailureTime;
        private string _addressCacheSilentUpdateTime;
        private string _addressCachePeriodicPruningTime;
        private ObservableCollection<AffinityRule> _cacheDomainMatchingRules = [];
        private ObservableCollection<string> _limitQueryTypesCache = [];
        private bool _addressCacheInMemoryOnly;
        private bool _addressCacheDisabled;
        private string _localIpv4BindingAddress;
        private string _localIpv4BindingPort;
        private string _localIpv6BindingAddress;
        private string _localIpv6BindingPort;
        private string _generatedResponseTimeToLive;
        private string _serverUdpProtocolResponseTimeout;
        private string _serverTcpProtocolResponseTimeout;
        private string _serverTcpProtocolInternalTimeout;
        private string _serverSocks5ProtocolProxyRemoteConnectTimeout;
        private string _serverSocks5ProtocolProxyRemoteResponseTimeout;
        private string _serverSocks5ProtocolProxyFirstByteTimeout;
        private string _serverSocks5ProtocolProxyOtherBytesTimeout;
        private bool _enableHitLog;
        private ObservableCollection<string> _logEvents = [];
        private bool _hitLogFullDump;
        private string _hitLogMaxPendingHits;
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
            set => SetProperty(ref _isBuiltIn, value);
        }

        /// <summary>
        /// 此配置的 DNS 服务器列表。
        /// </summary>
        public ObservableCollection<DnsServer> DnsServers
        {
            get => _dnsServers;
            set
            {
                if (_dnsServers != null)
                {
                    _dnsServers.CollectionChanged -= OnDnsServersChanged;
                    foreach (var server in _dnsServers)
                        server.PropertyChanged -= OnDnsServerPropertyChanged;
                }

                if (SetProperty(ref _dnsServers, value))
                {
                    if (_dnsServers != null)
                    {
                        _dnsServers.CollectionChanged += OnDnsServersChanged;
                        foreach (var server in _dnsServers)
                            server.PropertyChanged += OnDnsServerPropertyChanged;
                    }
                    OnPropertyChanged(nameof(RequiresIPv6));
                }
            }
        }

        /// <summary>
        /// 是否拦截 IPv6 查询。
        /// </summary>
        public bool SinkholeIPv6Lookups { get => _sinkholeIPv6Lookups; set => SetProperty(ref _sinkholeIPv6Lookups, value); }

        /// <summary>
        /// 是否转发私有反向查询。
        /// </summary>
        public bool ForwardPrivateReverseLookups { get => _forwardPrivateReverseLookups; set => SetProperty(ref _forwardPrivateReverseLookups, value); }

        /// <summary>
        /// 肯定响应缓存时间。
        /// </summary>
        public string AddressCacheScavengingTime { get => _addressCacheScavengingTime; set => SetProperty(ref _addressCacheScavengingTime, value); }

        /// <summary>
        /// 否定响应缓存时间。
        /// </summary>
        public string AddressCacheNegativeTime { get => _addressCacheNegativeTime; set => SetProperty(ref _addressCacheNegativeTime, value); }

        /// <summary>
        /// 失败响应缓存时间。
        /// </summary>
        public string AddressCacheFailureTime { get => _addressCacheFailureTime; set => SetProperty(ref _addressCacheFailureTime, value); }

        /// <summary>
        /// 缓存静默更新阈值。
        /// </summary>
        public string AddressCacheSilentUpdateTime { get => _addressCacheSilentUpdateTime; set => SetProperty(ref _addressCacheSilentUpdateTime, value); }

        /// <summary>
        /// 缓存定期清理时间。
        /// </summary>
        public string AddressCachePeriodicPruningTime { get => _addressCachePeriodicPruningTime; set => SetProperty(ref _addressCachePeriodicPruningTime, value); }

        /// <summary>
        /// 缓存域名匹配规则。
        /// </summary>
        public ObservableCollection<AffinityRule> CacheDomainMatchingRules { get => _cacheDomainMatchingRules; set => SetProperty(ref _cacheDomainMatchingRules, value); }

        /// <summary>
        /// 缓存查询类型限制列表。
        /// </summary>
        public ObservableCollection<string> LimitQueryTypesCache { get => _limitQueryTypesCache; set => SetProperty(ref _limitQueryTypesCache, value); }

        /// <summary>
        /// 是否仅使用内存缓存，不使用磁盘缓存。
        /// </summary>
        public bool AddressCacheInMemoryOnly { get => _addressCacheInMemoryOnly; set => SetProperty(ref _addressCacheInMemoryOnly, value); }

        /// <summary>
        /// 是否禁用地址缓存。
        /// </summary>
        public bool AddressCacheDisabled { get => _addressCacheDisabled; set => SetProperty(ref _addressCacheDisabled, value); }

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
        public string GeneratedResponseTimeToLive { get => _generatedResponseTimeToLive; set => SetProperty(ref _generatedResponseTimeToLive, value); }

        /// <summary>
        /// UDP 响应超时时间。
        /// </summary>
        public string ServerUdpProtocolResponseTimeout { get => _serverUdpProtocolResponseTimeout; set => SetProperty(ref _serverUdpProtocolResponseTimeout, value); }

        /// <summary>
        /// TCP 首字节超时时间。
        /// </summary>
        public string ServerTcpProtocolResponseTimeout { get => _serverTcpProtocolResponseTimeout; set => SetProperty(ref _serverTcpProtocolResponseTimeout, value); }

        /// <summary>
        /// TCP 内部超时时间。
        /// </summary>
        public string ServerTcpProtocolInternalTimeout { get => _serverTcpProtocolInternalTimeout; set => SetProperty(ref _serverTcpProtocolInternalTimeout, value); }

        /// <summary>
        /// SOCKS5 连接超时时间。
        /// </summary>
        public string ServerSocks5ProtocolProxyRemoteConnectTimeout { get => _serverSocks5ProtocolProxyRemoteConnectTimeout; set => SetProperty(ref _serverSocks5ProtocolProxyRemoteConnectTimeout, value); }

        /// <summary>
        /// SOCKS5 响应超时时间。
        /// </summary>
        public string ServerSocks5ProtocolProxyRemoteResponseTimeout { get => _serverSocks5ProtocolProxyRemoteResponseTimeout; set => SetProperty(ref _serverSocks5ProtocolProxyRemoteResponseTimeout, value); }

        /// <summary>
        /// SOCKS5 首字节超时时间。
        /// </summary>
        public string ServerSocks5ProtocolProxyFirstByteTimeout { get => _serverSocks5ProtocolProxyFirstByteTimeout; set => SetProperty(ref _serverSocks5ProtocolProxyFirstByteTimeout, value); }

        /// <summary>
        /// SOCKS5 其他字节超时时间。
        /// </summary>
        public string ServerSocks5ProtocolProxyOtherBytesTimeout { get => _serverSocks5ProtocolProxyOtherBytesTimeout; set => SetProperty(ref _serverSocks5ProtocolProxyOtherBytesTimeout, value); }

        /// <summary>
        /// 是否启用命中日志。
        /// </summary>
        public bool EnableHitLog { get => _enableHitLog; set => SetProperty(ref _enableHitLog, value); }

        /// <summary>
        /// 日志记录事件列表。
        /// </summary>
        public ObservableCollection<string> LogEvents { get => _logEvents; set => SetProperty(ref _logEvents, value); }

        /// <summary>
        /// 是否启用完整日志转储。
        /// </summary>
        public bool HitLogFullDump { get => _hitLogFullDump; set => SetProperty(ref _hitLogFullDump, value); }

        /// <summary>
        /// 日志内存缓冲区大小。
        /// </summary>
        public string HitLogMaxPendingHits { get => _hitLogMaxPendingHits; set => SetProperty(ref _hitLogMaxPendingHits, value); }

        /// <summary>
        /// 此配置是否需要 IPv6 支持。
        /// </summary>
        public bool RequiresIPv6 { get => DnsServers.Any(server => server.RequiresIPv6); }
        #endregion

        #region Event Handlers
        private void OnDnsServersChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (DnsServer item in e.NewItems)
                    item.PropertyChanged += OnDnsServerPropertyChanged;

            if (e.OldItems != null)
                foreach (DnsServer item in e.OldItems)
                    item.PropertyChanged -= OnDnsServerPropertyChanged;

            OnPropertyChanged(nameof(RequiresIPv6));
        }

        private void OnDnsServerPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DnsServer.RequiresIPv6))
                OnPropertyChanged(nameof(RequiresIPv6));
        }
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
                DnsServers = [.. DnsServers.OrEmpty().Select(server => server.Clone())],
                SinkholeIPv6Lookups = SinkholeIPv6Lookups,
                ForwardPrivateReverseLookups = ForwardPrivateReverseLookups,
                AddressCacheScavengingTime = AddressCacheScavengingTime,
                AddressCacheNegativeTime = AddressCacheNegativeTime,
                AddressCacheFailureTime = AddressCacheFailureTime,
                AddressCacheSilentUpdateTime = AddressCacheSilentUpdateTime,
                AddressCachePeriodicPruningTime = AddressCachePeriodicPruningTime,
                CacheDomainMatchingRules = [.. CacheDomainMatchingRules.OrEmpty().Select(server => server.Clone())],
                AddressCacheInMemoryOnly = AddressCacheInMemoryOnly,
                AddressCacheDisabled = AddressCacheDisabled,
                LocalIpv4BindingAddress = LocalIpv4BindingAddress,
                LocalIpv4BindingPort = LocalIpv4BindingPort,
                LocalIpv6BindingAddress = LocalIpv6BindingAddress,
                LocalIpv6BindingPort = LocalIpv6BindingPort,
                GeneratedResponseTimeToLive = GeneratedResponseTimeToLive,
                ServerUdpProtocolResponseTimeout = ServerUdpProtocolResponseTimeout,
                ServerTcpProtocolResponseTimeout = ServerTcpProtocolResponseTimeout,
                ServerTcpProtocolInternalTimeout = ServerTcpProtocolInternalTimeout,
                ServerSocks5ProtocolProxyRemoteConnectTimeout = ServerSocks5ProtocolProxyRemoteConnectTimeout,
                ServerSocks5ProtocolProxyRemoteResponseTimeout = ServerSocks5ProtocolProxyRemoteResponseTimeout,
                ServerSocks5ProtocolProxyFirstByteTimeout = ServerSocks5ProtocolProxyFirstByteTimeout,
                ServerSocks5ProtocolProxyOtherBytesTimeout = ServerSocks5ProtocolProxyOtherBytesTimeout,
                EnableHitLog = EnableHitLog,
                HitLogFullDump = HitLogFullDump,
                HitLogMaxPendingHits = HitLogMaxPendingHits,
                LimitQueryTypesCache = [.. LimitQueryTypesCache.OrEmpty()],
                LogEvents = [.. LogEvents.OrEmpty()],
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
            DnsServers = [.. source.DnsServers?.Select(w => w.Clone()).OrEmpty()];
            SinkholeIPv6Lookups = source.SinkholeIPv6Lookups;
            ForwardPrivateReverseLookups = source.ForwardPrivateReverseLookups;
            AddressCacheScavengingTime = source.AddressCacheScavengingTime;
            AddressCacheNegativeTime = source.AddressCacheNegativeTime;
            AddressCacheFailureTime = source.AddressCacheFailureTime;
            AddressCacheSilentUpdateTime = source.AddressCacheSilentUpdateTime;
            AddressCachePeriodicPruningTime = source.AddressCachePeriodicPruningTime;
            CacheDomainMatchingRules = [.. source.CacheDomainMatchingRules?.Select(w => w.Clone()).OrEmpty()];
            LimitQueryTypesCache = [.. source.LimitQueryTypesCache.OrEmpty()];
            AddressCacheInMemoryOnly = source.AddressCacheInMemoryOnly;
            AddressCacheDisabled = source.AddressCacheDisabled;
            LocalIpv4BindingAddress = source.LocalIpv4BindingAddress;
            LocalIpv4BindingPort = source.LocalIpv4BindingPort;
            LocalIpv6BindingAddress = source.LocalIpv6BindingAddress;
            LocalIpv6BindingPort = source.LocalIpv6BindingPort;
            GeneratedResponseTimeToLive = source.GeneratedResponseTimeToLive;
            ServerUdpProtocolResponseTimeout = source.ServerUdpProtocolResponseTimeout;
            ServerTcpProtocolResponseTimeout = source.ServerTcpProtocolResponseTimeout;
            ServerTcpProtocolInternalTimeout = source.ServerTcpProtocolInternalTimeout;
            ServerSocks5ProtocolProxyRemoteConnectTimeout = source.ServerSocks5ProtocolProxyRemoteConnectTimeout;
            ServerSocks5ProtocolProxyRemoteResponseTimeout = source.ServerSocks5ProtocolProxyRemoteResponseTimeout;
            ServerSocks5ProtocolProxyFirstByteTimeout = source.ServerSocks5ProtocolProxyFirstByteTimeout;
            ServerSocks5ProtocolProxyOtherBytesTimeout = source.ServerSocks5ProtocolProxyOtherBytesTimeout;
            EnableHitLog = source.EnableHitLog;
            LogEvents = [.. source.LogEvents.OrEmpty()];
            HitLogFullDump = source.HitLogFullDump;
            HitLogMaxPendingHits = source.HitLogMaxPendingHits;
        }
        #endregion
    }
}
