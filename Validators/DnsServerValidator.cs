using FluentValidation;
using FluentValidation.Results;
using SNIBypassGUI.Common.Extensions;
using SNIBypassGUI.Common.Network;
using SNIBypassGUI.Consts;
using SNIBypassGUI.Enums;
using SNIBypassGUI.Models;

namespace SNIBypassGUI.Validators
{
    public class DnsServerValidator : AbstractValidator<DnsServer>
    {
        public DnsServerValidator()
        {
            RuleFor(server => server.ServerAddress)
                .NotEmpty().WithMessage("服务器地址不能为空。");
            RuleFor(server => server.ServerAddress)
                .Must(NetworkUtils.IsValidIP)
                .WithMessage("“{PropertyValue}” 不是有效的服务器地址：应为合法的 IP 地址。")
                .When(server => !string.IsNullOrEmpty(server.ServerAddress));

            RuleFor(server => server.ServerPort)
                .NotEmpty().WithMessage("服务器端口不能为空。");
            RuleFor(server => server.ServerPort)
                .Must(NetworkUtils.IsValidPort)
                .WithMessage("“{PropertyValue}” 不是有效的服务器端口：应为 0 到 65535 的整数。")
                .When(server => !string.IsNullOrEmpty(server.ServerPort));

            When(server => server.ProtocolType == DnsServerProtocol.DoH, () =>
            {
                RuleFor(server => server.DohHostname)
                    .Custom((value, context) =>
                    {
                        var trimmed = value?.Trim();
                        if (string.IsNullOrWhiteSpace(trimmed))
                            context.AddFailure("DoH 主机名不能为空。");
                        else if (NetworkUtils.IsValidIP(trimmed))
                        {
                            var warning = new ValidationFailure(
                                context.PropertyPath,
                                $"使用 IP 地址 “{trimmed}” 作为 DoH 主机名可能会导致证书验证失败，建议使用域名。"
                            )
                            { Severity = Severity.Warning };
                            context.AddFailure(warning);
                        }
                        else if (!NetworkUtils.IsValidDomain(trimmed))
                            context.AddFailure($"{value} 不是有效的 DoH 主机名：应为合法的域名或 IP 地址。");
                    });

                RuleFor(server => server.DohQueryPath)
                    .Custom((value, context) =>
                    {
                        if (string.IsNullOrWhiteSpace(value))
                        {
                            var warning = new ValidationFailure(
                                context.PropertyPath,
                                $"DoH 查询路径为空，是否缺少类似 “dns-query” 的路径？"
                            )
                            { Severity = Severity.Warning };
                            context.AddFailure(warning);
                        }
                        else if (value.StartsWith('/'))
                            context.AddFailure($"{value} 不是有效的 DoH 查询路径：不需要以 “/” 开头，应为 “{value.TrimStart('/')}”。");
                        else if (!NetworkUtils.IsValidUrlPath($"/{value}"))
                            context.AddFailure($"{value} 不是有效的 DoH 查询路径。");
                });
            });

            When(server => server.ProtocolType == DnsServerProtocol.SOCKS5, () =>
            {
                RuleFor(server => server.Socks5ProxyAddress)
                    .Must(x => !string.IsNullOrWhiteSpace(x))
                    .WithMessage("SOCKS5 代理地址为空，将使用默认值 “127.0.0.1”。")
                    .WithSeverity(Severity.Warning);
                RuleFor(server => server.Socks5ProxyAddress)
                    .Must(NetworkUtils.IsValidIP)
                    .WithMessage("“{PropertyValue}” 不是有效的 SOCKS5 代理地址：应为合法的 IP 地址。")
                    .When(server => !string.IsNullOrEmpty(server.Socks5ProxyAddress));

                RuleFor(server => server.Socks5ProxyPort)
                    .Must(x => !string.IsNullOrWhiteSpace(x))
                    .WithMessage("SOCKS5 代理端口为空，将使用默认值 “10808”。")
                    .WithSeverity(Severity.Warning);
                RuleFor(server => server.Socks5ProxyPort)
                    .Must(NetworkUtils.IsValidPort)
                    .WithMessage("“{PropertyValue}” 不是有效的 SOCKS5 代理端口：应为 0 到 65535 的整数。")
                    .When(server => !string.IsNullOrEmpty(server.Socks5ProxyPort));
            });

            RuleFor(server => server.DomainMatchingRules)
                .Custom((rules, context) =>
                {
                    for (int i = 0; i < rules.Count; i++)
                    {
                        var pattern = rules[i]?.Pattern?.Trim();
                        if (string.IsNullOrWhiteSpace(pattern))
                        {
                            context.AddFailure($"第 {i + 1} 个域名匹配规则：匹配模式不能为空。");
                            continue;
                        }
                        if (pattern.ContainsAny('^', ';', Chars.Whitespaces))
                        {
                            context.AddFailure($"第 {i + 1} 个域名匹配规则：“{pattern}” 不是有效的匹配模式，不应包含 “^”、“;” 或任意空白字符。");
                            continue;
                        }
                    }
                });
        }
    }
}
