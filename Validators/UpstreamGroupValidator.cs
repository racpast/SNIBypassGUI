using System.Linq;
using FluentValidation;
using FluentValidation.Results;
using SNIBypassGUI.Models;
using SNIBypassGUI.ViewModels.Validation;

namespace SNIBypassGUI.Validators
{
    public class UpstreamGroupValidator : AbstractValidator<UpstreamGroup>
    {
        public UpstreamGroupValidator()
        {
            RuleFor(group => group.GroupName)
                .NotEmpty().WithMessage("上游组名称不能为空。");

            RuleFor(group => group.ServerSources)
                .Custom((sources, context) =>
                {
                    var sourceValidator = new UpstreamSourceValidator();

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

            RuleFor(group => group.AdditionalDirectives)
                .Must(directives => false)
                .WithSeverity(Severity.Warning)
                .WithMessage("请仔细核对额外指令中的语法，本程序不提供语法校验功能。")
                .When(p => !string.IsNullOrEmpty(p.AdditionalDirectives));
        }
    }
}
