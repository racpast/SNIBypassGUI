using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using MaterialDesignThemes.Wpf;
using Newtonsoft.Json.Linq;
using SNIBypassGUI.Common;
using SNIBypassGUI.Common.Extensions;
using SNIBypassGUI.Common.Network;
using SNIBypassGUI.Common.Results;
using SNIBypassGUI.Enums;
using SNIBypassGUI.Interfaces;

namespace SNIBypassGUI.Models
{
    /// <summary>
    /// 表示一个 DNS 解析器配置。
    /// </summary>
    public class ResolverConfig : NotifyPropertyChangedBase, IStorable
    {
        #region Fields
        private Guid _id;
        private string _configName;
        private bool _isBuiltIn;
        private ResolverConfigProtocol _protocolType;
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
        private ObservableCollection<string> _tlsNextProtos;
        private ObservableCollection<string> _tlsCipherSuites;
        private ObservableCollection<string> _tlsCurvePreferences;
        private string _tlsClientCertPath;
        private string _tlsClientKeyPath;
        private string _httpUserAgent;
        private HttpMethodType _httpMethod;
        private ObservableCollection<HttpHeaderItem> _httpHeaders;
        private HttpVersionMode _httpVersionMode;
        private bool _enablePmtud;
        private ObservableCollection<string> _quicAlpnTokens;
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
        /// 此解析器配置的唯一标识符。
        /// </summary>
        public Guid Id
        {
            get => _id;
            internal set => SetProperty(ref _id, value);
        }

        /// <summary>
        /// 此解析器配置的名称。
        /// </summary>
        public string ConfigName { get => _configName; set => SetProperty(ref _configName, value); }

        /// <summary>
        /// 此解析器配置是否为内置方案。
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
        /// 此解析器配置的图标类型，供 UI 使用。
        /// </summary>
        public PackIconKind ListIconKind => IsBuiltIn ? PackIconKind.ArchiveLockOutline : PackIconKind.SearchWeb;

        /// <summary>
        /// 此解析器配置的类型描述，供 UI 使用。
        /// </summary>
        public string ListTypeDescription => IsBuiltIn ? "(内置)" : "(用户)";

        /// <summary>
        /// 此解析器配置使用的 DNS 协议类型。
        /// </summary>
        public ResolverConfigProtocol ProtocolType { get => _protocolType; set => SetProperty(ref _protocolType, value); }

        /// <summary>
        /// 此解析器配置的 DNS 服务器地址。
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
        public decimal TlsMinVersion
        {
            get => _tlsMinVersion;
            set
            {
                if (SetProperty(ref _tlsMinVersion, value))
                    OnPropertyChanged(nameof(TlsMinVersionDouble));
            }
        }

        /// <summary>
        /// 最高 TLS 版本。
        /// </summary>
        public decimal TlsMaxVersion
        {
            get => _tlsMaxVersion;
            set
            {
                if (SetProperty(ref _tlsMaxVersion, value))
                    OnPropertyChanged(nameof(TlsMaxVersionDouble));
            }
        }

        /// <summary>
        /// 供 UI 绑定的最低 TLS 版本。
        /// </summary>
        public double TlsMinVersionDouble
        {
            get => (double)TlsMinVersion;
            set
            {
                decimal newValue = (decimal)value;
                if (TlsMinVersion != newValue) { TlsMinVersion = newValue; }
            }
        }

        /// <summary>
        /// 供 UI 绑定的最高 TLS 版本。
        /// </summary>
        public double TlsMaxVersionDouble
        {
            get => (double)TlsMaxVersion;
            set
            {
                decimal newValue = (decimal)value;
                if (TlsMaxVersion != newValue) { TlsMaxVersion = newValue; }
            }
        }

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
        public ObservableCollection<HttpHeaderItem> HttpHeaders { get => _httpHeaders; set => SetProperty(ref _httpHeaders, value); }

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
        /// 此解析器配置是否需要 IPv6 支持。
        /// </summary>
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
        /// 创建当前 <see cref="ResolverConfig"/> 实例的完整副本。
        /// </summary>
        /// <returns>当前对象的一个完整副本。</returns>
        public ResolverConfig Clone()
        {
            var clone = new ResolverConfig
            {
                Id = Id,
                ConfigName = ConfigName,
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
        /// 使用指定的 <see cref="ResolverConfig"/> 实例的属性值更新当前实例的内容。
        /// </summary>
        public void UpdateFrom(ResolverConfig source)
        {
            if (source == null) return;
            ConfigName = source.ConfigName;
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

        /// <summary>
        /// 将当前 <see cref="ResolverConfig"/> 实例转换为 JSON 对象。
        /// </summary>
        public JObject ToJObject()
        {
            var jObject = new JObject
            {
                ["id"] = Id.ToString(),
                ["configName"] = ConfigName.OrDefault(),
                ["isBuiltIn"] = IsBuiltIn,
                ["protocolType"] = ProtocolType.ToString(),
                ["serverAddress"] = ServerAddress.OrDefault(),
                ["timeoutSeconds"] = QueryTimeout.OrDefault(),
                ["dnssec"] = Dnssec,
                ["clientSubnet"] = ClientSubnet.OrDefault(),
                ["enablePadding"] = EnablePadding,
                ["dnsCookie"] = DnsCookie.OrDefault(),
                ["reuseConnection"] = ReuseConnection,
                ["bootstrapServer"] = BootstrapServer.OrDefault(),
                ["bootstrapQueryTimeout"] = BootstrapTimeout.OrDefault()
            };

            switch (ProtocolType)
            {
                case ResolverConfigProtocol.Plain:
                    jObject["udpBufferSize"] = UdpBufferSize.OrDefault();
                    break;

                case ResolverConfigProtocol.Tcp:
                    break;

                case ResolverConfigProtocol.DnsOverHttps:
                    jObject["httpUserAgent"] = HttpUserAgent.OrDefault();
                    jObject["httpMethod"] = HttpMethod.ToString();
                    jObject["httpHeaders"] = new JArray(HttpHeaders?.Select(s => s.ToJObject()).OrEmpty());
                    jObject["httpVersionMode"] = HttpVersionMode.ToString();
                    jObject["enablePmtud"] = EnablePmtud;
                    jObject["tlsInsecure"] = TlsInsecure;
                    jObject["tlsServerName"] = TlsServerName.OrDefault();
                    jObject["tlsMinVersion"] = TlsMinVersion.ToString();
                    jObject["tlsMaxVersion"] = TlsMaxVersion.ToString();
                    jObject["tlsNextProtos"] = new JArray(TlsNextProtos.OrEmpty());
                    jObject["tlsCipherSuites"] = new JArray(TlsCipherSuites.OrEmpty());
                    jObject["tlsCurvePreferences"] = new JArray(TlsCurvePreferences.OrEmpty());
                    jObject["tlsClientCertPath"] = TlsClientCertPath.OrDefault();
                    jObject["tlsClientKeyPath"] = TlsClientKeyPath.OrDefault();
                    break;

                case ResolverConfigProtocol.DnsOverTls:
                    jObject["tlsInsecure"] = TlsInsecure;
                    jObject["tlsServerName"] = TlsServerName.OrDefault();
                    jObject["tlsMinVersion"] = TlsMinVersion.ToString();
                    jObject["tlsMaxVersion"] = TlsMaxVersion.ToString();
                    jObject["tlsNextProtos"] = new JArray(TlsNextProtos.OrEmpty());
                    jObject["tlsCipherSuites"] = new JArray(TlsCipherSuites.OrEmpty());
                    jObject["tlsCurvePreferences"] = new JArray(TlsCurvePreferences.OrEmpty());
                    jObject["tlsClientCertPath"] = TlsClientCertPath.OrDefault();
                    jObject["tlsClientKeyPath"] = TlsClientKeyPath.OrDefault();
                    break;

                case ResolverConfigProtocol.DnsOverQuic:
                    jObject["enablePmtud"] = EnablePmtud;
                    jObject["quicAlpnTokens"] = new JArray(QuicAlpnTokens.OrEmpty());
                    jObject["quicLengthPrefix"] = QuicLengthPrefix;
                    jObject["tlsInsecure"] = TlsInsecure;
                    jObject["tlsMinVersion"] = TlsMinVersion.ToString("F1");
                    jObject["tlsMaxVersion"] = TlsMaxVersion.ToString("F1");
                    jObject["tlsCipherSuites"] = new JArray(TlsCipherSuites.OrEmpty());
                    jObject["tlsCurvePreferences"] = new JArray(TlsCurvePreferences.OrEmpty());
                    jObject["tlsClientCertPath"] = TlsClientCertPath.OrDefault();
                    jObject["tlsClientKeyPath"] = TlsClientKeyPath.OrDefault();
                    break;

                case ResolverConfigProtocol.DnsCrypt:
                    jObject["dnsCryptPublicKey"] = DnsCryptPublicKey.OrDefault();
                    jObject["dnsCryptProvider"] = DnsCryptProvider.OrDefault();
                    jObject["dnsCryptUseTcp"] = DnsCryptUseTcp;
                    jObject["dnsCryptUdpSize"] = DnsCryptUdpSize.OrDefault();
                    break;
            }

            return jObject;
        }

        /// <summary>
        /// 从 JSON 对象创建一个新的 <see cref="ResolverConfig"/> 实例。
        /// </summary>
        public static ParseResult<ResolverConfig> FromJObject(JObject jObject)
        {
            if (jObject == null)
                return ParseResult<ResolverConfig>.Failure("JSON 对象为空。");

            if (!jObject.TryGetGuid("id", out var id) ||
                !jObject.TryGetString("configName", out var configName) ||
                !jObject.TryGetBool("isBuiltIn", out var isBuiltIn) ||
                !jObject.TryGetEnum("protocolType", out ResolverConfigProtocol protocolType) ||
                !jObject.TryGetString("serverAddress", out var serverAddress) ||
                !jObject.TryGetString("timeoutSeconds", out var timeoutSeconds) ||
                !jObject.TryGetBool("dnssec", out var dnssec) ||
                !jObject.TryGetString("clientSubnet", out var clientSubnet) ||
                !jObject.TryGetBool("enablePadding", out var enablePadding) ||
                !jObject.TryGetString("dnsCookie", out var dnsCookie) ||
                !jObject.TryGetBool("reuseConnection", out var reuseConnection) ||
                !jObject.TryGetString("bootstrapServer", out var bootstrapServer) ||
                !jObject.TryGetString("bootstrapQueryTimeout", out var bootstrapQueryTimeout))
                return ParseResult<ResolverConfig>.Failure("一个或多个通用字段缺失或类型错误。");

            var config = new ResolverConfig
            {
                Id = id,
                ConfigName = configName,
                IsBuiltIn = isBuiltIn,
                ProtocolType = protocolType,
                ServerAddress = serverAddress,
                QueryTimeout = timeoutSeconds,
                Dnssec = dnssec,
                ClientSubnet = clientSubnet,
                EnablePadding = enablePadding,
                DnsCookie = dnsCookie,
                ReuseConnection = reuseConnection,
                BootstrapServer = bootstrapServer,
                BootstrapTimeout = bootstrapQueryTimeout,
            };

            switch (protocolType)
            {
                case ResolverConfigProtocol.Plain:
                    if (!jObject.TryGetString("udpBufferSize", out var udpBufferSize))
                        return ParseResult<ResolverConfig>.Failure("Plain 协议所需的字段缺失或类型错误。");
                    config.UdpBufferSize = udpBufferSize;
                    break;

                case ResolverConfigProtocol.Tcp:
                    break;

                case ResolverConfigProtocol.DnsOverTls:
                    if (!jObject.TryGetBool("tlsInsecure", out var dotTlsInsecure) ||
                        !jObject.TryGetString("tlsServerName", out var dotTlsServerName) ||
                        !jObject.TryGetString("tlsMinVersion", out string dotTlsMinVersionStr) ||
                        !jObject.TryGetString("tlsMaxVersion", out string dotTlsMaxVersionStr) ||
                        !jObject.TryGetArray("tlsNextProtos", out IReadOnlyList<string> dotTlsNextProtos) ||
                        !jObject.TryGetArray("tlsCipherSuites", out IReadOnlyList<string> dotTlsCipherSuites) ||
                        !jObject.TryGetArray("tlsCurvePreferences", out IReadOnlyList<string> dotTlsCurvePreferences) ||
                        !jObject.TryGetString("tlsClientCertPath", out var dotTlsClientCertPath) ||
                        !jObject.TryGetString("tlsClientKeyPath", out var dotTlsClientKeyPath))
                        return ParseResult<ResolverConfig>.Failure("DoT 协议所需的字段缺失或类型错误。");
                    config.TlsInsecure = dotTlsInsecure;
                    config.TlsServerName = dotTlsServerName;
                    if (decimal.TryParse(dotTlsMinVersionStr, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal dotTlsMinVersion)
                        && dotTlsMinVersion >= 1.0m && dotTlsMinVersion <= 1.3m)
                        config.TlsMinVersion = dotTlsMinVersion;
                    else return ParseResult<ResolverConfig>.Failure($"解析 tlsMinVersion 时出错：“{dotTlsMinVersionStr}” 不是有效的 TLS 版本。");
                    if (decimal.TryParse(dotTlsMaxVersionStr, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal dotTlsMaxVersion)
                        && dotTlsMaxVersion >= 1.0m && dotTlsMaxVersion <= 1.3m)
                        config.TlsMaxVersion = dotTlsMaxVersion;
                    else return ParseResult<ResolverConfig>.Failure($"解析 tlsMaxVersion 时出错：“{dotTlsMaxVersionStr}” 不是有效的 TLS 版本。");
                    config.TlsNextProtos = [.. dotTlsNextProtos];
                    config.TlsCipherSuites = [.. dotTlsCipherSuites];
                    config.TlsCurvePreferences = [.. dotTlsCurvePreferences];
                    config.TlsClientCertPath = dotTlsClientCertPath;
                    config.TlsClientKeyPath = dotTlsClientKeyPath;
                    break;

                case ResolverConfigProtocol.DnsOverHttps:
                    if (!jObject.TryGetString("httpUserAgent", out var httpUserAgent) ||
                        !jObject.TryGetEnum("httpMethod", out HttpMethodType httpMethod) ||
                        !jObject.TryGetArray("httpHeaders", out IReadOnlyList<JObject> httpHeaderObjects) ||
                        !jObject.TryGetEnum("httpVersionMode", out HttpVersionMode httpVersionMode) ||
                        !jObject.TryGetBool("enablePmtud", out var dohPmtud) ||
                        !jObject.TryGetBool("tlsInsecure", out var dohTlsInsecure) ||
                        !jObject.TryGetString("tlsServerName", out var dohTlsServerName) ||
                        !jObject.TryGetString("tlsMinVersion", out string dohTlsMinVersionStr) ||
                        !jObject.TryGetString("tlsMaxVersion", out string dohTlsMaxVersionStr) ||
                        !jObject.TryGetArray("tlsNextProtos", out IReadOnlyList<string> dohTlsNextProtos) ||
                        !jObject.TryGetArray("tlsCipherSuites", out IReadOnlyList<string> dohTlsCipherSuites) ||
                        !jObject.TryGetArray("tlsCurvePreferences", out IReadOnlyList<string> dohTlsCurvePreferences) ||
                        !jObject.TryGetString("tlsClientCertPath", out var dohTlsClientCertPath) ||
                        !jObject.TryGetString("tlsClientKeyPath", out var dohTlsClientKeyPath))
                        return ParseResult<ResolverConfig>.Failure("DoH 协议所需的字段缺失或类型错误。");
                    ObservableCollection<HttpHeaderItem> httpHeaders = [];
                    foreach (var item in httpHeaderObjects.OfType<JObject>())
                    {
                        var parsed = HttpHeaderItem.FromJObject(item);
                        if (!parsed.IsSuccess)
                            return ParseResult<ResolverConfig>.Failure($"解析 httpHeaders 时出错：{parsed.ErrorMessage}");
                        httpHeaders.Add(parsed.Value);
                    }
                    config.HttpUserAgent = httpUserAgent;
                    config.HttpMethod = httpMethod;
                    config.HttpHeaders = httpHeaders;
                    config.HttpVersionMode = httpVersionMode;
                    config.EnablePmtud = dohPmtud;
                    config.TlsInsecure = dohTlsInsecure;
                    config.TlsServerName = dohTlsServerName;
                    if (decimal.TryParse(dohTlsMinVersionStr, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal dohTlsMinVersion)
                        && dohTlsMinVersion >= 1.0m && dohTlsMinVersion <= 1.3m)
                        config.TlsMinVersion = dohTlsMinVersion;
                    else return ParseResult<ResolverConfig>.Failure($"解析 tlsMinVersion 时出错：“{dohTlsMinVersionStr}” 不是有效的 TLS 版本。");
                    if (decimal.TryParse(dohTlsMaxVersionStr, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal dohTlsMaxVersion)
                        && dohTlsMaxVersion >= 1.0m && dohTlsMaxVersion <= 1.3m)
                        config.TlsMaxVersion = dohTlsMaxVersion;
                    else return ParseResult<ResolverConfig>.Failure($"解析 tlsMaxVersion 时出错：“{dohTlsMaxVersionStr}” 不是有效的 TLS 版本。");
                    config.TlsNextProtos = [.. dohTlsNextProtos];
                    config.TlsCipherSuites = [.. dohTlsCipherSuites];
                    config.TlsCurvePreferences = [.. dohTlsCurvePreferences];
                    config.TlsClientCertPath = dohTlsClientCertPath;
                    config.TlsClientKeyPath = dohTlsClientKeyPath;
                    break;

                case ResolverConfigProtocol.DnsOverQuic:
                    if (!jObject.TryGetBool("enablePmtud", out var doqPmtud) ||
                       !jObject.TryGetArray("quicAlpnTokens", out IReadOnlyList<string> quicAlpnTokens) ||
                       !jObject.TryGetBool("quicLengthPrefix", out var quicLengthPrefix) ||
                       !jObject.TryGetBool("tlsInsecure", out var doqTlsInsecure) ||
                       !jObject.TryGetString("tlsMinVersion", out string doqTlsMinVersionStr) ||
                       !jObject.TryGetString("tlsMaxVersion", out string doqTlsMaxVersionStr) ||
                       !jObject.TryGetArray("tlsCipherSuites", out IReadOnlyList<string> doqTlsCipherSuites) ||
                       !jObject.TryGetArray("tlsCurvePreferences", out IReadOnlyList<string> doqTlsCurvePreferences) ||
                       !jObject.TryGetString("tlsClientCertPath", out var doqTlsClientCertPath) ||
                       !jObject.TryGetString("tlsClientKeyPath", out var doqTlsClientKeyPath))
                        return ParseResult<ResolverConfig>.Failure("DoQ 协议所需的字段缺失或类型错误。");
                    config.EnablePmtud = doqPmtud;
                    config.QuicAlpnTokens = [.. quicAlpnTokens];
                    config.QuicLengthPrefix = quicLengthPrefix;
                    config.TlsInsecure = doqTlsInsecure;
                    if (decimal.TryParse(doqTlsMinVersionStr, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal doqTlsMinVersion)
                        && doqTlsMinVersion >= 1.0m && doqTlsMinVersion <= 1.3m)
                        config.TlsMinVersion = doqTlsMinVersion;
                    else return ParseResult<ResolverConfig>.Failure($"解析 tlsMinVersion 时出错：“{doqTlsMinVersionStr}” 不是有效的 TLS 版本。");
                    if (decimal.TryParse(doqTlsMaxVersionStr, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal doqTlsMaxVersion)
                        && doqTlsMaxVersion >= 1.0m && doqTlsMaxVersion <= 1.3m)
                        config.TlsMaxVersion = doqTlsMaxVersion;
                    else return ParseResult<ResolverConfig>.Failure($"解析 tlsMaxVersion 时出错：“{doqTlsMaxVersionStr}” 不是有效的 TLS 版本。");
                    config.TlsCipherSuites = [.. doqTlsCipherSuites];
                    config.TlsCurvePreferences = [.. doqTlsCurvePreferences];
                    config.TlsClientCertPath = doqTlsClientCertPath;
                    config.TlsClientKeyPath = doqTlsClientKeyPath;
                    break;

                case ResolverConfigProtocol.DnsCrypt:
                    if (!jObject.TryGetBool("dnsCryptUseTcp", out var dnsCryptUseTcp) ||
                        !jObject.TryGetString("dnsCryptUdpSize", out var dnsCryptUdpSize) ||
                        !jObject.TryGetString("dnsCryptPublicKey", out var dnsCryptPublicKey) ||
                        !jObject.TryGetString("dnsCryptProvider", out var dnsCryptProvider))
                        return ParseResult<ResolverConfig>.Failure("DNSCrypt 协议所需的字段缺失或类型错误。");
                    config.DnsCryptPublicKey = dnsCryptPublicKey;
                    config.DnsCryptProvider = dnsCryptProvider;
                    config.DnsCryptUseTcp = dnsCryptUseTcp;
                    config.DnsCryptUdpSize = dnsCryptUdpSize;
                    break;
            }

            return ParseResult<ResolverConfig>.Success(config);
        }
        #endregion
    }
}