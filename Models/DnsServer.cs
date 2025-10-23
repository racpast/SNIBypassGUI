using System.Collections.ObjectModel;
using System.Linq;
using SNIBypassGUI.Common;
using SNIBypassGUI.Common.Extensions;
using SNIBypassGUI.Common.Network;
using SNIBypassGUI.Enums;

namespace SNIBypassGUI.Models
{
    /// <summary>
    /// 表示一个供 DNS 服务配置使用的 DNS 服务器。
    /// </summary>
    public class DnsServer : NotifyPropertyChangedBase
    {
        #region Fields
        private string _serverAddress;
        private string _serverPort;
        private DnsServerProtocol _protocolType;
        private string _dohHostname;
        private string _dohQueryPath;
        private DohConnectionType _dohConnectionType;
        private bool _dohReuseConnection;
        private bool _dohUseWinHttp;
        private string _socks5ProxyAddress;
        private string _socks5ProxyPort;
        private ObservableCollection<AffinityRule> _domainMatchingRules = [];
        private ObservableCollection<string> _limitQueryTypes = [];
        private bool _ignoreFailureResponses;
        private bool _ignoreNegativeResponses;
        #endregion

        #region Properties
        /// <summary>
        /// 此服务器的 IP 地址，支持 IPv4 或 IPv6。
        /// </summary>
        public string ServerAddress
        {
            get => _serverAddress;
            set
            {
                if (SetProperty(ref _serverAddress, value))
                    OnPropertyChanged(nameof(RequiresIPv6));
            }
        }

        /// <summary>
        /// 此 DNS 服务器的端口号。
        /// </summary>
        public string ServerPort
        {
            get => _serverPort;
            set => SetProperty(ref _serverPort, value);
        }

        /// <summary>
        /// 与此服务器通信使用的协议。
        /// </summary>
        public DnsServerProtocol ProtocolType
        {
            get => _protocolType;
            set
            {
                if (SetProperty(ref _protocolType, value))
                    OnPropertyChanged(nameof(RequiresIPv6));
            }
        }

        /// <summary>
        /// 使用 DoH 协议时，此服务器的主机名。
        /// </summary>
        public string DohHostname
        {
            get => _dohHostname;
            set => SetProperty(ref _dohHostname, value);
        }

        /// <summary>
        /// 使用 DoH 协议时，此服务器的查询路径。
        /// </summary>
        public string DohQueryPath
        {
            get => _dohQueryPath;
            set => SetProperty(ref _dohQueryPath, value);
        }

        /// <summary>
        /// 使用 DoH 协议时的连接类型。
        /// </summary>
        public DohConnectionType DohConnectionType
        {
            get => _dohConnectionType;
            set => SetProperty(ref _dohConnectionType, value);
        }

        /// <summary>
        /// 是否与此服务器重用 TCP 连接以提高性能。
        /// </summary>
        public bool DohReuseConnection
        {
            get => _dohReuseConnection;
            set => SetProperty(ref _dohReuseConnection, value);
        }

        /// <summary>
        /// 是否与此服务器使用 WinHttp 库，否则使用 WinINet。
        /// </summary>
        public bool DohUseWinHttp
        {
            get => _dohUseWinHttp;
            set => SetProperty(ref _dohUseWinHttp, value);
        }

        /// <summary>
        /// 使用 SOCKS5 协议时的代理服务器地址。
        /// </summary>
        public string Socks5ProxyAddress
        {
            get => _socks5ProxyAddress;
            set
            {
                if (SetProperty(ref _socks5ProxyAddress, value))
                    OnPropertyChanged(nameof(RequiresIPv6));
            }
        }

        /// <summary>
        /// 使用 SOCKS5 协议时的代理服务器端口号。
        /// </summary>
        public string Socks5ProxyPort
        {
            get => _socks5ProxyPort;
            set => SetProperty(ref _socks5ProxyPort, value);
        }

        /// <summary>
        /// 此服务器的域名匹配规则，用于决定哪些域名可以使用此服务器。
        /// </summary>
        public ObservableCollection<AffinityRule> DomainMatchingRules
        {
            get => _domainMatchingRules;
            set => SetProperty(ref _domainMatchingRules, value);
        }

        /// <summary>
        /// 此服务器支持的查询类型列表，用于限制此服务器处理的 DNS 查询类型。
        /// </summary>
        public ObservableCollection<string> LimitQueryTypes
        {
            get => _limitQueryTypes;
            set => SetProperty(ref _limitQueryTypes, value);
        }

        /// <summary>
        /// 是否忽略来自此服务器的失败响应。
        /// </summary>
        public bool IgnoreFailureResponses
        {
            get => _ignoreFailureResponses;
            set => SetProperty(ref _ignoreFailureResponses, value);
        }

        /// <summary>
        /// 是否忽略来自此服务器的否定响应。
        /// </summary>
        public bool IgnoreNegativeResponses
        {
            get => _ignoreNegativeResponses;
            set => SetProperty(ref _ignoreNegativeResponses, value);
        }

        /// <summary>
        /// 此服务器是否需要 IPv6 支持。
        /// </summary>
        public bool RequiresIPv6
        {
            get => ProtocolType switch
            {
                DnsServerProtocol.SOCKS5 => NetworkUtils.RequiresPublicIPv6(ServerAddress?.Trim()) || NetworkUtils.RequiresPublicIPv6(Socks5ProxyAddress?.Trim()),
                _ => NetworkUtils.RequiresPublicIPv6(ServerAddress?.Trim())
            };
        }
        #endregion

        #region Methods
        /// <summary>
        /// 创建当前 <see cref="DnsServer"/> 实例的完整副本。
        /// </summary>
        /// <returns>当前对象的一个完整副本。</returns>
        public DnsServer Clone()
        {
            var clone = new DnsServer
            {
                ServerAddress = ServerAddress,
                ServerPort = ServerPort,
                ProtocolType = ProtocolType,
                DohHostname = DohHostname,
                DohQueryPath = DohQueryPath,
                DohConnectionType = DohConnectionType,
                DohReuseConnection = DohReuseConnection,
                DohUseWinHttp = DohUseWinHttp,
                Socks5ProxyAddress = Socks5ProxyAddress,
                Socks5ProxyPort = Socks5ProxyPort,
                DomainMatchingRules = [.. DomainMatchingRules.OrEmpty().Select(rule => rule.Clone())],
                IgnoreFailureResponses = IgnoreFailureResponses,
                IgnoreNegativeResponses = IgnoreNegativeResponses,
                LimitQueryTypes = [.. LimitQueryTypes.OrEmpty()]
            };

            return clone;
        }

        /// <summary>
        /// 使用指定的 <see cref="DnsServer"/> 实例的属性值更新当前实例的内容。
        /// </summary>
        public void UpdateFrom(DnsServer server)
        {
            if (server == null) return;
            ServerAddress = server.ServerAddress;
            ServerPort = server.ServerPort;
            ProtocolType = server.ProtocolType;
            DohHostname = server.DohHostname;
            DohQueryPath = server.DohQueryPath;
            DohConnectionType = server.DohConnectionType;
            DohReuseConnection = server.DohReuseConnection;
            DohUseWinHttp = server.DohUseWinHttp;
            Socks5ProxyAddress = server.Socks5ProxyAddress;
            Socks5ProxyPort = server.Socks5ProxyPort;
            DomainMatchingRules = [.. server.DomainMatchingRules.OrEmpty().Select(h => h.Clone())];
            IgnoreFailureResponses = server.IgnoreFailureResponses;
            IgnoreNegativeResponses = server.IgnoreNegativeResponses;
            LimitQueryTypes = [.. server.LimitQueryTypes.OrEmpty()];
        }
        #endregion
    }
}
