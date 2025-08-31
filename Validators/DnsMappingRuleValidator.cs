using System.Linq;
using FluentValidation;
using FluentValidation.Results;
using SNIBypassGUI.Enums;
using SNIBypassGUI.Interop.Pcre;
using SNIBypassGUI.Models;
using SNIBypassGUI.Utils.Network;

namespace SNIBypassGUI.Validators
{
    public class DnsMappingRuleValidator : AbstractValidator<DnsMappingRule>
    {
        public DnsMappingRuleValidator()
        {
            RuleFor(rule => rule.DomainPattern)
                .Must(x => !string.IsNullOrWhiteSpace(x))
                .WithMessage("域名匹配模式不能为空。");
            RuleFor(rule => rule.DomainPattern)
                .Custom((pattern, context) =>
                {
                    var regexContent = pattern.Substring(1).Trim();
                    if (string.IsNullOrEmpty(regexContent))
                    {
                        context.AddFailure("正则表达式不能为空。");
                        return;
                    }
                    int pcreOptions = PcreOptions.PCRE_UTF8 | PcreOptions.PCRE_CASELESS;
                    if (!PcreRegex.TryValidatePattern(regexContent, pcreOptions, out string errorMessage, out int errorOffset))
                        context.AddFailure($"“{regexContent}” 不是一个有效的 PCRE 表达式：在位置 {errorOffset} 存在错误 “{errorMessage}”。");
                })
                .When(rule => !string.IsNullOrWhiteSpace(rule.DomainPattern) &&
                              rule.DomainPattern.Trim().StartsWith("/"));
            RuleFor(rule => rule.DomainPattern)
                .Custom((pattern, context) =>
                {
                    var trimmed = pattern.Trim();
                    if (trimmed.StartsWith(">"))
                    {
                        if (trimmed.IndexOf('>', 1) >= 0)
                        {
                            context.AddFailure("子域匹配符号 “>” 只能出现在模式开头。");
                            return;
                        }
                        if (trimmed.Length == 1)
                        {
                            context.AddFailure("子域匹配符号 “>” 后必须提供域名模式。");
                            return;
                        }
                    }
                })
                .When(rule => !string.IsNullOrWhiteSpace(rule.DomainPattern) &&
                              !rule.DomainPattern.Trim().StartsWith("/"));
            
            When(rule => rule.RuleAction == DnsMappingRuleAction.IP, () =>
            {
                When(rule => rule.TargetIpType == IpAddressSourceType.Static, () =>
                {
                    RuleFor(rule => rule.TargetIp)
                        .Must(x => !string.IsNullOrWhiteSpace(x))
                        .WithMessage("目标地址不能为空。");
                    RuleFor(rule => rule.TargetIp)
                        .Must(NetworkUtils.IsValidIP)
                        .WithMessage("“{PropertyValue}” 不是有效的目标地址：应为合法的 IP 地址。")
                        .When(rule => !string.IsNullOrWhiteSpace(rule.TargetIp));
                });

                When(rule => rule.TargetIpType == IpAddressSourceType.Dynamic, () =>
                {
                    RuleFor(rule => rule.QueryDomain)
                        .Must(x => !string.IsNullOrWhiteSpace(x))
                        .WithMessage("查询域名不能为空。");
                    RuleFor(rule => rule.QueryDomain)
                        .Must(NetworkUtils.IsValidDomain)
                        .WithMessage("“{PropertyValue}” 不是有效的查询域名：应符合 RFC 1035、RFC 1123 及国际化域名规范。")
                        .When(rule => !string.IsNullOrWhiteSpace(rule.QueryDomain));

                    RuleFor(rule => rule.FallbackIpAddresses)
                        .Custom((fallbackIps, context) =>
                        {
                            if (fallbackIps.Count == 0)
                            {
                                var failure = new ValidationFailure(context.PropertyPath, "此规则未关联解析器且无有效回落地址，将不会生效。")
                                { Severity = Severity.Warning };
                                context.AddFailure(failure);
                                return;
                            }

                            var invalidIps = fallbackIps.Where(ip => !NetworkUtils.IsValidIP(ip)).ToList();

                            if (invalidIps.Any())
                            {
                                foreach (var invalidIp in invalidIps)
                                    context.AddFailure($"“{invalidIp}” 不是有效的回落地址：应为合法的 IP 地址。");
                            }
                            else
                            {
                                var failure = new ValidationFailure(context.PropertyPath, "此规则未关联解析器，将使用回落地址。")
                                { Severity = Severity.Warning };
                                context.AddFailure(failure);
                            }
                        })
                        .When(rule => rule.ResolverId == null);
                });
            });
        }
    }
}
