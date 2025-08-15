using FluentValidation;
using SNIBypassGUI.Enums;
using SNIBypassGUI.Models;
using SNIBypassGUI.Utils.Network;

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
                    .NotEmpty().WithMessage("DoH 主机名不能为空。");

                RuleFor(server => server.DohQueryPath)
                    .NotEmpty().WithMessage("DoH 查询路径不能为空。");
            });

            When(server => server.ProtocolType == DnsServerProtocol.SOCKS5, () =>
            {
                RuleFor(server => server.Socks5ProxyAddress)
                    .NotEmpty().WithMessage("SOCKS5 代理地址不能为空。");
                RuleFor(server => server.Socks5ProxyAddress)
                    .Must(NetworkUtils.IsValidIP)
                    .WithMessage("“{PropertyValue}” 不是有效的 SOCKS5 代理地址：应为合法的 IP 地址。")
                    .When(server => !string.IsNullOrEmpty(server.Socks5ProxyAddress));

                RuleFor(server => server.Socks5ProxyPort)
                    .NotEmpty().WithMessage("SOCKS5 代理端口不能为空。");
                RuleFor(server => server.Socks5ProxyPort)
                    .Must(NetworkUtils.IsValidPort)
                    .WithMessage("“{PropertyValue}” 不是有效的 SOCKS5 代理端口：应为 0 到 65535 的整数。")
                    .When(server => !string.IsNullOrEmpty(server.Socks5ProxyPort));
            });
        }
    }
}
