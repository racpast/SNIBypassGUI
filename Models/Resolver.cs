using System;
using System.Collections.ObjectModel;
using System.Linq;
using SNIBypassGUI.Common;
using SNIBypassGUI.Common.Extensions;
using SNIBypassGUI.Common.Network;
using SNIBypassGUI.Enums;
using SNIBypassGUI.Interfaces;

namespace SNIBypassGUI.Models
{
    /// <summary>
    /// 表示一个 DNS 解析器。
    /// </summary>
    public class Resolver : NotifyPropertyChangedBase, IStorable
    {
        #region Fields
        private Guid _id;
        private string _resolverName;
        private bool _isBuiltIn;
        private ResolverProtocol _protocolType;
        private string _serverAddress;
        private string _queryTimeout;
        private bool _dnssec;
        private string _clientSubnet;
        private bool _enablePadding;
        private string _dnsCookie;
        private string _udpBufferSize;
        private bool _tlsInsecure;
        private string _tlsServerName;
        private decimal _tlsMinVersion;
        private decimal _tlsMaxVersion;
        private decimal? _preDoQTlsMinVersion;
        private decimal? _preDoQTlsMaxVersion;
        private ObservableCollection<string> _tlsNextProtos = [];
        private ObservableCollection<string> _tlsCipherSuites = [];
        private ObservableCollection<string> _tlsCurvePreferences = [];
        private string _tlsClientCertPath;
        private string _tlsClientKeyPath;
        private string _httpUserAgent;
        private HttpMethodType _httpMethod;
        private ObservableCollection<HttpHeader> _httpHeaders = [];
        private HttpVersionMode _httpVersionMode;
        private bool _enablePmtud;
        private ObservableCollection<string> _quicAlpnTokens = [];
        private bool _quicLengthPrefix;
        private bool _dnsCryptUseTcp;
        private string _dnsCryptUdpSize;
        private string _dnsCryptPublicKey;
        private string _dnsCryptProvider;
        private bool _reuseConnection;
        private string _bootstrapServer;
        private string _bootstrapTimeout;
        #endregion

        #region Properties
        /// <summary>
        /// 此解析器的唯一标识符。
        /// </summary>
        public Guid Id
        {
            get => _id;
            internal set => SetProperty(ref _id, value);
        }

        /// <summary>
        /// 此解析器的名称。
        /// </summary>
        public string ResolverName { get => _resolverName; set => SetProperty(ref _resolverName, value); }

        /// <summary>
        /// 此解析器是否为内置方案。
        /// </summary>
        public bool IsBuiltIn { get => _isBuiltIn; set => SetProperty(ref _isBuiltIn, value); }

        /// <summary>
        /// 此解析器使用的 DNS 协议类型。
        /// </summary>
        public ResolverProtocol ProtocolType
        {
            get => _protocolType;
            set
            {
                var oldProtocol = _protocolType;

                if (SetProperty(ref _protocolType, value))
                {
                    if (value == ResolverProtocol.DnsOverQuic && oldProtocol != ResolverProtocol.DnsOverQuic)
                    {
                        // 拿小本本把当前的 TLS 版本存起来，万一待会要改回去呢
                        _preDoQTlsMinVersion = TlsMinVersion;
                        _preDoQTlsMaxVersion = TlsMaxVersion;

                        // DoQ 强制设置为 1.3
                        TlsMinVersion = 1.3m;
                        TlsMaxVersion = 1.3m;
                    }
                    else if (value != ResolverProtocol.DnsOverQuic && oldProtocol == ResolverProtocol.DnsOverQuic)
                    {
                        // 翻一下小本本
                        if (_preDoQTlsMinVersion.HasValue)
                            TlsMinVersion = _preDoQTlsMinVersion.Value;

                        if (_preDoQTlsMaxVersion.HasValue)
                            TlsMaxVersion = _preDoQTlsMaxVersion.Value;

                        _preDoQTlsMinVersion = null;
                        _preDoQTlsMaxVersion = null;
                    }
                }
            }
        }

        /// <summary>
        /// 此解析器的 DNS 服务器地址。
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
        /// 查询超时时间。
        /// </summary>
        public string QueryTimeout { get => _queryTimeout; set => SetProperty(ref _queryTimeout, value); }

        /// <summary>
        /// 是否启用 DNSSEC。
        /// </summary>
        public bool Dnssec { get => _dnssec; set => SetProperty(ref _dnssec, value); }

        /// <summary>
        /// EDNS0 客户端子网。
        /// </summary>
        public string ClientSubnet { get => _clientSubnet; set => SetProperty(ref _clientSubnet, value); }

        /// <summary>
        /// 是否启用 EDNS0 填充。
        /// </summary>
        public bool EnablePadding { get => _enablePadding; set => SetProperty(ref _enablePadding, value); }

        /// <summary>
        /// EDNS0 Cookie。
        /// </summary>
        public string DnsCookie { get => _dnsCookie; set => SetProperty(ref _dnsCookie, value); }

        /// <summary>
        /// EDNS0 UDP 大小。
        /// </summary>
        public string UdpBufferSize { get => _udpBufferSize; set => SetProperty(ref _udpBufferSize, value); }

        /// <summary>
        /// 是否禁用 TLS 证书验证。
        /// </summary>
        public bool TlsInsecure { get => _tlsInsecure; set => SetProperty(ref _tlsInsecure, value); }

        /// <summary>
        /// 用于验证的 TLS 服务器名称。
        /// </summary>
        public string TlsServerName { get => _tlsServerName; set => SetProperty(ref _tlsServerName, value); }

        /// <summary>
        /// 最低 TLS 版本。
        /// </summary>
        public decimal TlsMinVersion { get => _tlsMinVersion; set => SetProperty(ref _tlsMinVersion, value); }

        /// <summary>
        /// 最高 TLS 版本。
        /// </summary>
        public decimal TlsMaxVersion { get => _tlsMaxVersion; set => SetProperty(ref _tlsMaxVersion, value); }

        /// <summary>
        /// TLS ALPN 协议列表。
        /// </summary>
        public ObservableCollection<string> TlsNextProtos { get => _tlsNextProtos; set => SetProperty(ref _tlsNextProtos, value); }

        /// <summary>
        /// TLS 加密套件列表。
        /// </summary>
        public ObservableCollection<string> TlsCipherSuites { get => _tlsCipherSuites; set => SetProperty(ref _tlsCipherSuites, value); }

        /// <summary>
        /// TLS 椭圆曲线偏好列表。
        /// </summary>
        public ObservableCollection<string> TlsCurvePreferences { get => _tlsCurvePreferences; set => SetProperty(ref _tlsCurvePreferences, value); }

        /// <summary>
        /// 客户端证书文件路径。
        /// </summary>
        public string TlsClientCertPath { get => _tlsClientCertPath; set => SetProperty(ref _tlsClientCertPath, value); }

        /// <summary>
        /// 客户端私钥文件路径。
        /// </summary>
        public string TlsClientKeyPath { get => _tlsClientKeyPath; set => SetProperty(ref _tlsClientKeyPath, value); }

        /// <summary>
        /// DoH 查询使用的 HTTP User-Agent。
        /// </summary>
        public string HttpUserAgent { get => _httpUserAgent; set => SetProperty(ref _httpUserAgent, value); }

        /// <summary>
        /// DoH 查询使用的 HTTP 方法。
        /// </summary>
        public HttpMethodType HttpMethod { get => _httpMethod; set => SetProperty(ref _httpMethod, value); }

        /// <summary>
        /// 自定义的 HTTP 头部信息。
        /// </summary>
        public ObservableCollection<HttpHeader> HttpHeaders { get => _httpHeaders; set => SetProperty(ref _httpHeaders, value); }

        /// <summary>
        /// DoH 查询使用的 HTTP 版本模式。
        /// </summary>
        public HttpVersionMode HttpVersionMode { get => _httpVersionMode; set => SetProperty(ref _httpVersionMode, value); }

        /// <summary>
        /// 是否为 DoQ 或 HTTP/3 启用路径 MTU 发现。
        /// </summary>
        public bool EnablePmtud { get => _enablePmtud; set => SetProperty(ref _enablePmtud, value); }

        /// <summary>
        /// DoQ 查询使用的 QUIC ALPN 令牌。
        /// </summary>
        public ObservableCollection<string> QuicAlpnTokens { get => _quicAlpnTokens; set => SetProperty(ref _quicAlpnTokens, value); }

        /// <summary>
        /// 是否为 DoQ 查询添加 RFC 9250 规定的长度前缀。
        /// </summary>
        public bool QuicLengthPrefix { get => _quicLengthPrefix; set => SetProperty(ref _quicLengthPrefix, value); }

        /// <summary>
        /// DNSCrypt 是否使用 TCP 协议。
        /// </summary>
        public bool DnsCryptUseTcp { get => _dnsCryptUseTcp; set => SetProperty(ref _dnsCryptUseTcp, value); }

        /// <summary>
        /// DNSCrypt 的 UDP 缓冲区大小。
        /// </summary>
        public string DnsCryptUdpSize { get => _dnsCryptUdpSize; set => SetProperty(ref _dnsCryptUdpSize, value); }

        /// <summary>
        /// DNSCrypt 服务器的公钥。
        /// </summary>
        public string DnsCryptPublicKey { get => _dnsCryptPublicKey; set => SetProperty(ref _dnsCryptPublicKey, value); }

        /// <summary>
        /// DNSCrypt 服务器的提供商名称。
        /// </summary>
        public string DnsCryptProvider { get => _dnsCryptProvider; set => SetProperty(ref _dnsCryptProvider, value); }

        /// <summary>
        /// 是否在多次查询中复用连接。
        /// </summary>
        public bool ReuseConnection { get => _reuseConnection; set => SetProperty(ref _reuseConnection, value); }

        /// <summary>
        /// 用于解析服务器域名的引导 DNS 服务器。
        /// </summary>
        public string BootstrapServer { get => _bootstrapServer; set => SetProperty(ref _bootstrapServer, value); }

        /// <summary>
        /// 引导 DNS 服务器的查询超时时间。
        /// </summary>
        public string BootstrapTimeout { get => _bootstrapTimeout; set => SetProperty(ref _bootstrapTimeout, value); }

        /// <summary>
        /// Checks whether this resolver lives in the far-off year 2038 and therefore
        /// requires the shiny, chrome-plated future of networking known as IPv6.
        /// </summary>
        /// <remarks>
        /// <i>
        /// <para>Normally, you'd think a boolean flag like this belongs in the ViewModel. 
        /// And you'd be right, a hero among developers. </para>
        /// 
        /// <para>But not this time. This flag is less of a display suggestion and more of a 
        /// "you must be this tall to ride" sign: without IPv6, this configuration simply won't work.</para>
        /// 
        /// <para>According to the DDD gods, core, unchangeable rules like this belong in the domain model. 
        /// Moving it elsewhere would be a crime against the sacred layers of our architecture. 
        /// And nobody wants to be the villain, right?</para>
        /// </i>
        /// </remarks>
        public bool RequiresIPv6
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ServerAddress)) return false;

                string hostCandidate = ServerAddress;

                int pathSlashIndex = hostCandidate.IndexOf('/');
                if (pathSlashIndex >= 0)
                    hostCandidate = hostCandidate.Substring(0, pathSlashIndex);

                // 如果剥离后为空，则无效
                if (string.IsNullOrWhiteSpace(hostCandidate)) return false;

                // 处理方括号格式，这是 IPv6 带端口的唯一标准形式
                if (hostCandidate.StartsWith("["))
                {
                    int closingBracketIndex = hostCandidate.IndexOf(']');
                    // 如果没有找到结束括号，或者括号是空的，则格式无效
                    if (closingBracketIndex <= 1) return false;

                    // 提取方括号内的内容
                    string addressInBrackets = hostCandidate.Substring(1, closingBracketIndex - 1);

                    // 判断括号内的内容是不是一个有效的 IPv6 地址
                    return NetworkUtils.IsValidIPv6(addressInBrackets);
                }

                return NetworkUtils.IsValidIPv6(hostCandidate);
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// 创建当前 <see cref="Resolver"/> 实例的完整副本。
        /// </summary>
        /// <returns>当前对象的一个完整副本。</returns>
        public Resolver Clone()
        {
            var clone = new Resolver
            {
                Id = Id,
                ResolverName = ResolverName,
                IsBuiltIn = IsBuiltIn,
                ProtocolType = ProtocolType,
                ServerAddress = ServerAddress,
                QueryTimeout = QueryTimeout,
                Dnssec = Dnssec,
                ClientSubnet = ClientSubnet,
                EnablePadding = EnablePadding,
                DnsCookie = DnsCookie,
                UdpBufferSize = UdpBufferSize,
                TlsInsecure = TlsInsecure,
                TlsServerName = TlsServerName,
                TlsMinVersion = TlsMinVersion,
                TlsMaxVersion = TlsMaxVersion,
                TlsClientCertPath = TlsClientCertPath,
                TlsClientKeyPath = TlsClientKeyPath,
                HttpUserAgent = HttpUserAgent,
                HttpMethod = HttpMethod,
                HttpVersionMode = HttpVersionMode,
                EnablePmtud = EnablePmtud,
                QuicLengthPrefix = QuicLengthPrefix,
                DnsCryptUseTcp = DnsCryptUseTcp,
                DnsCryptUdpSize = DnsCryptUdpSize,
                DnsCryptPublicKey = DnsCryptPublicKey,
                DnsCryptProvider = DnsCryptProvider,
                ReuseConnection = ReuseConnection,
                BootstrapServer = BootstrapServer,
                BootstrapTimeout = BootstrapTimeout,
                TlsNextProtos = [.. TlsNextProtos.OrEmpty()],
                TlsCipherSuites = [.. TlsCipherSuites.OrEmpty()],
                TlsCurvePreferences = [.. TlsCurvePreferences.OrEmpty()],
                QuicAlpnTokens = [.. QuicAlpnTokens.OrEmpty()],
                HttpHeaders = [.. HttpHeaders.OrEmpty().Select(header => header.Clone())]
            };

            return clone;
        }

        /// <summary>
        /// 使用指定的 <see cref="Resolver"/> 实例的属性值更新当前实例的内容。
        /// </summary>
        public void UpdateFrom(Resolver source)
        {
            if (source == null) return;
            ResolverName = source.ResolverName;
            IsBuiltIn = source.IsBuiltIn;
            ProtocolType = source.ProtocolType;
            ServerAddress = source.ServerAddress;
            QueryTimeout = source.QueryTimeout;
            Dnssec = source.Dnssec;
            ClientSubnet = source.ClientSubnet;
            EnablePadding = source.EnablePadding;
            DnsCookie = source.DnsCookie;
            UdpBufferSize = source.UdpBufferSize;
            TlsInsecure = source.TlsInsecure;
            TlsServerName = source.TlsServerName;
            TlsMinVersion = source.TlsMinVersion;
            TlsMaxVersion = source.TlsMaxVersion;
            TlsNextProtos = [.. source.TlsNextProtos.OrEmpty()];
            TlsCipherSuites = [.. source.TlsCipherSuites.OrEmpty()];
            TlsCurvePreferences = [.. source.TlsCurvePreferences.OrEmpty()];
            TlsClientCertPath = source.TlsClientCertPath;
            TlsClientKeyPath = source.TlsClientKeyPath;
            HttpUserAgent = source.HttpUserAgent;
            HttpMethod = source.HttpMethod;
            HttpHeaders = [.. source.HttpHeaders.OrEmpty().Select(h => h.Clone())];
            HttpVersionMode = source.HttpVersionMode;
            EnablePmtud = source.EnablePmtud;
            QuicAlpnTokens = [.. source.QuicAlpnTokens.OrEmpty()];
            QuicLengthPrefix = source.QuicLengthPrefix;
            DnsCryptUseTcp = source.DnsCryptUseTcp;
            DnsCryptUdpSize = source.DnsCryptUdpSize;
            DnsCryptPublicKey = source.DnsCryptPublicKey;
            DnsCryptProvider = source.DnsCryptProvider;
            ReuseConnection = source.ReuseConnection;
            BootstrapServer = source.BootstrapServer;
            BootstrapTimeout = source.BootstrapTimeout;
        }
        #endregion
    }
}