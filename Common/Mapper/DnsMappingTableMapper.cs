using System;
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
    public class DnsMappingTableMapper(IMapper<DnsMappingGroup> dnsMappingGroupMapper) : IMapper<DnsMappingTable>
    {
        /// <summary>
        /// 将 <see cref="DnsMappingTable"/> 类型的 <paramref name="dnsMappingTable"/> 实例转换为 JSON 对象。
        /// </summary>
        public JObject ToJObject(DnsMappingTable dnsMappingTable)
        {
            var jObject = new JObject
            {
                ["id"] = dnsMappingTable.Id.ToString(),
                ["tableName"] = dnsMappingTable.TableName.OrDefault(),
                ["isBuiltIn"] = dnsMappingTable.IsBuiltIn,
                ["mappingGroups"] = new JArray(dnsMappingTable.MappingGroups?.Select(dnsMappingGroupMapper.ToJObject).OrEmpty())
            };

            return jObject;
        }

        /// <summary>
        /// 从 JSON 对象创建一个新的 <see cref="DnsMappingTable"/> 实例。
        /// </summary>
        public ParseResult<DnsMappingTable> FromJObject(JObject jObject)
        {
            if (jObject == null)
                return ParseResult<DnsMappingTable>.Failure("JSON 对象为空。");
            try
            {
                if (!jObject.TryGetGuid("id", out Guid id) ||
                    !jObject.TryGetString("tableName", out string tableName) ||
                    !jObject.TryGetBool("isBuiltIn", out bool isBuiltIn) ||
                    !jObject.TryGetArray("mappingGroups", out IReadOnlyList<JObject> mappingGroupObjects))
                    return ParseResult<DnsMappingTable>.Failure("一个或多个通用字段缺失或类型错误。");

                ObservableCollection<DnsMappingGroup> mappingGroups = [];
                foreach (var item in mappingGroupObjects.OfType<JObject>())
                {
                    var parsed = dnsMappingGroupMapper.FromJObject(item);
                    if (!parsed.IsSuccess)
                        return ParseResult<DnsMappingTable>.Failure($"解析 mappingGroups 时遇到异常：{parsed.ErrorMessage}");
                    mappingGroups.Add(parsed.Value);
                }

                var table = new DnsMappingTable
                {
                    Id = id,
                    TableName = tableName,
                    IsBuiltIn = isBuiltIn,
                    MappingGroups = mappingGroups
                };

                return ParseResult<DnsMappingTable>.Success(table);
            }
            catch (Exception ex)
            {
                return ParseResult<DnsMappingTable>.Failure($"解析 DnsMappingTable 时遇到异常：{ex.Message}");
            }
        }
    }
}
