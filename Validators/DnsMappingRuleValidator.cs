using System.Linq;
using FluentValidation;
using FluentValidation.Results;
using SNIBypassGUI.Enums;
using SNIBypassGUI.Interop.Pcre;
using SNIBypassGUI.Models;
using SNIBypassGUI.ViewModels.Validation;

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
                RuleFor(rule => rule.TargetSources)
                    .Custom((sources, context) =>
                    {
                        if (sources.Count == 0)
                        {
                            context.AddFailure(new ValidationFailure(context.PropertyPath, "此规则未配置任何目标 IP 来源，将不会生效。") { Severity = Severity.Warning });
                            return;
                        }

                        var sourceValidator = new TargetIpSourceValidator();

                        for (int i = 0; i < sources.Count; i++)
                        {
                            var result = sourceValidator.Validate(sources[i]);
                            if (result.Errors.Any())
                            {
                                var errors = result.Errors.Where(e => e.Severity == Severity.Error).ToList();
                                var warnings = result.Errors.Where(e => e.Severity == Severity.Warning).ToList();

                                if (errors.Any())
                                {
                                    var sourceNode = new ValidationErrorNode { Message = $"第 {i + 1} 个来源：" };

                                    foreach (var error in errors)
                                    {
                                        // 检查是否有结构化的错误节点
                                        if (error.CustomState is ValidationErrorNode structuredError)
                                            sourceNode.AddChild(structuredError);
                                        else sourceNode.AddChild(new ValidationErrorNode { Message = error.ErrorMessage });
                                    }

                                    context.AddFailure(new ValidationFailure(context.PropertyPath, sourceNode.Message) { CustomState = sourceNode });
                                }

                                if (warnings.Any())
                                {
                                    var sourceNode = new ValidationErrorNode { Message = $"第 {i + 1} 个来源：" };

                                    foreach (var warning in warnings)
                                    {
                                        // 检查是否有结构化的警告节点
                                        if (warning.CustomState is ValidationErrorNode structuredWarning)
                                            sourceNode.AddChild(structuredWarning);
                                        else sourceNode.AddChild(new ValidationErrorNode { Message = warning.ErrorMessage });
                                    }

                                    context.AddFailure(new ValidationFailure(context.PropertyPath, sourceNode.Message) { CustomState = sourceNode, Severity = Severity.Warning });
                                }
                            }
                        }
                    });
            });
        }
    }
}
