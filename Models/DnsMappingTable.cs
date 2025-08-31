using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MaterialDesignThemes.Wpf;
using Newtonsoft.Json.Linq;
using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Utils.Extensions;
using SNIBypassGUI.Utils.Results;

namespace SNIBypassGUI.Models
{
    /// <summary>
    /// 表示一个域名映射表。
    /// </summary>
    public class DnsMappingTable : NotifyPropertyChangedBase, IStorable
    {
        #region Fields
        private Guid _id;
        private string _tableName;
        private bool _isBuiltIn;
        private ObservableCollection<DnsMappingGroup> _mappingGroups;
        #endregion

        #region Properties
        /// <summary>
        /// 此映射表的唯一标识符。
        /// </summary>
        public Guid Id
        {
            get => _id;
            internal set => SetProperty(ref _id, value);
        }

        /// <summary>
        /// 此映射表的名称。
        /// </summary>
        public string TableName
        {
            get => _tableName;
            set => SetProperty(ref _tableName, value);
        }

        /// <summary>
        /// 此映射表是否为内置的。
        /// </summary>
        public bool IsBuiltIn
        {
            get => _isBuiltIn;
            set
            {
                if (SetProperty(ref _isBuiltIn, value))
                {
                    OnPropertyChanged(nameof(ListIconKind));
                    OnPropertyChanged(nameof(ListTypeDescription));
                }
            }
        }

        /// <summary>
        /// 此映射表的图标类型，供 UI 使用。
        /// </summary>
        public PackIconKind ListIconKind
        {
            get
            {
                if (IsBuiltIn) return PackIconKind.ArchiveLockOutline;
                else return PackIconKind.ListBoxOutline;
            }
        }

        /// <summary>
        /// 此映射表的类型描述，供 UI 使用。
        /// </summary>
        public string ListTypeDescription => IsBuiltIn ? "(内置)" : "(用户)";

        /// <summary>
        /// 此映射表的映射组列表。
        /// </summary>
        public ObservableCollection<DnsMappingGroup> MappingGroups
        {
            get => _mappingGroups;
            set => SetProperty(ref _mappingGroups, value);
        }
        #endregion

        #region Methods
        /// <summary>
        /// 创建当前 <see cref="DnsMappingTable"/> 实例的完整副本。
        /// </summary>
        /// <returns>当前对象的一个完整副本。</returns>
        public DnsMappingTable Clone()
        {
            var clone = new DnsMappingTable
            {
                Id = Id,
                TableName = TableName,
                IsBuiltIn = IsBuiltIn,
                MappingGroups = [.. MappingGroups.Select(group => group.Clone())]
            };

            return clone;
        }

        /// <summary>
        /// 使用指定的 <see cref="DnsMappingTable"/> 实例的属性值更新当前实例的内容。
        /// </summary>
        public void UpdateFrom(DnsMappingTable source)
        {
            if (source == null) return;
            TableName = source.TableName;
            IsBuiltIn = source.IsBuiltIn;
            MappingGroups = [.. source.MappingGroups?.Select(w => w.Clone()).OrEmpty()];
        }

        /// <summary>
        /// 将当前 <see cref="DnsMappingTable"/> 实例转换为 JSON 对象。
        /// </summary>
        public JObject ToJObject()
        {
            var jObject = new JObject
            {
                ["id"] = Id.ToString(),
                ["tableName"] = TableName.OrDefault(),
                ["isBuiltIn"] = IsBuiltIn,
                ["mappingGroups"] = new JArray(MappingGroups?.Select(s => s.ToJObject()).OrEmpty())
            };

            return jObject;
        }

        /// <summary>
        /// 从 JSON 对象创建一个新的 <see cref="DnsMappingTable"/> 实例。
        /// </summary>
        public static ParseResult<DnsMappingTable> FromJObject(JObject jObject)
        {
            if (jObject == null)
                return ParseResult<DnsMappingTable>.Failure("JSON 对象为空。");

            if (!jObject.TryGetGuid("id", out Guid id) ||
                !jObject.TryGetString("tableName", out string tableName) ||
                !jObject.TryGetBool("isBuiltIn", out bool isBuiltIn) ||
                !jObject.TryGetArray("mappingGroups", out IReadOnlyList<JObject> mappingGroupObjects))
                return ParseResult<DnsMappingTable>.Failure("一个或多个通用字段缺失或类型错误。");

            ObservableCollection<DnsMappingGroup> mappingGroups = [];
            foreach (var item in mappingGroupObjects.OfType<JObject>())
            {
                var parsed = DnsMappingGroup.FromJObject(item);
                if (!parsed.IsSuccess)
                    return ParseResult<DnsMappingTable>.Failure($"解析 mappingGroups 时出错：{parsed.ErrorMessage}");
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
        #endregion
    }
}
