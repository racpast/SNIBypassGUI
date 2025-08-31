using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MaterialDesignThemes.Wpf;
using Newtonsoft.Json.Linq;
using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Utils.Results;
using SNIBypassGUI.Utils.Extensions;

namespace SNIBypassGUI.Models
{
    /// <summary>
    /// 表示一个供站点使用的上游组。
    /// </summary>
    public class UpstreamGroup : NotifyPropertyChangedBase, IStorable
    {

        #region Fields
        private Guid _id;
        private string _groupName;
        private bool _isBuiltIn;
        private ObservableCollection<UpstreamSource> _serverSources;
        private string _additionalDirectives;
        #endregion

        #region Properties
        /// <summary>
        /// 此上游组的唯一标识符。
        /// </summary>
        public Guid Id
        {
            get => _id;
            internal set => SetProperty(ref _id, value);
        }

        /// <summary>
        /// 此上游组的名称。
        /// </summary>
        public string GroupName { get => _groupName; set => SetProperty(ref _groupName, value); }

        /// <summary>
        /// 此上游组是否为内置方案。
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
        /// 此上游组的上游来源列表。
        /// </summary>
        public ObservableCollection<UpstreamSource> ServerSources { get => _serverSources; set => SetProperty(ref _serverSources, value); }

        /// <summary>
        /// 此上游组的附加指令。
        /// </summary>
        public string AdditionalDirectives { get => _additionalDirectives; set => SetProperty(ref _additionalDirectives, value); }

        /// <summary>
        /// 此配置的图标类型，供 UI 使用。
        /// </summary>
        public PackIconKind ListIconKind => IsBuiltIn ? PackIconKind.ArchiveLockOutline : PackIconKind.ServerOutline;

        /// <summary>
        /// 此上游组的类型描述，供 UI 使用。
        /// </summary>
        public string ListTypeDescription => IsBuiltIn ? "(内置)" : "(用户)";
        #endregion

        #region Methods
        /// <summary>
        /// 创建当前 <see cref="UpstreamGroup"/> 实例的完整副本。
        /// </summary>
        /// <returns>当前对象的一个完整副本。</returns>
        public UpstreamGroup Clone()
        {
            var clone = new UpstreamGroup
            {
                Id = Id,
                GroupName = GroupName,
                IsBuiltIn = IsBuiltIn,
                AdditionalDirectives = AdditionalDirectives,
                ServerSources = [.. ServerSources.OrEmpty().Select(source => source.Clone())]
            };

            return clone;
        }

        /// <summary>
        /// 使用指定的 <see cref="UpstreamGroup"/> 实例的属性值更新当前实例的内容。
        /// </summary>
        public void UpdateFrom(UpstreamGroup source)
        {
            if (source == null) return;
            GroupName = source.GroupName;
            IsBuiltIn = source.IsBuiltIn;
            ServerSources = [.. source.ServerSources?.Select(w => w.Clone()).OrEmpty()];
            AdditionalDirectives = source.AdditionalDirectives;
        }

        /// <summary>
        /// 将当前 <see cref="UpstreamGroup"/> 实例转换为 JSON 对象。
        /// </summary>
        public JObject ToJObject()
        {
            var jObject = new JObject
            {
                ["id"] = Id.ToString(),
                ["groupName"] = GroupName.OrDefault(),
                ["isBuiltIn"] = IsBuiltIn,
                ["serverSources"] = new JArray(ServerSources?.Select(w => w.ToJObject()).OrEmpty()),
                ["additionalDirectives"] = AdditionalDirectives.OrDefault()
            };

            return jObject;
        }

        /// <summary>
        /// 从 JSON 对象创建一个新的 <see cref="UpstreamGroup"/> 实例。
        /// </summary>
        public static ParseResult<UpstreamGroup> FromJObject(JObject jObject)
        {
            if (jObject == null)
                return ParseResult<UpstreamGroup>.Failure("JSON 对象为空。");

            if (!jObject.TryGetGuid("id", out Guid id) ||
                !jObject.TryGetString("groupName", out string groupName) ||
                !jObject.TryGetBool("isBuiltIn", out bool isBuiltIn) ||
                !jObject.TryGetArray("serverSources", out IReadOnlyList<JObject> serverSourceObjects) ||
                !jObject.TryGetString("additionalDirectives", out string additionalDirectives))
                return ParseResult<UpstreamGroup>.Failure("一个或多个通用字段缺失或类型错误。");

            ObservableCollection<UpstreamSource> serverSources = [];
            foreach (var item in serverSourceObjects.OfType<JObject>())
            {
                var parsed = UpstreamSource.FromJObject(item);
                if (!parsed.IsSuccess)
                    return ParseResult<UpstreamGroup>.Failure($"解析 serverSources 时出错：{parsed.ErrorMessage}");
                serverSources.Add(parsed.Value);
            }

            var group = new UpstreamGroup
            {
                Id = id,
                GroupName = groupName,
                IsBuiltIn = isBuiltIn,
                ServerSources = serverSources,
                AdditionalDirectives = additionalDirectives
            };

            return ParseResult<UpstreamGroup>.Success(group);
        }
        #endregion
    }
}