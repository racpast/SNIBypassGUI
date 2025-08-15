using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using MaterialDesignThemes.Wpf;
using Newtonsoft.Json.Linq;
using SNIBypassGUI.Enums;
using SNIBypassGUI.Utils.Network;
using SNIBypassGUI.Utils.Extensions;
using SNIBypassGUI.Utils.Results;

namespace SNIBypassGUI.Models
{
    /// <summary>
    /// 表示一个供 DNS 服务配置使用的 DNS 服务器配置。
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
        private string _domainMatchingRule;
        private ObservableCollection<string> _limitQueryTypes;
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
                {
                    OnPropertyChanged(nameof(PrimaryDisplayText));
                    OnPropertyChanged(nameof(SecondaryDisplayText));
                }
            }
        }

        /// <summary>
        /// 此 DNS 服务器的端口号。
        /// </summary>
        public string ServerPort
        {
            get => _serverPort;
            set
            {
                if (SetProperty(ref _serverPort, value))
                {
                    OnPropertyChanged(nameof(PrimaryDisplayText));
                    OnPropertyChanged(nameof(SecondaryDisplayText));
                }
            }
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
                {
                    OnPropertyChanged(nameof(ListIconKind));
                    OnPropertyChanged(nameof(PrimaryDisplayText));
                    OnPropertyChanged(nameof(SecondaryDisplayText));
                }

            }
        }

        /// <summary>
        /// 使用 DoH 协议时，此服务器的主机名。
        /// </summary>
        public string DohHostname
        {
            get => _dohHostname;
            set
            {
                if (SetProperty(ref _dohHostname, value))
                {
                    OnPropertyChanged(nameof(PrimaryDisplayText));
                    OnPropertyChanged(nameof(SecondaryDisplayText));
                }
            }
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
                {
                    OnPropertyChanged(nameof(PrimaryDisplayText));
                    OnPropertyChanged(nameof(SecondaryDisplayText));
                }
            }
        }

        /// <summary>
        /// 使用 SOCKS5 协议时的代理服务器端口号。
        /// </summary>
        public string Socks5ProxyPort
        {
            get => _socks5ProxyPort;
            set
            {
                if (SetProperty(ref _socks5ProxyPort, value))
                {
                    OnPropertyChanged(nameof(PrimaryDisplayText));
                    OnPropertyChanged(nameof(SecondaryDisplayText));
                }
            }
        }

        /// <summary>
        /// 此服务器的域名匹配规则，用于决定哪些域名可以使用此服务器。
        /// </summary>
        public string DomainMatchingRule
        {
            get => _domainMatchingRule;
            set => SetProperty(ref _domainMatchingRule, value);
        }

        /// <summary>
        /// 此服务器支持的查询类型列表，用于限制此服务器处理的 DNS 查询类型。
        /// </summary>
        public ObservableCollection<string> LimitQueryTypes
        {
            get => _limitQueryTypes;
            set
            {
                if (_limitQueryTypes != null)
                    _limitQueryTypes.CollectionChanged -= OnLimitQueryTypesChanged;

                if (SetProperty(ref _limitQueryTypes, value))
                {
                    if (_limitQueryTypes != null)
                        _limitQueryTypes.CollectionChanged += OnLimitQueryTypesChanged;
                    OnLimitQueryTypesChanged(this, null);
                }
            }
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
        /// 此服务器的图标类型，用于列表显示。
        /// </summary>
        public PackIconKind ListIconKind
        {
            get => ProtocolType switch
            {
                DnsServerProtocol.UDP => PackIconKind.Server,
                DnsServerProtocol.TCP => PackIconKind.Server,
                DnsServerProtocol.SOCKS5 => PackIconKind.ServerNetwork,
                DnsServerProtocol.DoH => PackIconKind.ServerSecurity,
                _ => PackIconKind.HelpCircleOutline
            };
        }

        /// <summary>
        /// 此服务器在列表中的主要展示文本。
        /// </summary>
        public object PrimaryDisplayText
        {
            get =>
                ProtocolType switch
                {
                    DnsServerProtocol.DoH => $"{DohHostname.OrDefault("未指定")}",
                    _ => $"{(NetworkUtils.IsValidIPv6(ServerAddress) ?
                        $"[{ServerAddress}]" :
                        ServerAddress.OrDefault("未指定"))}:{ServerPort.OrDefault("未指定")}"
                };
        }

        /// <summary>
        /// 此服务器在列表中的次要展示文本。
        /// </summary>
        public object SecondaryDisplayText
        {
            get =>
                ProtocolType switch
                {
                    DnsServerProtocol.DoH => $"{(NetworkUtils.IsValidIPv6(ServerAddress) ?
                        $"[{ServerAddress}]" :
                        ServerAddress.OrDefault("未指定"))}:{ServerPort.OrDefault("未指定")}",
                    DnsServerProtocol.SOCKS5 => $"{(NetworkUtils.IsValidIPv6(Socks5ProxyAddress) ?
                        $"[{Socks5ProxyAddress}]" :
                        Socks5ProxyAddress.OrDefault("未指定"))}:{Socks5ProxyPort.OrDefault("未指定")}",
                    _ => null
                };
        }

        /// <summary>
        /// 此服务器在列表中的查询类型限制展示文本。
        /// </summary>
        public string LimitQueryTypesDisplayText =>
            LimitQueryTypes.Any()
                ? $"{string.Join(" ", LimitQueryTypes.OrderBy(type => type))}"
                : string.Empty;

        /// <summary>
        /// 指示此服务器是否存在查询类型限制。
        /// </summary>
        public bool HasQueryTypeRestrictions =>
            LimitQueryTypes.Any();
        #endregion

        #region Methods
        /// <summary>
        /// 当 <see cref="LimitQueryTypes"/> 集合发生更改时调用，
        /// 用于通知 UI 更新显示的查询类型限制相关属性。
        /// </summary>
        private void OnLimitQueryTypesChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(LimitQueryTypesDisplayText));
            OnPropertyChanged(nameof(HasQueryTypeRestrictions));
        }

        /// <summary>
        /// 创建当前 <see cref="DnsServer"/> 实例的完整副本。
        /// </summary>
        /// <returns>当前对象的一个完整副本。</returns>
        public DnsServer Clone()
        {
            var clone = (DnsServer)MemberwiseClone();
            clone.LimitQueryTypes = [.. LimitQueryTypes.OrEmpty()];
            return clone;
        }

        /// <summary>
        /// 将当前 <see cref="DnsServer"/> 实例转换为 JSON 对象。
        /// </summary>
        public JObject ToJObject()
        {
            var jObject = new JObject
            {
                ["serverAddress"] = ServerAddress.OrDefault(),
                ["serverPort"] = ServerPort.OrDefault(),
                ["protocolType"] = ProtocolType.ToString(),
                ["domainMatchingRule"] = DomainMatchingRule.OrDefault(),
                ["limitQueryTypes"] = new JArray(LimitQueryTypes.OrEmpty()),
                ["ignoreFailureResponses"] = IgnoreFailureResponses,
                ["ignoreNegativeResponses"] = IgnoreNegativeResponses
            };

            switch (ProtocolType)
            {
                case DnsServerProtocol.DoH:
                    jObject["dohHostname"] = DohHostname.OrDefault();
                    jObject["dohQueryPath"] = DohQueryPath.OrDefault();
                    jObject["dohConnectionType"] = DohConnectionType.ToString();
                    jObject["dohReuseConnection"] = DohReuseConnection;
                    jObject["dohUseWinHttp"] = DohUseWinHttp;
                    break;
                case DnsServerProtocol.SOCKS5:
                    jObject["socks5ProxyAddress"] = Socks5ProxyAddress.OrDefault();
                    jObject["socks5ProxyPort"] = Socks5ProxyPort.OrDefault();
                    break;
            }

            return jObject;
        }

        /// <summary>
        /// 从 JSON 对象创建一个新的 <see cref="DnsServer"/> 实例。
        /// </summary>
        public static ParseResult<DnsServer> FromJObject(JObject jObject)
        {
            if (jObject == null)
                return ParseResult<DnsServer>.Failure("JSON 对象为空。");

            if (!jObject.TryGetString("serverAddress", out string serverAddress) ||
                !jObject.TryGetString("serverPort", out string serverPort) ||
                !jObject.TryGetEnum("protocolType", out DnsServerProtocol protocolType) ||
                !jObject.TryGetBool("ignoreFailureResponses", out bool ignoreFailureResponses) ||
                !jObject.TryGetBool("ignoreNegativeResponses", out bool ignoreNegativeResponses) ||
                !jObject.TryGetString("domainMatchingRule", out string domainMatchingRule) ||
                !jObject.TryGetArray("limitQueryTypes", out IReadOnlyList<string> limitQueryTypes))
                return ParseResult<DnsServer>.Failure("必填字段缺失或类型错误。");

            var server = new DnsServer
            {
                ServerAddress = serverAddress,
                ServerPort = serverPort,
                ProtocolType = protocolType,
                IgnoreFailureResponses = ignoreFailureResponses,
                IgnoreNegativeResponses = ignoreNegativeResponses,
                DomainMatchingRule = domainMatchingRule,
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
        #endregion
    }
}
