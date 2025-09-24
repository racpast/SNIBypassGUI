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
            RuleFor(rule => rule.DomainPatterns)
                .Must(x => x.Any())
                .WithMessage("至少要有一个域名匹配模式。");
            RuleFor(rule => rule.DomainPatterns)
                .Custom((patterns, context) =>
                {
                    for (int i = 0; i < patterns.Count; i++)
                    {
                        var pattern = patterns[i];
                        if (string.IsNullOrWhiteSpace(pattern))
                        {
                            context.AddFailure($"第 {i + 1} 个域名匹配模式：不能为空。");
                            continue;
                        }
                        if (pattern.Trim().StartsWith("/"))
                        {
                            var regexContent = pattern.Substring(1).Trim();
                            if (string.IsNullOrEmpty(regexContent))
                            {
                                context.AddFailure($"第 {i + 1} 个域名匹配模式：正则表达式不能为空。");
                                continue;
                            }
                            int pcreOptions = PcreOptions.PCRE_UTF8 | PcreOptions.PCRE_CASELESS;
                            if (!PcreRegex.TryValidatePattern(regexContent, pcreOptions, out string errorMessage, out int errorOffset))
                                context.AddFailure($"第 {i + 1} 个域名匹配模式：“{regexContent}” 不是有效的正则表达式，在位置 {errorOffset} 存在错误 “{errorMessage}”。");
                        }
                        else
                        {
                            var trimmed = pattern.Trim();
                            if (trimmed.StartsWith(">"))
                            {
                                if (trimmed.IndexOf('>', 1) >= 0)
                                {
                                    context.AddFailure($"第 {i + 1} 个域名匹配模式：子域匹配符号 “>” 只能出现在模式开头。");
                                    continue;
                                }
                                if (trimmed.Length == 1)
                                {
                                    context.AddFailure($"第 {i + 1} 个域名匹配模式：子域匹配符号 “>” 后必须提供域名模式。");
                                    continue;
                                }
                            }
                        }
                    }
                });

            When(rule => rule.RuleAction == DnsMappingRuleAction.IP, () =>
            {
                RuleFor(rule => rule.TargetSources)
                    .Custom((sources, context) =>
                    {
                        if (sources.Count == 0)
                        {
                            context.AddFailure(new ValidationFailure(context.PropertyPath, "此规则未配置任何目标来源，将不会生效。") { Severity = Severity.Warning });
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