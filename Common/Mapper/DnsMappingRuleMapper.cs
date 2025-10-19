using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Newtonsoft.Json.Linq;
using SNIBypassGUI.Common.Extensions;
using SNIBypassGUI.Common.Results;
using SNIBypassGUI.Enums;
using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Models;

namespace SNIBypassGUI.Common.Mapper
{
    public class DnsMappingRuleMapper(IMapper<TargetIpSource> targetIpSourceMapper) : IMapper<DnsMappingRule>
    {
        /// <summary>
        /// 将 <see cref="DnsMappingRule"/> 类型的 <paramref name="dnsMappingRule"/> 实例转换为 JSON 对象。
        /// </summary>
        public JObject ToJObject(DnsMappingRule dnsMappingRule)
        {
            var jObject = new JObject
            {
                ["domainPatterns"] = new JArray(dnsMappingRule.DomainPatterns.OrEmpty()),
                ["ruleAction"] = dnsMappingRule.RuleAction.ToString()
            };

            if (dnsMappingRule.RuleAction == DnsMappingRuleAction.IP)
                jObject["targetSources"] = new JArray(dnsMappingRule.TargetSources?.Select(targetIpSourceMapper.ToJObject).OrEmpty());

            return jObject;
        }

        /// <summary>
        /// 从 JSON 对象创建一个新的 <see cref="DnsMappingRule"/> 实例。
        /// </summary>
        public ParseResult<DnsMappingRule> FromJObject(JObject jObject)
        {
            if (jObject == null)
                return ParseResult<DnsMappingRule>.Failure("JSON 对象为空。");

            try
            {
                if (!jObject.TryGetArray("domainPatterns", out IReadOnlyList<string> domainPatterns) ||
                    !jObject.TryGetEnum("ruleAction", out DnsMappingRuleAction ruleAction))
                    return ParseResult<DnsMappingRule>.Failure("一个或多个通用字段缺失或类型错误。");

                var rule = new DnsMappingRule
                {
                    DomainPatterns = [.. domainPatterns],
                    RuleAction = ruleAction
                };

                if (ruleAction == DnsMappingRuleAction.IP)
                {
                    if (!jObject.TryGetArray("targetSources", out IReadOnlyList<JObject> targetSourceObjects))
                        return ParseResult<DnsMappingRule>.Failure("返回地址动作所需的字段缺失或类型错误。");
                    ObservableCollection<TargetIpSource> targetSources = [];
                    foreach (var item in targetSourceObjects.OfType<JObject>())
                    {
                        var parsed = targetIpSourceMapper.FromJObject(item);
                        if (!parsed.IsSuccess)
                            return ParseResult<DnsMappingRule>.Failure($"解析 targetSources 时遇到异常：{parsed.ErrorMessage}");
                        targetSources.Add(parsed.Value);
                    }
                    rule.TargetSources = targetSources;
                }

                return ParseResult<DnsMappingRule>.Success(rule);
            }
            catch (Exception ex)
            {
                return ParseResult<DnsMappingRule>.Failure($"解析 DnsMappingRule 时遇到异常：{ex.Message}");
            }
        }
    }
}
