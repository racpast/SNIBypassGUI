using FluentValidation;
using FluentValidation.Results;
using SNIBypassGUI.Models;
using SNIBypassGUI.Common.Network;
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
                .WithMessage("最多只能添加 10 个 DNS 服务器。")
                .Custom((servers, context) =>
                {
                    var serverValidator = new DnsServerValidator();
                    for (int i = 0; i < servers.Count; i++)
                    {
                        var result = serverValidator.Validate(servers[i]);
                        if (!result.IsValid)
                        {
                            var serverNode = new ValidationErrorNode { Message = $"第 {i + 1} 个服务器：" };

                            foreach (var error in result.Errors)
                                serverNode.Children.Add(new ValidationErrorNode { Message = error.ErrorMessage });

                            context.AddFailure(new ValidationFailure(context.PropertyPath, serverNode.Message) { CustomState = serverNode });
                        }
                    }
                });

            RuleFor(config => config.PositiveResponseCacheTime)
                .NotEmpty().WithMessage("肯定响应缓存时间不能为空。");
            RuleFor(config => config.PositiveResponseCacheTime)
                .Matches(@"^\d+$").WithMessage("“{PropertyValue}” 不是有效的肯定响应缓存时间：应为数字。")
                .When(p => !string.IsNullOrEmpty(p.PositiveResponseCacheTime));

            RuleFor(config => config.NegativeResponseCacheTime)
                .NotEmpty().WithMessage("否定响应缓存时间不能为空。");
            RuleFor(config => config.NegativeResponseCacheTime)
                .Matches(@"^\d+$").WithMessage("“{PropertyValue}” 不是有效的否定响应缓存时间：应为数字。")
                .When(p => !string.IsNullOrEmpty(p.NegativeResponseCacheTime));

            RuleFor(config => config.FailedResponseCacheTime)
                .NotEmpty().WithMessage("失败响应缓存时间不能为空。");
            RuleFor(config => config.FailedResponseCacheTime)
                .Matches(@"^\d+$").WithMessage("“{PropertyValue}” 不是有效的失败响应缓存时间：应为数字。")
                .When(p => !string.IsNullOrEmpty(p.FailedResponseCacheTime));

            RuleFor(config => config.SilentCacheUpdateTime)
                .NotEmpty().WithMessage("缓存静默更新阈值不能为空。");
            RuleFor(config => config.SilentCacheUpdateTime)
                .Matches(@"^\d+$").WithMessage("“{PropertyValue}” 不是有效的缓存静默更新阈值：应为数字。")
                .When(p => !string.IsNullOrEmpty(p.SilentCacheUpdateTime));

            RuleFor(config => config.CacheAutoCleanupTime)
                .NotEmpty().WithMessage("缓存自动清理时间不能为空。");
            RuleFor(config => config.CacheAutoCleanupTime)
                .Matches(@"^\d+$").WithMessage("“{PropertyValue}” 不是有效的缓存自动清理时间：应为数字。")
                .When(p => !string.IsNullOrEmpty(p.CacheAutoCleanupTime));

            RuleFor(server => server.LocalIpv4BindingAddress)
                .NotEmpty().WithMessage("本地 IPv4 绑定地址不能为空。");
            RuleFor(server => server.LocalIpv4BindingAddress)
                .Must(NetworkUtils.IsValidIPv4)
                .WithMessage("“{PropertyValue}” 不是有效的本地 IPv4 绑定地址：应为合法的 IPv4 地址。")
                .When(server => !string.IsNullOrEmpty(server.LocalIpv4BindingAddress));

            RuleFor(server => server.LocalIpv4BindingPort)
                .NotEmpty().WithMessage("本地 IPv4 绑定端口不能为空。");
            RuleFor(server => server.LocalIpv4BindingPort)
                .Must(NetworkUtils.IsValidPort)
                .WithMessage("“{PropertyValue}” 不是有效的本地 IPv4 绑定端口：应为 0 到 65535 的整数。")
                .When(server => !string.IsNullOrEmpty(server.LocalIpv4BindingPort));

            RuleFor(server => server.LocalIpv6BindingAddress)
                .NotEmpty().WithMessage("本地 IPv6 绑定地址不能为空。");
            RuleFor(server => server.LocalIpv6BindingAddress)
                .Must(NetworkUtils.IsValidIPv6)
                .WithMessage("“{PropertyValue}” 不是有效的本地 IPv6 绑定地址：应为合法的 IPv6 地址。")
                .When(server => !string.IsNullOrEmpty(server.LocalIpv6BindingAddress));

            RuleFor(server => server.LocalIpv6BindingPort)
                .NotEmpty().WithMessage("本地 IPv6 绑定端口不能为空。");
            RuleFor(server => server.LocalIpv6BindingPort)
                .Must(NetworkUtils.IsValidPort)
                .WithMessage("“{PropertyValue}” 不是有效的本地 IPv6 绑定：应为 0 到 65535 的整数。")
                .When(server => !string.IsNullOrEmpty(server.LocalIpv6BindingPort));

            RuleFor(config => config.GeneratedResponseTtl)
                .NotEmpty().WithMessage("本地生成响应 TTL 不能为空。");
            RuleFor(config => config.GeneratedResponseTtl)
                .Matches(@"^\d+$").WithMessage("“{PropertyValue}” 不是有效的本地生成响应 TTL：应为数字。")
                .When(p => !string.IsNullOrEmpty(p.GeneratedResponseTtl));

            RuleFor(config => config.UdpResponseTimeout)
                .NotEmpty().WithMessage("UDP 协议响应超时不能为空。");
            RuleFor(config => config.UdpResponseTimeout)
                .Matches(@"^\d+$").WithMessage("“{PropertyValue}” 不是有效的 UDP 协议响应超时：应为数字。")
                .When(p => !string.IsNullOrEmpty(p.UdpResponseTimeout));

            RuleFor(config => config.TcpFirstByteTimeout)
                .NotEmpty().WithMessage("TCP 协议首字节超时不能为空。");
            RuleFor(config => config.TcpFirstByteTimeout)
                .Matches(@"^\d+$").WithMessage("“{PropertyValue}” 不是有效的 TCP 协议首字节超时：应为数字。")
                .When(p => !string.IsNullOrEmpty(p.TcpFirstByteTimeout));

            RuleFor(config => config.TcpInternalTimeout)
                .NotEmpty().WithMessage("TCP 协议内部超时不能为空。");
            RuleFor(config => config.TcpInternalTimeout)
                .Matches(@"^\d+$").WithMessage("“{PropertyValue}” 不是有效的 TCP 协议内部超时：应为数字。")
                .When(p => !string.IsNullOrEmpty(p.TcpInternalTimeout));

            RuleFor(config => config.Socks5FirstByteTimeout)
                .NotEmpty().WithMessage("SOCKS5 代理首字节超时不能为空。");
            RuleFor(config => config.Socks5FirstByteTimeout)
                .Matches(@"^\d+$").WithMessage("“{PropertyValue}” 不是有效的 SOCKS5 代理首字节超时：应为数字。")
                .When(p => !string.IsNullOrEmpty(p.Socks5FirstByteTimeout));

            RuleFor(config => config.Socks5OtherByteTimeout)
                .NotEmpty().WithMessage("SOCKS5 代理其他字节超时不能为空。");
            RuleFor(config => config.Socks5OtherByteTimeout)
                .Matches(@"^\d+$").WithMessage("“{PropertyValue}” 不是有效的 SOCKS5 代理其他字节超时：应为数字。")
                .When(p => !string.IsNullOrEmpty(p.Socks5OtherByteTimeout));

            RuleFor(config => config.Socks5ConnectTimeout)
                .NotEmpty().WithMessage("SOCKS5 代理连接超时不能为空。");
            RuleFor(config => config.Socks5ConnectTimeout)
                .Matches(@"^\d+$").WithMessage("“{PropertyValue}” 不是有效的 SOCKS5 代理连接超时：应为数字。")
                .When(p => !string.IsNullOrEmpty(p.Socks5ConnectTimeout));

            RuleFor(config => config.Socks5ResponseTimeout)
                .NotEmpty().WithMessage("SOCKS5 代理响应超时不能为空。");
            RuleFor(config => config.Socks5ResponseTimeout)
                .Matches(@"^\d+$").WithMessage("“{PropertyValue}” 不是有效的 SOCKS5 代理响应超时：应为数字。")
                .When(p => !string.IsNullOrEmpty(p.Socks5ResponseTimeout));

            RuleFor(config => config.LogMemoryBufferSize)
                .NotEmpty().WithMessage("日志内存缓冲上限不能为空。");
            RuleFor(config => config.LogMemoryBufferSize)
                .Matches(@"^\d+$").WithMessage("“{PropertyValue}” 不是有效的日志内存缓冲上限：应为数字。")
                .When(p => !string.IsNullOrEmpty(p.LogMemoryBufferSize));
        }
    }
}
