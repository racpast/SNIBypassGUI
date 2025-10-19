using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using SNIBypassGUI.Common.Extensions;
using SNIBypassGUI.Common.Results;
using SNIBypassGUI.Enums;
using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Models;

namespace SNIBypassGUI.Common.Mapper
{
    public class ResolverMapper(IMapper<HttpHeader> httpHeaderMapper) : IMapper<Resolver>
    {
        /// <summary>
        /// 将 <see cref="Resolver"/> 类型的 <paramref name="resolverConfig"/> 实例转换为 JSON 对象。
        /// </summary>
        public JObject ToJObject(Resolver resolverConfig)
        {
            var jObject = new JObject
            {
                ["id"] = resolverConfig.Id.ToString(),
                ["resolverName"] = resolverConfig.ResolverName.OrDefault(),
                ["isBuiltIn"] = resolverConfig.IsBuiltIn,
                ["protocolType"] = resolverConfig.ProtocolType.ToString(),
                ["serverAddress"] = resolverConfig.ServerAddress.OrDefault(),
                ["timeoutSeconds"] = resolverConfig.QueryTimeout.OrDefault(),
                ["dnssec"] = resolverConfig.Dnssec,
                ["clientSubnet"] = resolverConfig.ClientSubnet.OrDefault(),
                ["enablePadding"] = resolverConfig.EnablePadding,
                ["dnsCookie"] = resolverConfig.DnsCookie.OrDefault(),
                ["reuseConnection"] = resolverConfig.ReuseConnection,
                ["bootstrapServer"] = resolverConfig.BootstrapServer.OrDefault(),
                ["bootstrapQueryTimeout"] = resolverConfig.BootstrapTimeout.OrDefault()
            };

            switch (resolverConfig.ProtocolType)
            {
                case ResolverProtocol.Plain:
                    jObject["udpBufferSize"] = resolverConfig.UdpBufferSize.OrDefault();
                    break;

                case ResolverProtocol.Tcp:
                    break;

                case ResolverProtocol.DnsOverHttps:
                    jObject["httpUserAgent"] = resolverConfig.HttpUserAgent.OrDefault();
                    jObject["httpMethod"] = resolverConfig.HttpMethod.ToString();
                    jObject["httpHeaders"] = new JArray(resolverConfig.HttpHeaders?.Select(httpHeaderMapper.ToJObject).OrEmpty());
                    jObject["httpVersionMode"] = resolverConfig.HttpVersionMode.ToString();
                    jObject["enablePmtud"] = resolverConfig.EnablePmtud;
                    jObject["tlsInsecure"] = resolverConfig.TlsInsecure;
                    jObject["tlsServerName"] = resolverConfig.TlsServerName.OrDefault();
                    jObject["tlsMinVersion"] = resolverConfig.TlsMinVersion.ToString();
                    jObject["tlsMaxVersion"] = resolverConfig.TlsMaxVersion.ToString();
                    jObject["tlsNextProtos"] = new JArray(resolverConfig.TlsNextProtos.OrEmpty());
                    jObject["tlsCipherSuites"] = new JArray(resolverConfig.TlsCipherSuites.OrEmpty());
                    jObject["tlsCurvePreferences"] = new JArray(resolverConfig.TlsCurvePreferences.OrEmpty());
                    jObject["tlsClientCertPath"] = resolverConfig.TlsClientCertPath.OrDefault();
                    jObject["tlsClientKeyPath"] = resolverConfig.TlsClientKeyPath.OrDefault();
                    break;

                case ResolverProtocol.DnsOverTls:
                    jObject["tlsInsecure"] = resolverConfig.TlsInsecure;
                    jObject["tlsServerName"] = resolverConfig.TlsServerName.OrDefault();
                    jObject["tlsMinVersion"] = resolverConfig.TlsMinVersion.ToString();
                    jObject["tlsMaxVersion"] = resolverConfig.TlsMaxVersion.ToString();
                    jObject["tlsNextProtos"] = new JArray(resolverConfig.TlsNextProtos.OrEmpty());
                    jObject["tlsCipherSuites"] = new JArray(resolverConfig.TlsCipherSuites.OrEmpty());
                    jObject["tlsCurvePreferences"] = new JArray(resolverConfig.TlsCurvePreferences.OrEmpty());
                    jObject["tlsClientCertPath"] = resolverConfig.TlsClientCertPath.OrDefault();
                    jObject["tlsClientKeyPath"] = resolverConfig.TlsClientKeyPath.OrDefault();
                    break;

                case ResolverProtocol.DnsOverQuic:
                    jObject["enablePmtud"] = resolverConfig.EnablePmtud;
                    jObject["quicAlpnTokens"] = new JArray(resolverConfig.QuicAlpnTokens.OrEmpty());
                    jObject["quicLengthPrefix"] = resolverConfig.QuicLengthPrefix;
                    jObject["tlsInsecure"] = resolverConfig.TlsInsecure;
                    jObject["tlsMinVersion"] = resolverConfig.TlsMinVersion.ToString("F1");
                    jObject["tlsMaxVersion"] = resolverConfig.TlsMaxVersion.ToString("F1");
                    jObject["tlsCipherSuites"] = new JArray(resolverConfig.TlsCipherSuites.OrEmpty());
                    jObject["tlsCurvePreferences"] = new JArray(resolverConfig.TlsCurvePreferences.OrEmpty());
                    jObject["tlsClientCertPath"] = resolverConfig.TlsClientCertPath.OrDefault();
                    jObject["tlsClientKeyPath"] = resolverConfig.TlsClientKeyPath.OrDefault();
                    break;

                case ResolverProtocol.DnsCrypt:
                    jObject["dnsCryptPublicKey"] = resolverConfig.DnsCryptPublicKey.OrDefault();
                    jObject["dnsCryptProvider"] = resolverConfig.DnsCryptProvider.OrDefault();
                    jObject["dnsCryptUseTcp"] = resolverConfig.DnsCryptUseTcp;
                    jObject["dnsCryptUdpSize"] = resolverConfig.DnsCryptUdpSize.OrDefault();
                    break;
            }

            return jObject;
        }

        /// <summary>
        /// 从 JSON 对象创建一个新的 <see cref="Resolver"/> 实例。
        /// </summary>
        public ParseResult<Resolver> FromJObject(JObject jObject)
        {
            if (jObject == null)
                return ParseResult<Resolver>.Failure("JSON 对象为空。");
            try
            {
                if (!jObject.TryGetGuid("id", out var id) ||
                    !jObject.TryGetString("resolverName", out var resolverName) ||
                    !jObject.TryGetBool("isBuiltIn", out var isBuiltIn) ||
                    !jObject.TryGetEnum("protocolType", out ResolverProtocol protocolType) ||
                    !jObject.TryGetString("serverAddress", out var serverAddress) ||
                    !jObject.TryGetString("timeoutSeconds", out var timeoutSeconds) ||
                    !jObject.TryGetBool("dnssec", out var dnssec) ||
                    !jObject.TryGetString("clientSubnet", out var clientSubnet) ||
                    !jObject.TryGetBool("enablePadding", out var enablePadding) ||
                    !jObject.TryGetString("dnsCookie", out var dnsCookie) ||
                    !jObject.TryGetBool("reuseConnection", out var reuseConnection) ||
                    !jObject.TryGetString("bootstrapServer", out var bootstrapServer) ||
                    !jObject.TryGetString("bootstrapQueryTimeout", out var bootstrapQueryTimeout))
                    return ParseResult<Resolver>.Failure("一个或多个通用字段缺失或类型错误。");

                var config = new Resolver
                {
                    Id = id,
                    ResolverName = resolverName,
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
                    case ResolverProtocol.Plain:
                        if (!jObject.TryGetString("udpBufferSize", out var udpBufferSize))
                            return ParseResult<Resolver>.Failure("Plain 协议所需的字段缺失或类型错误。");
                        config.UdpBufferSize = udpBufferSize;
                        break;

                    case ResolverProtocol.Tcp:
                        break;

                    case ResolverProtocol.DnsOverTls:
                        if (!jObject.TryGetBool("tlsInsecure", out var dotTlsInsecure) ||
                            !jObject.TryGetString("tlsServerName", out var dotTlsServerName) ||
                            !jObject.TryGetString("tlsMinVersion", out string dotTlsMinVersionStr) ||
                            !jObject.TryGetString("tlsMaxVersion", out string dotTlsMaxVersionStr) ||
                            !jObject.TryGetArray("tlsNextProtos", out IReadOnlyList<string> dotTlsNextProtos) ||
                            !jObject.TryGetArray("tlsCipherSuites", out IReadOnlyList<string> dotTlsCipherSuites) ||
                            !jObject.TryGetArray("tlsCurvePreferences", out IReadOnlyList<string> dotTlsCurvePreferences) ||
                            !jObject.TryGetString("tlsClientCertPath", out var dotTlsClientCertPath) ||
                            !jObject.TryGetString("tlsClientKeyPath", out var dotTlsClientKeyPath))
                            return ParseResult<Resolver>.Failure("DoT 协议所需的字段缺失或类型错误。");
                        config.TlsInsecure = dotTlsInsecure;
                        config.TlsServerName = dotTlsServerName;
                        if (decimal.TryParse(dotTlsMinVersionStr, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal dotTlsMinVersion)
                            && dotTlsMinVersion >= 1.0m && dotTlsMinVersion <= 1.3m)
                            config.TlsMinVersion = dotTlsMinVersion;
                        else return ParseResult<Resolver>.Failure($"解析 tlsMinVersion 时遇到异常：“{dotTlsMinVersionStr}” 不是有效的 TLS 版本。");
                        if (decimal.TryParse(dotTlsMaxVersionStr, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal dotTlsMaxVersion)
                            && dotTlsMaxVersion >= 1.0m && dotTlsMaxVersion <= 1.3m)
                            config.TlsMaxVersion = dotTlsMaxVersion;
                        else return ParseResult<Resolver>.Failure($"解析 tlsMaxVersion 时遇到异常：“{dotTlsMaxVersionStr}” 不是有效的 TLS 版本。");
                        config.TlsNextProtos = [.. dotTlsNextProtos];
                        config.TlsCipherSuites = [.. dotTlsCipherSuites];
                        config.TlsCurvePreferences = [.. dotTlsCurvePreferences];
                        config.TlsClientCertPath = dotTlsClientCertPath;
                        config.TlsClientKeyPath = dotTlsClientKeyPath;
                        break;

                    case ResolverProtocol.DnsOverHttps:
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
                            return ParseResult<Resolver>.Failure("DoH 协议所需的字段缺失或类型错误。");
                        ObservableCollection<HttpHeader> httpHeaders = [];
                        foreach (var item in httpHeaderObjects.OfType<JObject>())
                        {
                            var parsed = httpHeaderMapper.FromJObject(item);
                            if (!parsed.IsSuccess)
                                return ParseResult<Resolver>.Failure($"解析 httpHeaders 时遇到异常：{parsed.ErrorMessage}");
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
                        else return ParseResult<Resolver>.Failure($"解析 tlsMinVersion 时遇到异常：“{dohTlsMinVersionStr}” 不是有效的 TLS 版本。");
                        if (decimal.TryParse(dohTlsMaxVersionStr, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal dohTlsMaxVersion)
                            && dohTlsMaxVersion >= 1.0m && dohTlsMaxVersion <= 1.3m)
                            config.TlsMaxVersion = dohTlsMaxVersion;
                        else return ParseResult<Resolver>.Failure($"解析 tlsMaxVersion 时遇到异常：“{dohTlsMaxVersionStr}” 不是有效的 TLS 版本。");
                        config.TlsNextProtos = [.. dohTlsNextProtos];
                        config.TlsCipherSuites = [.. dohTlsCipherSuites];
                        config.TlsCurvePreferences = [.. dohTlsCurvePreferences];
                        config.TlsClientCertPath = dohTlsClientCertPath;
                        config.TlsClientKeyPath = dohTlsClientKeyPath;
                        break;

                    case ResolverProtocol.DnsOverQuic:
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
                            return ParseResult<Resolver>.Failure("DoQ 协议所需的字段缺失或类型错误。");
                        config.EnablePmtud = doqPmtud;
                        config.QuicAlpnTokens = [.. quicAlpnTokens];
                        config.QuicLengthPrefix = quicLengthPrefix;
                        config.TlsInsecure = doqTlsInsecure;
                        if (decimal.TryParse(doqTlsMinVersionStr, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal doqTlsMinVersion)
                            && doqTlsMinVersion >= 1.0m && doqTlsMinVersion <= 1.3m)
                            config.TlsMinVersion = doqTlsMinVersion;
                        else return ParseResult<Resolver>.Failure($"解析 tlsMinVersion 时遇到异常：“{doqTlsMinVersionStr}” 不是有效的 TLS 版本。");
                        if (decimal.TryParse(doqTlsMaxVersionStr, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal doqTlsMaxVersion)
                            && doqTlsMaxVersion >= 1.0m && doqTlsMaxVersion <= 1.3m)
                            config.TlsMaxVersion = doqTlsMaxVersion;
                        else return ParseResult<Resolver>.Failure($"解析 tlsMaxVersion 时遇到异常：“{doqTlsMaxVersionStr}” 不是有效的 TLS 版本。");
                        config.TlsCipherSuites = [.. doqTlsCipherSuites];
                        config.TlsCurvePreferences = [.. doqTlsCurvePreferences];
                        config.TlsClientCertPath = doqTlsClientCertPath;
                        config.TlsClientKeyPath = doqTlsClientKeyPath;
                        break;

                    case ResolverProtocol.DnsCrypt:
                        if (!jObject.TryGetBool("dnsCryptUseTcp", out var dnsCryptUseTcp) ||
                            !jObject.TryGetString("dnsCryptUdpSize", out var dnsCryptUdpSize) ||
                            !jObject.TryGetString("dnsCryptPublicKey", out var dnsCryptPublicKey) ||
                            !jObject.TryGetString("dnsCryptProvider", out var dnsCryptProvider))
                            return ParseResult<Resolver>.Failure("DNSCrypt 协议所需的字段缺失或类型错误。");
                        config.DnsCryptPublicKey = dnsCryptPublicKey;
                        config.DnsCryptProvider = dnsCryptProvider;
                        config.DnsCryptUseTcp = dnsCryptUseTcp;
                        config.DnsCryptUdpSize = dnsCryptUdpSize;
                        break;
                }

                return ParseResult<Resolver>.Success(config);
            }
            catch (Exception ex)
            {
                return ParseResult<Resolver>.Failure($"解析 Resolver 时遇到异常：{ex.Message}");
            }
        }
    }
}
