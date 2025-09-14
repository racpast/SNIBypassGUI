using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentValidation;
using FluentValidation.Results;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using SNIBypassGUI.Enums;
using SNIBypassGUI.Models;
using SNIBypassGUI.Utils.Network;

namespace SNIBypassGUI.Validators
{
    public class ResolverConfigValidator : AbstractValidator<ResolverConfig>
    {
        public ResolverConfigValidator()
        {
            RuleFor(config => config.ConfigName)
                .NotEmpty().WithMessage("配置名称不能为空。");

            RuleFor(config => config.ServerAddress)
                .NotEmpty().WithMessage("服务器地址不能为空。");

            // This validator? Oh, it's not just some uptight nerd checking boxes –
            // it's more like your favorite, slightly sarcastic bar buddy who's seen it all.
            // Got an IPv4 or a domain chilling inside some fancy brackets? It'll raise an eyebrow
            // and be like, "Whoa there, hotshot, those designer brackets are totally overkill for
            // plain old IPs and domains. Save 'em for your high-fashion IPv6 addresses, eh?"
            //
            // Then there's the IPv6 that tried to sneak in a port without its proper square-bracket tuxedo.
            // Our validator here will gently (but firmly) tap its foot and whisper,
            // "Honey, an IPv6 without its brackets before a port is like showing up to the Oscars
            // in sweatpants. Not cool. Get those brackets on, pronto!"
            //
            // In short: part stand-up comedian, part network-address-etiquette coach.
            // It's here to keep your server addresses in line and save you from those forehead-slapping
            // "D'oh!" moments. You're welcome!
            RuleFor(config => config.ServerAddress)
                .Custom((value, context) =>
                {
                    // 处理 [IPv6]:端口 或 [IPv6] 格式（保持不变）
                    if (value.StartsWith("["))
                    {
                        int closingBracketIndex = value.IndexOf(']');
                        if (closingBracketIndex == -1)
                        {
                            context.AddFailure($"“{value}” 不是有效的服务器地址：缺少结束方括号 “]”。");
                            return;
                        }

                        string hostPart = value.Substring(1, closingBracketIndex - 1);
                        string portPart = null;

                        if (string.IsNullOrWhiteSpace(hostPart))
                        {
                            context.AddFailure($"“{value}” 不是有效的服务器地址：方括号内不能是空的。");
                            return;
                        }

                        // 统一处理方括号包裹的非IPv6地址
                        string suggestion = null;
                        string addressType = null;

                        if (NetworkUtils.IsValidIPv4(hostPart))
                        {
                            addressType = "IPv4 ";
                            suggestion = closingBracketIndex + 1 < value.Length && value[closingBracketIndex + 1] == ':'
                                ? $"{hostPart}:{value.Substring(closingBracketIndex + 2)}"
                                : hostPart;
                        }
                        else if (NetworkUtils.IsValidDomain(hostPart))
                        {
                            addressType = "域名";
                            suggestion = closingBracketIndex + 1 < value.Length && value[closingBracketIndex + 1] == ':'
                                ? $"{hostPart}:{value.Substring(closingBracketIndex + 2)}"
                                : hostPart;
                        }

                        if (!string.IsNullOrEmpty(addressType))
                        {
                            context.AddFailure($"“{value}” 不是有效的服务器地址：{addressType}不需要使用方括号包裹，应为 “{suggestion}”。");
                            return;
                        }

                        if (!NetworkUtils.IsValidIPv6(hostPart))
                        {
                            context.AddFailure($"“{value}” 不是有效的服务器地址：“{hostPart}” 不是合法的 IPv6 地址。");
                            return;
                        }

                        // 有效 IPv6 后才检查端口
                        if (value.Length > closingBracketIndex + 1)
                        {
                            if (value[closingBracketIndex + 1] != ':')
                            {
                                context.AddFailure($"“{value}” 不是有效的服务器地址：方括号后必须是冒号加端口。");
                                return;
                            }

                            portPart = value.Substring(closingBracketIndex + 2);
                            if (string.IsNullOrWhiteSpace(portPart))
                            {
                                context.AddFailure($"“{value}” 不是有效的服务器地址：冒号后缺少端口，应为 0 到 65535 的整数。");
                                return;
                            }

                            if (!NetworkUtils.IsValidPort(portPart))
                            {
                                context.AddFailure($"“{value}” 不是有效的服务器地址：“{portPart}” 不是有效的端口，应为 0 到 65535 的整数。");
                                return;
                            }
                        }

                        return; // 合法的 [IPv6] 或 [IPv6]:端口
                    }

                    // 处理裸 IPv6 地址（可能带端口形式）
                    if (NetworkUtils.IsValidIPv6(value))
                    {
                        int lastColon = value.LastIndexOf(':');
                        // 确保最后一段是数字且主机部分也是有效 IPv6
                        if (lastColon > 0 &&
                            !string.IsNullOrEmpty(value.Substring(lastColon + 1)) &&
                            value.Substring(lastColon + 1).All(char.IsDigit))
                        {
                            string lastPart = value.Substring(lastColon + 1);
                            if (int.TryParse(lastPart, out int port) && port >= 0 && port <= 65535)
                            {
                                string possibleHost = value.Substring(0, lastColon);
                                // 验证拆分后的主机部分是否仍是有效 IPv6
                                if (NetworkUtils.IsValidIPv6(possibleHost))
                                {
                                    var warning = new ValidationFailure(
                                        context.PropertyPath,
                                        $"“{value}” 是有效的服务器地址，但是否意为 “[{possibleHost}]:{lastPart}”？"
                                    )
                                    { Severity = Severity.Warning };
                                    context.AddFailure(warning);
                                    return;
                                }
                            }
                        }
                        return; // 合法裸 IPv6
                    }

                    // 处理 host:port 格式
                    int lastColonIndex = value.LastIndexOf(':');
                    if (lastColonIndex >= 0)
                    {
                        string hostPart = value.Substring(0, lastColonIndex);
                        string portPart = value.Substring(lastColonIndex + 1);

                        if (string.IsNullOrWhiteSpace(hostPart))
                        {
                            context.AddFailure($"“{value}” 不是有效的服务器地址：应为合法的域名或 IP 地址，可选择性地携带端口。");
                            return;
                        }

                        // 处理空端口情况
                        if (string.IsNullOrEmpty(portPart))
                        {
                            if (NetworkUtils.IsValidIPv4(hostPart) || NetworkUtils.IsValidDomain(hostPart))
                                context.AddFailure($"“{value}” 不是有效的服务器地址：冒号后缺少端口，应为 0 到 65535 的整数。");
                            else
                                context.AddFailure($"“{value}” 不是有效的服务器地址：应为合法的域名或 IP 地址，可选择性地携带端口。");
                            return;
                        }

                        // 如果主机部分是合法 IPv6，提示需要方括号包裹
                        if (NetworkUtils.IsValidIPv6(hostPart))
                        {
                            context.AddFailure($"“{value}” 不是有效的服务器地址：IPv6 地址使用方括号包裹后才能指定端口，应为 “[{hostPart}]:{portPart}”。");
                            return;
                        }

                        // 先检查主机部分是否有效
                        if (!NetworkUtils.IsValidIPv4(hostPart) && !NetworkUtils.IsValidDomain(hostPart))
                        {
                            context.AddFailure($"“{value}” 不是有效的服务器地址：“{hostPart}” 应为合法的域名或 IP 地址。");
                            return; // 主机无效时直接退出，不再检查端口
                        }

                        // 主机有效时才检查端口格式
                        if (!NetworkUtils.IsValidPort(portPart))
                        {
                            context.AddFailure($"“{value}” 不是有效的服务器地址：“{portPart}” 不是有效的端口，应为 0 到 65535 的整数。");
                            return;
                        }

                        return;
                    }
                    else
                    {
                        // 没有冒号的纯地址
                        if (!NetworkUtils.IsValidIPv4(value) && !NetworkUtils.IsValidDomain(value) && !NetworkUtils.IsValidIPv6(value))
                            context.AddFailure($"“{value}” 不是有效的服务器地址：应为合法的域名或 IP 地址，可选择性地携带端口。");
                    }
                })
                .When(config => !string.IsNullOrWhiteSpace(config.ServerAddress) && config.ProtocolType != ResolverConfigProtocol.DnsOverHttps);
            RuleFor(config => config.ServerAddress)
                .Custom((value, context) =>
                {
                    // 协议头检查
                    if (Regex.IsMatch(value, @"^[^:]+://"))
                    {
                        context.AddFailure($"“{value}” 不是有效的服务器地址：不能包含协议头，应为合法的域名或 IP 地址，可选择性地携带端口和路径。");
                        return;
                    }

                    string hostPortPart;
                    string pathPart = null;

                    // 优先处理 IPv6 方括号边界
                    if (value.StartsWith("["))
                    {
                        int closingBracketIndex = value.IndexOf(']');
                        if (closingBracketIndex == -1) hostPortPart = value;
                        else
                        {
                            int pathSlashIndex = value.IndexOf('/', closingBracketIndex + 1);
                            if (pathSlashIndex >= 0)
                            {
                                hostPortPart = value.Substring(0, pathSlashIndex);
                                pathPart = value.Substring(pathSlashIndex);
                            }
                            else hostPortPart = value;
                        }
                    }
                    else
                    {
                        int firstSlashIndex = value.IndexOf('/');
                        if (firstSlashIndex >= 0)
                        {
                            hostPortPart = value.Substring(0, firstSlashIndex);
                            pathPart = value.Substring(firstSlashIndex);
                        }
                        else hostPortPart = value;
                    }

                    // 主机端口验证
                    if (string.IsNullOrWhiteSpace(hostPortPart))
                    {
                        context.AddFailure($"“{value}” 不是有效的服务器地址：路径前缺少主机地址。");
                        return;
                    }

                    // 裸 IPv6 地址检查
                    if (!hostPortPart.StartsWith("[") && NetworkUtils.IsValidIPv6(hostPortPart))
                    {
                        string suggestion = $"[{hostPortPart}]";
                        context.AddFailure($"“{value}” 不是有效的服务器地址：IPv6 地址必须使用方括号包裹，应为 “{suggestion}{(pathPart ?? "")}”");
                        return;
                    }

                    // 先处理方括号格式的地址
                    if (hostPortPart.StartsWith("["))
                    {
                        int closingBracketIndex = hostPortPart.IndexOf(']');

                        // 先检查结束方括号是否存在
                        if (closingBracketIndex == -1)
                        {
                            context.AddFailure($"“{value}” 不是有效的服务器地址：缺少结束方括号 “]”。");
                            return;
                        }

                        // 提取方括号内容
                        string hostInBracketsCandidate = hostPortPart.Substring(1, closingBracketIndex - 1);

                        // 检查方括号内容是否为空
                        if (string.IsNullOrWhiteSpace(hostInBracketsCandidate))
                        {
                            context.AddFailure($"“{value}” 不是有效的服务器地址：方括号内不能是空的。");
                            return;
                        }

                        // 检查方括号内是否是有效的 IPv4 或域名
                        string suggestion = null;
                        string addressType = null;

                        if (NetworkUtils.IsValidIPv4(hostInBracketsCandidate))
                        {
                            addressType = "IPv4 ";
                            // 去掉方括号，保留端口部分
                            suggestion = hostInBracketsCandidate;
                            if (closingBracketIndex + 1 < hostPortPart.Length)
                                suggestion += hostPortPart.Substring(closingBracketIndex + 1);
                        }
                        else if (NetworkUtils.IsValidDomain(hostInBracketsCandidate))
                        {
                            addressType = "域名";
                            // 建议去掉方括号，保留端口部分
                            suggestion = hostInBracketsCandidate;
                            if (closingBracketIndex + 1 < hostPortPart.Length)
                                suggestion += hostPortPart.Substring(closingBracketIndex + 1);
                        }

                        if (!string.IsNullOrEmpty(addressType))
                        {
                            // 添加路径部分
                            string fullSuggestion = suggestion + (pathPart ?? "");
                            context.AddFailure($"“{value}” 不是有效的服务器地址：{addressType}不需要使用方括号包裹，应为 “{fullSuggestion}”。");
                            return;
                        }

                        // 校验 IPv6 合法性
                        if (!NetworkUtils.IsValidIPv6(hostInBracketsCandidate))
                        {
                            context.AddFailure($"“{value}” 不是有效的服务器地址：“{hostInBracketsCandidate}” 不是合法的 IPv6 地址。");
                            return;
                        }

                        // 检查方括号内是否有类似 [IPv6地址:端口] 但实际是 [IPv6地址]:端口 的情况
                        int lastColonInBrackets = hostInBracketsCandidate.LastIndexOf(':');
                        if (lastColonInBrackets > 0)
                        {
                            string possibleHostNoPort = hostInBracketsCandidate.Substring(0, lastColonInBrackets);
                            string possiblePortPart = hostInBracketsCandidate.Substring(lastColonInBrackets + 1);

                            // 检查前半部分是否为合法 IPv6，后半部分是否为合法端口
                            if (NetworkUtils.IsValidIPv6(possibleHostNoPort) && NetworkUtils.IsValidPort(possiblePortPart))
                            {
                                var warning = new ValidationFailure(
                                    context.PropertyPath,
                                    $"“{value}” 是有效的服务器地址，但是否意为 “[{possibleHostNoPort}]:{possiblePortPart}”？"
                                )
                                { Severity = Severity.Warning };
                                context.AddFailure(warning);
                            }
                        }

                        // 处理端口部分
                        if (hostPortPart.Length > closingBracketIndex + 1)
                        {
                            if (hostPortPart[closingBracketIndex + 1] != ':')
                            {
                                context.AddFailure($"“{value}” 不是有效的服务器地址：方括号后必须是冒号加端口。");
                                return;
                            }

                            string portPart = hostPortPart.Substring(closingBracketIndex + 2);
                            if (string.IsNullOrEmpty(portPart))
                            {
                                context.AddFailure($"“{value}” 不是有效的服务器地址：冒号后缺少端口，应为 0 到 65535 的整数。");
                                return;
                            }

                            if (!NetworkUtils.IsValidPort(portPart))
                            {
                                context.AddFailure($"“{value}” 不是有效的服务器地址：“{portPart}” 不是有效的端口，应为 0 到 65535 的整数。");
                                return;
                            }
                        }
                    }
                    else // 非方括号地址
                    {
                        if (hostPortPart.EndsWith(":"))
                        {
                            context.AddFailure($"“{value}” 不是有效的服务器地址：应为合法的域名或 IP 地址，可选择性地携带端口和路径。");
                            return;
                        }

                        // 处理 host:port 格式
                        int lastColonIndex = hostPortPart.LastIndexOf(':');
                        string hostCandidate;
                        string portCandidate = null;

                        if (lastColonIndex >= 0)
                        {
                            hostCandidate = hostPortPart.Substring(0, lastColonIndex);
                            portCandidate = hostPortPart.Substring(lastColonIndex + 1);
                        }
                        else hostCandidate = hostPortPart;

                        bool isHostValid = NetworkUtils.IsValidIPv4(hostCandidate) ||
                                            NetworkUtils.IsValidDomain(hostCandidate);

                        if (!isHostValid)
                        {
                            context.AddFailure($"“{value}” 不是有效的服务器地址：应为合法的域名或 IP 地址，可选择性地携带端口和路径。");
                            return;
                        }

                        // 端口验证
                        if (portCandidate != null)
                        {
                            if (string.IsNullOrEmpty(portCandidate))
                            {
                                context.AddFailure($"“{value}” 不是有效的服务器地址：冒号后缺少端口，应为 0 到 65535 的整数。");
                                return;
                            }

                            if (!NetworkUtils.IsValidPort(portCandidate))
                            {
                                context.AddFailure($"“{value}” 不是有效的服务器地址：“{portCandidate}” 不是有效的端口，应为 0 到 65535 的整数。");
                                return;
                            }
                        }
                    }

                    // 路径验证
                    if (pathPart != null)
                    {
                        // 检查空路径
                        if (pathPart == "/" || string.IsNullOrWhiteSpace(pathPart.Trim('/')))
                        {
                            var warning = new ValidationFailure(
                                context.PropertyPath,
                                $"“{value}” 是有效的服务器地址，但是否缺少类似 “/dns-query” 的路径部分？"
                            )
                            { Severity = Severity.Warning };
                            context.AddFailure(warning);
                        }
                        else if (!NetworkUtils.IsValidUrlPath(pathPart))
                            context.AddFailure($"“{value}” 不是有效的服务器地址：“{pathPart}” 不是有效的路径。");
                    }
                    else
                    {
                        var warning = new ValidationFailure(
                            context.PropertyPath,
                            $"“{value}” 是有效的服务器地址，但是否缺少类似 “/dns-query” 的路径部分？"
                        )
                        { Severity = Severity.Warning };
                        context.AddFailure(warning);
                    }
                })
                .When(config => !string.IsNullOrWhiteSpace(config.ServerAddress) && config.ProtocolType == ResolverConfigProtocol.DnsOverHttps);

            RuleFor(config => config.QueryTimeout)
                .Must(x => !string.IsNullOrWhiteSpace(x))
                .WithMessage("查询超时为空，将使用默认值 “10s”。")
                .WithSeverity(Severity.Warning);
            RuleFor(config => config.QueryTimeout)
                .Custom((value, context) =>
                {
                    if (!TryParseGoDurationBecauseWhyNotThoughIDontReallyLikeIt(value, out var duration))
                    {
                        context.AddFailure($"“{value}” 不是有效的查询超时，应为一个或多个数字与单位（h, m, s, ms, us, ns）的组合。");
                        return;
                    }
                    var maxTimeout = TimeSpan.FromMinutes(10);
                    // 反正出来之后不可能是负的，不需要检查
                    if (duration > maxTimeout)
                        context.AddFailure($"查询超时 “{value}” 计算为 {duration.TotalSeconds:G} 秒，但最大值为 {maxTimeout.TotalSeconds:G} 秒。");
                })
                .When(config => !string.IsNullOrWhiteSpace(config.QueryTimeout));

            RuleFor(config => config.ClientSubnet)
                .Custom((value, context) =>
                {
                    if (value.StartsWith("[") && value.Contains("]"))
                    {
                        int closingBracketIndex = value.LastIndexOf(']');
                        // 确保括号成对且不为空，且只有一个结束方括号在末尾或后跟斜杠
                        if (closingBracketIndex > 1 && (closingBracketIndex == value.Length - 1 || (closingBracketIndex < value.Length - 1 && value[closingBracketIndex + 1] == '/')))
                        {
                            string hostPart = value.Substring(1, closingBracketIndex - 1);
                            if (NetworkUtils.IsValidIPv4(hostPart) || NetworkUtils.IsValidIPv6(hostPart))
                            {
                                // 重构建议，移除方括号
                                string suggestion = value.Remove(closingBracketIndex, 1).Remove(0, 1);
                                context.AddFailure($"“{value}” 不是有效的 EDNS0 客户端子网：IP 地址不需要使用方括号包裹，应为 “{suggestion}”。");
                                return;
                            }
                        }
                    }

                    int firstSlashIndex = value.IndexOf('/');

                    // 没有斜杠，这始终是错误的
                    if (firstSlashIndex == -1)
                    {
                        if (NetworkUtils.IsValidIPv4(value) || NetworkUtils.IsValidIPv6(value))
                            context.AddFailure($"“{value}” 不是有效的 EDNS0 客户端子网：必须在 “/” 后指定前缀长度。");
                        else
                            context.AddFailure($"“{value}” 不是有效的 EDNS0 客户端子网：应为使用 CIDR 表示法表示的 IP 地址块。");
                        return;
                    }

                    string ipPart = value.Substring(0, firstSlashIndex);
                    string prefixPart = value.Substring(firstSlashIndex + 1);

                    // 检查 IP 部分是否为空
                    if (string.IsNullOrWhiteSpace(ipPart))
                    {
                        context.AddFailure($"“{value}” 不是有效的 EDNS0 客户端子网：在“/” 前缺少合法的 IP 地址。");
                        return;
                    }

                    // 验证 IP 地址部分的合法性
                    bool isIPv4 = NetworkUtils.IsValidIPv4(ipPart);
                    bool isIPv6 = !isIPv4 && NetworkUtils.IsValidIPv6(ipPart);

                    if (!isIPv4 && !isIPv6)
                    {
                        context.AddFailure($"“{value}” 不是有效的 EDNS0 客户端子网：“{ipPart}” 不是合法的 IP 地址。");
                        return;
                    }

                    // 在 IP 地址合法的前提下，检查前缀部分是否为空
                    if (string.IsNullOrEmpty(prefixPart))
                    {
                        context.AddFailure($"“{value}” 不是有效的 EDNS0 客户端子网：在 “/” 后缺少前缀长度。");
                        return;
                    }

                    // 检查前缀是否为纯数字且无前导零
                    if (!prefixPart.All(char.IsDigit) || (prefixPart.StartsWith("0") && prefixPart.Length > 1))
                    {
                        string ipVersion = isIPv4 ? "IPv4" : "IPv6";
                        string range = isIPv4 ? "0 到 32" : "0 到 128";
                        context.AddFailure($"“{value}” 不是有效的 EDNS0 客户端子网：“{prefixPart}” 不是有效的 {ipVersion} 前缀长度，应为 {range} 的整数。");
                        return;
                    }

                    if (!int.TryParse(prefixPart, out int prefix))
                    {
                        context.AddFailure($"“{value}” 不是有效的 EDNS0 客户端子网：“{prefixPart}” 不是有效的前缀长度。");
                        return;
                    }

                    // 检查前缀长度范围
                    if (isIPv4)
                    {
                        if (prefix < 0 || prefix > 32)
                        {
                            context.AddFailure($"“{value}” 不是有效的 EDNS0 客户端子网：“{prefixPart}” 不是有效的前缀长度，IPv4 地址前缀长度应为 0 到 32 的整数。");
                            return;
                        }
                    }
                    else // isIPv6
                    {
                        if (prefix < 0 || prefix > 128)
                        {
                            context.AddFailure($"“{value}” 不是有效的 EDNS0 客户端子网：“{prefixPart}” 不是有效的前缀长度，IPv6 地址前缀长度应为 0 到 128 的整数。");
                            return;
                        }
                    }
                })
                .When(config => !string.IsNullOrWhiteSpace(config.ClientSubnet));

            RuleFor(config => config.DnsCookie)
                .Custom((value, context) =>
                {
                    if (!IsValidHex(value))
                    {
                        context.AddFailure($"“{value}” 不是有效的 EDNS0 Cookie：应为十六进制字符串。");
                        return;
                    }

                    // 长度必须是偶数
                    if (value.Length % 2 != 0)
                    {
                        context.AddFailure($"“{value}” 不是有效的 EDNS0 Cookie：十六进制字符串的长度必须是偶数。");
                        return;
                    }

                    // 十六进制字符串的长度应介于 16 和 64 之间（含）
                    if (value.Length < 16 || value.Length > 64)
                    {
                        context.AddFailure($"“{value}” 不是有效的 EDNS0 Cookie：长度应介于 16 和 64 个十六进制字符之间。");
                        return;
                    }
                })
                .When(config => !string.IsNullOrWhiteSpace(config.DnsCookie));

            RuleFor(config => config.BootstrapServer)
                .Custom((value, context) =>
                {
                    // 处理 [IPv6]:端口 或 [IPv6] 格式
                    if (value.StartsWith("["))
                    {
                        int closingBracketIndex = value.IndexOf(']');
                        if (closingBracketIndex == -1)
                        {
                            context.AddFailure($"“{value}” 不是有效的引导服务器地址：缺少结束方括号 “]”。");
                            return;
                        }

                        string hostPart = value.Substring(1, closingBracketIndex - 1);
                        string portPart = null;

                        if (string.IsNullOrWhiteSpace(hostPart))
                        {
                            context.AddFailure($"“{value}” 不是有效的引导服务器地址：方括号内不能是空的。");
                            return;
                        }

                        // 统一处理方括号包裹的非IPv6地址
                        string suggestion = null;
                        string addressType = null;

                        if (NetworkUtils.IsValidIPv4(hostPart))
                        {
                            addressType = "IPv4 ";
                            suggestion = closingBracketIndex + 1 < value.Length && value[closingBracketIndex + 1] == ':'
                                ? $"{hostPart}:{value.Substring(closingBracketIndex + 2)}"
                                : hostPart;
                        }
                        else if (NetworkUtils.IsValidDomain(hostPart))
                        {
                            addressType = "域名";
                            suggestion = closingBracketIndex + 1 < value.Length && value[closingBracketIndex + 1] == ':'
                                ? $"{hostPart}:{value.Substring(closingBracketIndex + 2)}"
                                : hostPart;
                        }

                        if (!string.IsNullOrEmpty(addressType))
                        {
                            context.AddFailure($"“{value}” 不是有效的引导服务器地址：{addressType}不需要使用方括号包裹，应为 “{suggestion}”。");
                            return;
                        }

                        if (!NetworkUtils.IsValidIPv6(hostPart))
                        {
                            context.AddFailure($"“{value}” 不是有效的引导服务器地址：“{hostPart}” 不是合法的 IPv6 地址。");
                            return;
                        }

                        // 有效 IPv6 后才检查端口
                        if (value.Length > closingBracketIndex + 1)
                        {
                            if (value[closingBracketIndex + 1] != ':')
                            {
                                context.AddFailure($"“{value}” 不是有效的引导服务器地址：方括号后必须是冒号加端口。");
                                return;
                            }

                            portPart = value.Substring(closingBracketIndex + 2);
                            if (string.IsNullOrWhiteSpace(portPart))
                            {
                                context.AddFailure($"“{value}” 不是有效的引导服务器地址：冒号后缺少端口，应为 0 到 65535 的整数。");
                                return;
                            }

                            if (!NetworkUtils.IsValidPort(portPart))
                            {
                                context.AddFailure($"“{value}” 不是有效的引导服务器地址：“{portPart}” 不是有效的端口，应为 0 到 65535 的整数。");
                                return;
                            }
                        }

                        return; // 合法的 [IPv6] 或 [IPv6]:端口
                    }

                    // 处理裸 IPv6 地址（可能带端口形式）
                    if (NetworkUtils.IsValidIPv6(value))
                    {
                        int lastColon = value.LastIndexOf(':');
                        if (lastColon > 0 && !string.IsNullOrEmpty(value.Substring(lastColon + 1))
                            && value.Substring(lastColon + 1).All(char.IsDigit))
                        {
                            string lastPart = value.Substring(lastColon + 1);
                            if (int.TryParse(lastPart, out int port) && port >= 0 && port <= 65535)
                            {
                                string possibleHost = value.Substring(0, lastColon);
                                if (NetworkUtils.IsValidIPv6(possibleHost))
                                {
                                    var warning = new ValidationFailure(
                                        context.PropertyPath,
                                        $"“{value}” 是有效的引导服务器地址，但是否意为 “[{possibleHost}]:{lastPart}”？"
                                    )
                                    { Severity = Severity.Warning };
                                    context.AddFailure(warning);
                                    return;
                                }
                            }
                        }
                        return; // 合法裸 IPv6
                    }

                    // 处理 host:port 格式
                    int lastColonIndex = value.LastIndexOf(':');
                    if (lastColonIndex >= 0)
                    {
                        string hostPart = value.Substring(0, lastColonIndex);
                        string portPart = value.Substring(lastColonIndex + 1);

                        if (string.IsNullOrWhiteSpace(hostPart))
                        {
                            context.AddFailure($"“{value}” 不是有效的引导服务器地址：应为合法的域名或 IP 地址，可选择性地携带端口。");
                            return;
                        }

                        // 处理空端口情况
                        if (string.IsNullOrEmpty(portPart))
                        {
                            if (NetworkUtils.IsValidIPv4(hostPart) || NetworkUtils.IsValidDomain(hostPart))
                                context.AddFailure($"“{value}” 不是有效的引导服务器地址：冒号后缺少端口，应为 0 到 65535 的整数。");
                            else
                                context.AddFailure($"“{value}” 不是有效的引导服务器地址：应为合法的域名或 IP 地址，可选择性地携带端口。");
                            return;
                        }

                        // 如果主机部分是合法 IPv6，提示需要方括号包裹
                        if (NetworkUtils.IsValidIPv6(hostPart))
                        {
                            context.AddFailure($"“{value}” 不是有效的引导服务器地址：IPv6 地址使用方括号包裹后才能指定端口，应为 “[{hostPart}]:{portPart}”。");
                            return;
                        }

                        // 先检查主机部分是否有效
                        if (!NetworkUtils.IsValidIPv4(hostPart) && !NetworkUtils.IsValidDomain(hostPart))
                        {
                            context.AddFailure($"“{value}” 不是有效的引导服务器地址：“{hostPart}” 应为合法的域名或 IP 地址。");
                            return; // 主机无效时直接退出，不再检查端口
                        }

                        // 主机有效时才检查端口格式
                        if (!NetworkUtils.IsValidPort(portPart))
                        {
                            context.AddFailure($"“{value}” 不是有效的引导服务器地址：“{portPart}” 不是有效的端口，应为 0 到 65535 的整数。");
                            return;
                        }

                        return;
                    }
                    else
                    {
                        if (!NetworkUtils.IsValidIPv4(value) && !NetworkUtils.IsValidDomain(value))
                            context.AddFailure($"“{value}” 不是有效的引导服务器地址：应为合法的域名或 IP 地址，可选择性地携带端口。");
                    }
                })
                .When(config => !string.IsNullOrWhiteSpace(config.BootstrapServer));

            RuleFor(config => config.BootstrapTimeout)
                .Must(x => !string.IsNullOrWhiteSpace(x))
                .WithMessage("引导服务器超时为空，将使用默认值 “5s”。")
                .WithSeverity(Severity.Warning)
                .When(config => !string.IsNullOrWhiteSpace(config.BootstrapServer));
            RuleFor(config => config.BootstrapTimeout)
                .Custom((value, context) =>
                {
                    if (!TryParseGoDurationBecauseWhyNotThoughIDontReallyLikeIt(value, out var duration))
                    {
                        context.AddFailure($"“{value}” 不是有效的引导服务器超时：应为一个或多个数字与单位（h, m, s, ms, us, ns）的组合。");
                        return;
                    }
                    var maxTimeout = TimeSpan.FromMinutes(10);
                    if (duration > maxTimeout)
                        context.AddFailure($"引导服务器超时 “{value}” 计算为 {duration.TotalSeconds:G} 秒，但最大值为 {maxTimeout.TotalSeconds:G} 秒。");
                })
                .When(config => !string.IsNullOrWhiteSpace(config.BootstrapTimeout));

            When(config => config.ProtocolType == ResolverConfigProtocol.Plain, () =>
            {
                RuleFor(config => config.UdpBufferSize)
                    .Must(x => !string.IsNullOrWhiteSpace(x))
                    .WithMessage("EDNS0 UDP 缓冲区大小为空，将使用默认值 “1232”。")
                    .WithSeverity(Severity.Warning);
                RuleFor(config => config.UdpBufferSize)
                    .Custom((value, context) =>
                    {
                        if (!int.TryParse(value, out int result) || result < 0 || result > 65535)
                            context.AddFailure($"“{value}” 不是有效的 EDNS0 UDP 缓冲区大小：应为 0 到 65535 的整数。");
                    })
                    .When(p => !string.IsNullOrEmpty(p.UdpBufferSize));
            });

            When(config => config.ProtocolType == ResolverConfigProtocol.DnsOverHttps || config.ProtocolType == ResolverConfigProtocol.DnsOverTls, () =>
            {
                RuleFor(config => config.TlsServerName)
                    .Must(x => !string.IsNullOrWhiteSpace(x))
                    .WithMessage("TLS 服务器名称为空，将自动使用服务器地址的主机名作为默认值。")
                    .WithSeverity(Severity.Warning);
                RuleFor(config => config.TlsServerName)
                    .Custom((value, context) =>
                    {
                        if (NetworkUtils.IsValidIPv4(value) || NetworkUtils.IsValidIPv6(value))
                        {
                            var warning = new ValidationFailure(
                                context.PropertyPath,
                                $"使用 IP 地址 “{value}” 作为 TLS 服务器名称可能会导致证书验证失败，建议使用域名。"
                            )
                            { Severity = Severity.Warning };
                            context.AddFailure(warning);
                            return;
                        }
                        else if (!NetworkUtils.IsValidDomain(value))
                        {
                            context.AddFailure($"“{value}” 不是有效的 TLS 服务器名称：应为合法的域名或 IP 地址。");
                            return;
                        }
                    })
                    .When(config => !string.IsNullOrWhiteSpace(config.TlsServerName));                    
            });

            When(config => config.ProtocolType == ResolverConfigProtocol.DnsOverHttps || config.ProtocolType == ResolverConfigProtocol.DnsOverTls || config.ProtocolType == ResolverConfigProtocol.DnsOverQuic, () =>
            {
                RuleFor(config => config)
                    .Must(config => config.TlsMinVersion <= config.TlsMaxVersion)
                    .WithMessage("最低 TLS 版本不能高于最高 TLS 版本。")
                    .When(config => !string.IsNullOrWhiteSpace(config.TlsServerName));

                RuleFor(config => config)
                    .Custom((config, context) =>
                    {
                        string certPath = config.TlsClientCertPath;
                        string keyPath = config.TlsClientKeyPath;

                        bool hasCert = !string.IsNullOrWhiteSpace(certPath);
                        bool hasKey = !string.IsNullOrWhiteSpace(keyPath);

                        // 如果两个路径都为空，那么校验通过，什么都不用做
                        if (!hasCert && !hasKey) return;

                        // 如果只提供了一个，这是最优先的错误，直接报错并返回
                        if (hasCert != hasKey)
                        {
                            if (!hasCert)
                                context.AddFailure(nameof(config.TlsClientCertPath), "指定 TLS 客户端私钥时，必须同时提供对应的证书。");
                            if (!hasKey)
                                context.AddFailure(nameof(config.TlsClientKeyPath), "指定 TLS 客户端证书时，必须同时提供对应的私钥。");
                            return;
                        }

                        // 只有当两个路径都提供了，才继续进行文件内容和匹配的校验
                        X509Certificate bcCert = null;
                        AsymmetricKeyParameter privateKey = null;

                        // 验证证书文件（只支持 PEM 的，q 是通过调用 Go 语言标准库中的 tls.LoadX509KeyPair 加载的）
                        try
                        {
                            // 直接尝试读取文件，如果文件不存在会在这里抛出异常，比先 Exists 再 Read 更安全
                            using var reader = new StringReader(File.ReadAllText(certPath));
                            var pemReader = new PemReader(reader);
                            if (pemReader.ReadObject() is X509Certificate x509Cert) bcCert = x509Cert;
                            else context.AddFailure(nameof(config.TlsClientCertPath), $"“{certPath}” 不是有效的 TLS 客户端证书：格式无效，应为有效的 PEM 证书。");
                        }
                        catch (FileNotFoundException)
                        {
                            context.AddFailure(nameof(config.TlsClientCertPath), $"“{certPath}” 不是有效的 TLS 客户端证书：文件不存在。");
                        }
                        catch (Exception ex)
                        {
                            context.AddFailure(nameof(config.TlsClientCertPath), $"“{certPath}” 不是有效的 TLS 客户端证书：解析失败，{ex.Message}");
                        }

                        // 验证私钥文件
                        try
                        {
                            using var reader = new StringReader(File.ReadAllText(keyPath));
                            var pemReader = new PemReader(reader);
                            var keyObject = pemReader.ReadObject();

                            if (keyObject is AsymmetricCipherKeyPair keyPair) privateKey = keyPair.Private;
                            else if (keyObject is AsymmetricKeyParameter keyParam && keyParam.IsPrivate) privateKey = keyParam;
                            else context.AddFailure(nameof(config.TlsClientKeyPath), $"“{keyPath}” 不是有效的 TLS 客户端私钥：格式无效，应为有效的 PEM 私钥。");
                        }
                        catch (FileNotFoundException)
                        {
                            context.AddFailure(nameof(config.TlsClientKeyPath), $"“{keyPath}” 不是有效的 TLS 客户端私钥：文件不存在。");
                        }
                        catch (Exception ex)
                        {
                            context.AddFailure(nameof(config.TlsClientCertPath), $"“{keyPath}” 不是有效的 TLS 客户端证书：{ex.Message}");
                        }

                        // 如果证书和私钥都成功解析，最后一步是验证它们是否匹配
                        try
                        {
                            if (bcCert != null && privateKey != null)
                                if (!KeyMatches(bcCert.GetPublicKey(), privateKey))
                                    context.AddFailure(nameof(config.TlsClientKeyPath), $"“{keyPath}” 不是有效的 TLS 客户端私钥：该私钥与 TLS 客户端证书不匹配。");
                        }
                        catch (Exception ex)
                        {
                            context.AddFailure(nameof(config.TlsClientKeyPath), $"“{keyPath}” 不是有效的 TLS 客户端私钥：{ex.Message}。");
                        }

                    });
            });

            When(config => config.ProtocolType == ResolverConfigProtocol.DnsCrypt, () =>
            {
                RuleFor(config => config.DnsCryptProvider)
                    .NotEmpty()
                    .WithMessage("DNSCrypt 提供商名称不能为空。");
                RuleFor(config => config.DnsCryptProvider)
                    .Must(NetworkUtils.IsValidDomain)
                    .WithMessage("“{PropertyValue}” 不是有效的 DNSCrypt 提供商名称：应为有效的完全限定域名。")
                    .When(config => !string.IsNullOrWhiteSpace(config.DnsCryptProvider));

                RuleFor(config => config.DnsCryptPublicKey)
                    .NotEmpty()
                    .WithMessage("DNSCrypt 公钥不能为空。");
                RuleFor(config => config.DnsCryptPublicKey)
                    .Custom((value, context) =>
                    {
                        if (!IsValidHex(value))
                        {
                            context.AddFailure($"“{value}” 不是有效的 DNSCrypt 公钥：应为十六进制字符串。");
                            return;
                        }

                        if (value.Length != 64)
                        {
                            context.AddFailure($"“{value}” 不是有效的 DNSCrypt 公钥：长度应为 64 个十六进制字符。");
                            return;
                        }
                    })
                    .When(config => !string.IsNullOrWhiteSpace(config.DnsCryptPublicKey));

                RuleFor(config => config.DnsCryptUdpSize)
                    .NotEmpty().WithMessage("UDP 缓冲区大小为空，将使用默认值。")
                    .WithSeverity(Severity.Warning);
                RuleFor(config => config.DnsCryptUdpSize)
                    .Custom((value, context) =>
                    {
                        if (!int.TryParse(value, out int result) || result < 0 || result > 65535)
                            context.AddFailure($"“{value}” 不是有效的 UDP 缓冲区大小：应为 0 到 65535 的整数。");
                    })
                    .When(p => !string.IsNullOrEmpty(p.DnsCryptUdpSize));
            });
        }

        // Parsing durations like it's Go, coding like it's C#,
        // questioning life choices like it's 3 AM.
        private static bool TryParseGoDurationBecauseWhyNotThoughIDontReallyLikeIt(string input, out TimeSpan result)
        {
            result = TimeSpan.Zero;
            var remaining = input;

            while (!string.IsNullOrEmpty(remaining))
            {
                int numEnd = 0;
                while (numEnd < remaining.Length && (char.IsDigit(remaining[numEnd]) || remaining[numEnd] == '.'))
                    numEnd++;

                if (numEnd == 0)
                    return false;

                var numStr = remaining.Substring(0, numEnd);
                if (!double.TryParse(numStr, out double number))
                    return false;

                remaining = remaining.Substring(numEnd);

                int unitEnd = 0;
                while (unitEnd < remaining.Length && (char.IsLetter(remaining[unitEnd]) || remaining[unitEnd] == 'µ'))
                    unitEnd++;

                if (unitEnd == 0)
                    return false;

                var unitStr = remaining.Substring(0, unitEnd);
                remaining = remaining.Substring(unitEnd);

                switch (unitStr)
                {
                    case "h": result += TimeSpan.FromHours(number); break;
                    case "m": result += TimeSpan.FromMinutes(number); break;
                    case "s": result += TimeSpan.FromSeconds(number); break;
                    case "ms": result += TimeSpan.FromMilliseconds(number); break;
                    case "us":
                    case "µs": result += TimeSpan.FromTicks((long)(number * 10)); break;
                    case "ns": result += TimeSpan.FromTicks((long)(number / 100)); break;
                    default: return false;
                }
            }

            return true;
        }

        private static bool KeyMatches(AsymmetricKeyParameter publicKey, AsymmetricKeyParameter privateKey)
        {
            byte[] testData = Guid.NewGuid().ToByteArray();
            string algorithm = publicKey switch
            {
                RsaKeyParameters => "SHA256withRSA",
                ECPublicKeyParameters => "SHA256withECDSA",
                DsaPublicKeyParameters => "SHA256withDSA",
                _ => throw new InvalidOperationException("不支持的密钥类型")
            };

            // 使用私钥签名
            ISigner signer = SignerUtilities.GetSigner(algorithm);
            signer.Init(true, privateKey);
            signer.BlockUpdate(testData, 0, testData.Length);
            byte[] signature = signer.GenerateSignature();

            // 使用公钥验证
            signer.Init(false, publicKey);
            signer.BlockUpdate(testData, 0, testData.Length);
            return signer.VerifySignature(signature);
        }

        private static bool IsValidHex(string hexString)
        {
            if (string.IsNullOrWhiteSpace(hexString)) return false;
            return Regex.IsMatch(hexString, @"^[0-9a-fA-F]+$");
        }
    }
}
