
using System.Linq;
using FluentValidation;
using FluentValidation.Results;
using SNIBypassGUI.Models;
using SNIBypassGUI.ViewModels.Validation;

namespace SNIBypassGUI.Validators
{
    public class DnsMappingGroupValidator : AbstractValidator<DnsMappingGroup>
    {
        public DnsMappingGroupValidator()
        {
            RuleFor(group => group.GroupName)
                .NotEmpty()
                .WithMessage("映射组名称不能为空。");

            RuleFor(group => group.MappingRules)
                .Custom((rules, context) =>
                {
                    var ruleValidator = new DnsMappingRuleValidator();

                    for (int i = 0; i < rules.Count; i++)
                    {
                        var result = ruleValidator.Validate(rules[i]);
                        if (result.Errors.Any())
                        {
                            var errors = result.Errors.Where(e => e.Severity == Severity.Error).ToList();
                            var warnings = result.Errors.Where(e => e.Severity == Severity.Warning).ToList();

                            if (errors.Any())
                            {
                                var ruleNode = new ValidationErrorNode { Message = $"第 {i + 1} 条规则：" };

                                foreach (var error in errors)
                                {
                                    // 检查是否有结构化的错误节点
                                    if (error.CustomState is ValidationErrorNode structuredError)
                                        ruleNode.AddChild(structuredError);
                                    else ruleNode.AddChild(new ValidationErrorNode { Message = error.ErrorMessage });
                                }

                                context.AddFailure(new ValidationFailure(context.PropertyPath, ruleNode.Message) { CustomState = ruleNode });
                            }

                            if (warnings.Any())
                            {
                                var ruleNode = new ValidationErrorNode { Message = $"第 {i + 1} 条规则：" };

                                foreach (var warning in warnings)
                                {
                                    // 检查是否有结构化的警告节点
                                    if (warning.CustomState is ValidationErrorNode structuredWarning)
                                        ruleNode.AddChild(structuredWarning);
                                    else ruleNode.AddChild(new ValidationErrorNode { Message = warning.ErrorMessage });
                                }

                                context.AddFailure(new ValidationFailure(context.PropertyPath, ruleNode.Message) { CustomState = ruleNode, Severity = Severity.Warning });
                            }
                        }
                    }
                });
        }
    }
}
