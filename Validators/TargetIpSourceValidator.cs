using System.Linq;
using FluentValidation;
using FluentValidation.Results;
using SNIBypassGUI.Enums;
using SNIBypassGUI.Models;
using SNIBypassGUI.Common.Network;

namespace SNIBypassGUI.Validators
{
    public class TargetIpSourceValidator : AbstractValidator<TargetIpSource>
    {
        public TargetIpSourceValidator()
        {
            When(source => source.SourceType == IpAddressSourceType.Static, () =>
            {
                RuleFor(source => source.Address)
                    .NotEmpty().WithMessage("目标地址不能为空。");
                RuleFor(source => source.Address)
                    .Must(NetworkUtils.IsValidIP)
                    .WithMessage("“{PropertyValue}” 不是有效的目标地址：应为合法的 IP 地址。")
                    .When(source => !string.IsNullOrEmpty(source.Address));
            });

            When(source => source.SourceType == IpAddressSourceType.Dynamic, () =>
            {
                RuleFor(source => source.QueryDomain)
                    .Must(x => !string.IsNullOrWhiteSpace(x))
                    .WithMessage("查询域名不能为空。");
                RuleFor(source => source.QueryDomain)
                    .Must(NetworkUtils.IsValidDomain)
                    .WithMessage("“{PropertyValue}” 不是有效的查询域名：应符合 RFC 1035、RFC 1123 及国际化域名规范。")
                    .When(source => !string.IsNullOrWhiteSpace(source.QueryDomain));

                RuleFor(source => source.FallbackIpAddresses)
                    .Custom((fallbackIps, context) =>
                    {
                        if (fallbackIps.Count == 0)
                        {
                            var failure = new ValidationFailure(context.PropertyPath, "此来源未关联解析器且无有效回落地址，将不会生效。")
                            { Severity = Severity.Warning };
                            context.AddFailure(failure);
                            return;
                        }

                        var invalidIps = fallbackIps.Select(f => f.Address).Where(ip => !NetworkUtils.IsValidIP(ip)).ToList();

                        if (invalidIps.Any())
                        {
                            foreach (var invalidIp in invalidIps)
                                context.AddFailure($"“{invalidIp}” 不是有效的回落地址：应为合法的 IP 地址。");
                        }
                        else
                        {
                            var failure = new ValidationFailure(context.PropertyPath, "此来源未关联解析器，将使用回落地址。")
                            { Severity = Severity.Warning };
                            context.AddFailure(failure);
                        }
                    })
                    .When(source => source.ResolverId == null);
            });
        }
    }

}
