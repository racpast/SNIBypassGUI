using System.Linq;
using FluentValidation;
using FluentValidation.Results;
using SNIBypassGUI.Common.Network;
using SNIBypassGUI.Enums;
using SNIBypassGUI.Models;

namespace SNIBypassGUI.Validators
{
    public class TargetIpSourceValidator : AbstractValidator<TargetIpSource>
    {
        public TargetIpSourceValidator()
        {
            When(source => source.SourceType == IpAddressSourceType.Static, () =>
            {
                RuleFor(source => source.Addresses)
                    .Must(x => x.Any()).WithMessage("至少需要一个目标地址。");
                RuleFor(source => source.Addresses)
                    .Custom((addresses, context) =>
                    {
                        foreach (var invalidIp in addresses.Where(ip => !NetworkUtils.IsValidIP(ip)).ToList())
                            context.AddFailure($"“{invalidIp}” 不是有效的目标地址：应为合法的 IP 地址。");
                    });
            });

            When(source => source.SourceType == IpAddressSourceType.Dynamic, () =>
            {
                RuleFor(source => source.QueryDomains)
                    .Must(x => x.Any()).WithMessage("至少需要一个查询域名。");
                RuleFor(source => source.QueryDomains)
                    .Custom((domains, context) =>
                    {
                        foreach (var invalidDomain in domains.Where(domain => !NetworkUtils.IsValidDomain(domain)).ToList())
                            context.AddFailure($"“{invalidDomain}” 不是有效的查询域名：应符合 RFC 1035、RFC 1123 及国际化域名规范。");
                    });

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
