using System.Linq;
using FluentValidation;
using FluentValidation.Results;
using SNIBypassGUI.Common.Network;
using SNIBypassGUI.Models;
using SNIBypassGUI.ViewModels.Validation;

namespace SNIBypassGUI.Validators
{
    public class DnsConfigValidator : AbstractValidator<DnsConfig>
    {
        public DnsConfigValidator()
        {
            RuleFor(config => config.ConfigName)
                .NotEmpty().WithMessage("配置名称不能为空。");

            RuleFor(config => config.DnsServers)
                .Must(servers => servers != null && servers.Count <= 10)
                .WithMessage("最多只能添加 10 个上游 DNS 服务器。")
                .Custom((servers, context) =>
                {
                    if (!servers.Any())
                    {
                        var warning = new ValidationFailure(
                            context.PropertyPath,
                            "未配置任何上游 DNS 服务器，将仅能从映射表及地址缓存中解析域名。"
                        )
                        { Severity = Severity.Warning };
                        context.AddFailure(warning);
                        return;
                    }

                    var serverValidator = new DnsServerValidator();

                    for (int i = 0; i < servers.Count; i++)
                    {
                        var result = serverValidator.Validate(servers[i]);
                        if (result.Errors.Any())
                        {
                            var errors = result.Errors.Where(e => e.Severity == Severity.Error).ToList();
                            var warnings = result.Errors.Where(e => e.Severity == Severity.Warning).ToList();

                            if (errors.Any())
                            {
                                var serverNode = new ValidationErrorNode { Message = $"第 {i + 1} 个上游服务器：" };

                                foreach (var error in errors)
                                {
                                    // 检查是否有结构化的错误节点
                                    if (error.CustomState is ValidationErrorNode structuredError)
                                        serverNode.AddChild(structuredError);
                                    else serverNode.AddChild(new ValidationErrorNode { Message = error.ErrorMessage });
                                }

                                context.AddFailure(new ValidationFailure(context.PropertyPath, serverNode.Message) { CustomState = serverNode });
                            }

                            if (warnings.Any())
                            {
                                var serverNode = new ValidationErrorNode { Message = $"第 {i + 1} 个上游服务器：" };

                                foreach (var warning in warnings)
                                {
                                    // 检查是否有结构化的警告节点
                                    if (warning.CustomState is ValidationErrorNode structuredWarning)
                                        serverNode.AddChild(structuredWarning);
                                    else serverNode.AddChild(new ValidationErrorNode { Message = warning.ErrorMessage });
                                }

                                context.AddFailure(new ValidationFailure(context.PropertyPath, serverNode.Message) { CustomState = serverNode, Severity = Severity.Warning });
                            }
                        }
                    }
                });

            RuleFor(config => config.AddressCacheScavengingTime)
                .Must(x => !string.IsNullOrWhiteSpace(x))
                .WithMessage("肯定响应缓存时间为空，将使用默认值 “360”。")
                .WithSeverity(Severity.Warning);
            RuleFor(config => config.AddressCacheScavengingTime)
                .Matches(@"^\d+$").WithMessage("“{PropertyValue}” 不是有效的肯定响应缓存时间：应为数字。")
                .When(p => !string.IsNullOrEmpty(p.AddressCacheScavengingTime));

            RuleFor(config => config.AddressCacheNegativeTime)
                .Must(x => !string.IsNullOrWhiteSpace(x))
                .WithMessage("否定响应缓存时间为空，将使用默认值 “60”。")
                .WithSeverity(Severity.Warning);
            RuleFor(config => config.AddressCacheNegativeTime)
                .Matches(@"^\d+$").WithMessage("“{PropertyValue}” 不是有效的否定响应缓存时间：应为数字。")
                .When(p => !string.IsNullOrEmpty(p.AddressCacheNegativeTime));

            RuleFor(config => config.AddressCacheFailureTime)
                .Must(x => !string.IsNullOrWhiteSpace(x))
                .WithMessage("失败响应缓存时间为空，将使用默认值 “0”。")
                .WithSeverity(Severity.Warning);
            RuleFor(config => config.AddressCacheFailureTime)
                .Matches(@"^\d+$").WithMessage("“{PropertyValue}” 不是有效的失败响应缓存时间：应为数字。")
                .When(p => !string.IsNullOrEmpty(p.AddressCacheFailureTime));

            RuleFor(config => config.AddressCacheSilentUpdateTime)
                .Must(x => !string.IsNullOrWhiteSpace(x))
                .WithMessage("缓存静默更新阈值为空，将使用默认值 “240”。")
                .WithSeverity(Severity.Warning);
            RuleFor(config => config.AddressCacheSilentUpdateTime)
                .Matches(@"^\d+$").WithMessage("“{PropertyValue}” 不是有效的缓存静默更新阈值：应为数字。")
                .When(p => !string.IsNullOrEmpty(p.AddressCacheSilentUpdateTime));

            RuleFor(config => config.AddressCachePeriodicPruningTime)
                .Must(x => !string.IsNullOrWhiteSpace(x))
                .WithMessage("缓存自动清理时间为空，将使用默认值 “60”。")
                .WithSeverity(Severity.Warning);
            RuleFor(config => config.AddressCachePeriodicPruningTime)
                .Matches(@"^\d+$").WithMessage("“{PropertyValue}” 不是有效的缓存自动清理时间：应为数字。")
                .When(p => !string.IsNullOrEmpty(p.AddressCachePeriodicPruningTime));

            RuleFor(server => server.LocalIpv4BindingAddress)
                .Must(x => !string.IsNullOrWhiteSpace(x))
                .WithMessage("本地 IPv4 绑定地址为空，将不会监听 IPv4 请求。若要监听所有地址，请输入 “0.0.0.0”。")
                .WithSeverity(Severity.Warning);
            RuleFor(server => server.LocalIpv4BindingAddress)
                .Must(NetworkUtils.IsValidIPv4)
                .WithMessage("“{PropertyValue}” 不是有效的本地 IPv4 绑定地址：应为合法的 IPv4 地址。")
                .When(server => !string.IsNullOrEmpty(server.LocalIpv4BindingAddress));

            RuleFor(server => server.LocalIpv4BindingPort)
                .Must(x => !string.IsNullOrWhiteSpace(x))
                .WithMessage("本地 IPv4 绑定端口为空，将使用默认值 “53”。")
                .WithSeverity(Severity.Warning);
            RuleFor(server => server.LocalIpv4BindingPort)
                .Must(NetworkUtils.IsValidPort)
                .WithMessage("“{PropertyValue}” 不是有效的本地 IPv4 绑定端口：应为 0 到 65535 的整数。")
                .When(server => !string.IsNullOrEmpty(server.LocalIpv4BindingPort));

            RuleFor(server => server.LocalIpv6BindingAddress)
                .Must(x => !string.IsNullOrWhiteSpace(x))
                .WithMessage("本地 IPv6 绑定地址为空，将不会监听 IPv6 请求。若要监听所有地址，请输入 “::”。")
                .WithSeverity(Severity.Warning);
            RuleFor(server => server.LocalIpv6BindingAddress)
                .Must(NetworkUtils.IsValidIPv6)
                .WithMessage("“{PropertyValue}” 不是有效的本地 IPv6 绑定地址：应为合法的 IPv6 地址。")
                .When(server => !string.IsNullOrEmpty(server.LocalIpv6BindingAddress));

            RuleFor(server => server.LocalIpv6BindingPort)
                .Must(x => !string.IsNullOrWhiteSpace(x))
                .WithMessage("本地 IPv6 绑定端口为空，将使用默认值 “53”。")
                .WithSeverity(Severity.Warning);
            RuleFor(server => server.LocalIpv6BindingPort)
                .Must(NetworkUtils.IsValidPort)
                .WithMessage("“{PropertyValue}” 不是有效的本地 IPv6 绑定：应为 0 到 65535 的整数。")
                .When(server => !string.IsNullOrEmpty(server.LocalIpv6BindingPort));

            RuleFor(config => config.GeneratedResponseTimeToLive)
                .Must(x => !string.IsNullOrWhiteSpace(x))
                .WithMessage("本地生成响应 TTL 为空，将使用默认值 “0”。")
                .WithSeverity(Severity.Warning);
            RuleFor(config => config.GeneratedResponseTimeToLive)
                .Matches(@"^\d+$").WithMessage("“{PropertyValue}” 不是有效的本地生成响应 TTL：应为数字。")
                .When(p => !string.IsNullOrEmpty(p.GeneratedResponseTimeToLive));

            RuleFor(config => config.ServerUdpProtocolResponseTimeout)
                .Must(x => !string.IsNullOrWhiteSpace(x))
                .WithMessage("UDP 响应超时为空，将使用默认值 “3989”。")
                .WithSeverity(Severity.Warning);
            RuleFor(config => config.ServerUdpProtocolResponseTimeout)
                .Matches(@"^\d+$").WithMessage("“{PropertyValue}” 不是有效的 UDP 响应超时：应为数字。")
                .When(p => !string.IsNullOrEmpty(p.ServerUdpProtocolResponseTimeout));

            RuleFor(config => config.ServerTcpProtocolResponseTimeout)
                .Must(x => !string.IsNullOrWhiteSpace(x))
                .WithMessage("TCP 首字节超时为空，将使用默认值 “3989”。")
                .WithSeverity(Severity.Warning);
            RuleFor(config => config.ServerTcpProtocolResponseTimeout)
                .Matches(@"^\d+$").WithMessage("“{PropertyValue}” 不是有效的 TCP 首字节超时：应为数字。")
                .When(p => !string.IsNullOrEmpty(p.ServerTcpProtocolResponseTimeout));

            RuleFor(config => config.ServerTcpProtocolInternalTimeout)
                .Must(x => !string.IsNullOrWhiteSpace(x))
                .WithMessage("TCP 其余字节超时为空，将使用默认值 “3989”。")
                .WithSeverity(Severity.Warning);
            RuleFor(config => config.ServerTcpProtocolInternalTimeout)
                .Matches(@"^\d+$").WithMessage("“{PropertyValue}” 不是有效的 TCP 其余字节超时：应为数字。")
                .When(p => !string.IsNullOrEmpty(p.ServerTcpProtocolInternalTimeout));

            RuleFor(config => config.ServerSocks5ProtocolProxyFirstByteTimeout)
                .Must(x => !string.IsNullOrWhiteSpace(x))
                .WithMessage("SOCKS5 代理首字节超时为空，将使用默认值 “3989”。")
                .WithSeverity(Severity.Warning);
            RuleFor(config => config.ServerSocks5ProtocolProxyFirstByteTimeout)
                .Matches(@"^\d+$").WithMessage("“{PropertyValue}” 不是有效的 SOCKS5 代理首字节超时：应为数字。")
                .When(p => !string.IsNullOrEmpty(p.ServerSocks5ProtocolProxyFirstByteTimeout));

            RuleFor(config => config.ServerSocks5ProtocolProxyOtherBytesTimeout)
                .Must(x => !string.IsNullOrWhiteSpace(x))
                .WithMessage("SOCKS5 代理其余字节为空，将使用默认值 “3989”。")
                .WithSeverity(Severity.Warning);
            RuleFor(config => config.ServerSocks5ProtocolProxyOtherBytesTimeout)
                .Matches(@"^\d+$").WithMessage("“{PropertyValue}” 不是有效的 SOCKS5 代理其余字节超时：应为数字。")
                .When(p => !string.IsNullOrEmpty(p.ServerSocks5ProtocolProxyOtherBytesTimeout));

            RuleFor(config => config.ServerSocks5ProtocolProxyRemoteConnectTimeout)
                .Must(x => !string.IsNullOrWhiteSpace(x))
                .WithMessage("SOCKS5 代理连接超时为空，将使用默认值 “3989”。")
                .WithSeverity(Severity.Warning);
            RuleFor(config => config.ServerSocks5ProtocolProxyRemoteConnectTimeout)
                .Matches(@"^\d+$").WithMessage("“{PropertyValue}” 不是有效的 SOCKS5 代理连接超时：应为数字。")
                .When(p => !string.IsNullOrEmpty(p.ServerSocks5ProtocolProxyRemoteConnectTimeout));

            RuleFor(config => config.ServerSocks5ProtocolProxyRemoteResponseTimeout)
                .Must(x => !string.IsNullOrWhiteSpace(x))
                .WithMessage("SOCKS5 代理响应超时为空，将使用默认值 “3989”。")
                .WithSeverity(Severity.Warning);
            RuleFor(config => config.ServerSocks5ProtocolProxyRemoteResponseTimeout)
                .Matches(@"^\d+$").WithMessage("“{PropertyValue}” 不是有效的 SOCKS5 代理响应超时：应为数字。")
                .When(p => !string.IsNullOrEmpty(p.ServerSocks5ProtocolProxyRemoteResponseTimeout));

            RuleFor(config => config.HitLogMaxPendingHits)
                .Must(x => !string.IsNullOrWhiteSpace(x))
                .WithMessage("日志内存缓冲上限为空，将使用默认值 “6”。")
                .WithSeverity(Severity.Warning);
            RuleFor(config => config.HitLogMaxPendingHits)
                .Matches(@"^\d+$").WithMessage("“{PropertyValue}” 不是有效的日志内存缓冲上限：应为数字。")
                .When(p => !string.IsNullOrEmpty(p.HitLogMaxPendingHits));
        }
    }
}
