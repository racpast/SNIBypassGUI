using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Newtonsoft.Json.Linq;
using SNIBypassGUI.Common.Extensions;
using SNIBypassGUI.Common.Results;
using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Models;

namespace SNIBypassGUI.Common.Mapper
{
    public class DnsMappingGroupMapper(IMapper<DnsMappingRule> dnsMappingRuleMapper) : IMapper<DnsMappingGroup>
    {
        /// <summary>
        /// 将 <see cref="DnsMappingGroup"/> 类型的 <paramref name="dnsMappingGroup"/> 实例转换为 JSON 对象。
        /// </summary>
        public JObject ToJObject(DnsMappingGroup dnsMappingGroup)
        {
            var jObject = new JObject
            {
                ["groupName"] = dnsMappingGroup.GroupName.OrDefault(),
                ["groupIcon"] = dnsMappingGroup.GroupIconBase64.OrDefault(),
                ["isEnabled"] = dnsMappingGroup.IsEnabled,
                ["mappingRules"] = new JArray(dnsMappingGroup.MappingRules?.Select(dnsMappingRuleMapper.ToJObject).OrEmpty())
            };

            return jObject;
        }

        /// <summary>
        /// 从 JSON 对象创建一个新的 <see cref="DnsMappingGroup"/> 实例。
        /// </summary>
        public ParseResult<DnsMappingGroup> FromJObject(JObject jObject)
        {
            if (jObject == null)
                return ParseResult<DnsMappingGroup>.Failure("JSON 对象为空。");

            if (!jObject.TryGetString("groupName", out string groupName) ||
                !jObject.TryGetString("groupIcon", out string groupIconBase64) ||
                !jObject.TryGetBool("isEnabled", out bool isEnabled) ||
                !jObject.TryGetArray("mappingRules", out IReadOnlyList<JObject> mappingRuleObjects))
                return ParseResult<DnsMappingGroup>.Failure("一个或多个通用字段缺失或类型错误。");

            ObservableCollection<DnsMappingRule> mappingRules = [];
            foreach (var item in mappingRuleObjects.OfType<JObject>())
            {
                var parsed = dnsMappingRuleMapper.FromJObject(item);
                if (!parsed.IsSuccess)
                    return ParseResult<DnsMappingGroup>.Failure($"解析 mappingRules 时出错：{parsed.ErrorMessage}");
                mappingRules.Add(parsed.Value);
            }

            var group = new DnsMappingGroup
            {
                GroupName = groupName,
                GroupIconBase64 = groupIconBase64,
                IsEnabled = isEnabled,
                MappingRules = mappingRules
            };

            return ParseResult<DnsMappingGroup>.Success(group);
        }
    }
}
