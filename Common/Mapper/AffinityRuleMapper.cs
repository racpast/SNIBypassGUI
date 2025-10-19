using System;
using Newtonsoft.Json.Linq;
using SNIBypassGUI.Common.Results;
using SNIBypassGUI.Common.Extensions;
using SNIBypassGUI.Enums;
using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Models;

namespace SNIBypassGUI.Common.Mapper
{
    public class AffinityRuleMapper : IMapper<AffinityRule>
    {
        /// <summary>
        /// 将 <see cref="AffinityRule"/> 类型的 <paramref name="affinityRule"/> 实例转换为 JSON 对象。
        /// </summary>
        public JObject ToJObject(AffinityRule affinityRule)
        {
            var jObject = new JObject
            {
                ["pattern"] = affinityRule.Pattern.ToString(),
                ["mode"] = affinityRule.Mode.ToString(),
            };

            return jObject;
        }

        /// <summary>
        /// 从 JSON 对象创建一个新的 <see cref="AffinityRule"/> 实例。
        /// </summary>
        public ParseResult<AffinityRule> FromJObject(JObject jObject)
        {
            if (jObject == null)
                return ParseResult<AffinityRule>.Failure("JSON 对象为空。");
            try
            {
                if (!jObject.TryGetString("pattern", out string pattern) ||
                    !jObject.TryGetEnum("mode", out AffinityRuleMatchMode mode))
                    return ParseResult<AffinityRule>.Failure("一个或多个通用字段缺失或类型错误。");  
                var rule = new AffinityRule
                {
                    Pattern = pattern,
                    Mode = mode
                };

                return ParseResult<AffinityRule>.Success(rule);
            }
            catch (Exception ex)
            {
                return ParseResult<AffinityRule>.Failure($"解析 AffinityRule 时遇到异常：{ex.Message}");
            }
        }
    }
}
