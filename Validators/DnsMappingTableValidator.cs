using System.Linq;
using FluentValidation;
using FluentValidation.Results;
using SNIBypassGUI.Models;
using SNIBypassGUI.ViewModels.Validation;

namespace SNIBypassGUI.Validators
{
    public class DnsMappingTableValidator : AbstractValidator<DnsMappingTable>
    {
        public DnsMappingTableValidator()
        {
            RuleFor(table => table.TableName)
                .NotEmpty().WithMessage("映射表名称不能为空。");

            RuleFor(table => table.MappingGroups)
            .Custom((groups, context) =>
            {
                var groupValidator = new DnsMappingGroupValidator();

                for (int i = 0; i < groups.Count; i++)
                {
                    var result = groupValidator.Validate(groups[i]);

                    if (result.Errors.Any())
                    {
                        var errors = result.Errors.Where(e => e.Severity == Severity.Error).ToList();
                        var warnings = result.Errors.Where(e => e.Severity == Severity.Warning).ToList();

                        if (errors.Any())
                        {
                            var groupNode = new ValidationErrorNode { Message = $"映射组 “{groups[i].GroupName}”：" };

                            foreach (var error in errors)
                            {
                                // 检查是否有结构化的错误节点
                                if (error.CustomState is ValidationErrorNode structuredError)
                                    groupNode.AddChild(structuredError);
                                else groupNode.AddChild(new ValidationErrorNode { Message = error.ErrorMessage });
                            }

                            context.AddFailure(new ValidationFailure(context.PropertyPath, groupNode.Message) { CustomState = groupNode });
                        }

                        if (warnings.Any())
                        {
                            var groupNode = new ValidationErrorNode { Message = $"映射组 “{groups[i].GroupName}”：" };

                            foreach (var warning in warnings)
                            {
                                // 检查是否有结构化的警告节点
                                if (warning.CustomState is ValidationErrorNode structuredWarning)
                                    groupNode.AddChild(structuredWarning);
                                else groupNode.AddChild(new ValidationErrorNode { Message = warning.ErrorMessage });
                            }

                            context.AddFailure(new ValidationFailure(context.PropertyPath, groupNode.Message) { CustomState = groupNode, Severity = Severity.Warning });
                        }
                    }
                }
            });
        }
    }
}
