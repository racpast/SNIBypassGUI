using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows.Media.Imaging;
using Newtonsoft.Json.Linq;
using SNIBypassGUI.Utils.Extensions;
using SNIBypassGUI.Utils.IO;
using SNIBypassGUI.Utils.Results;


namespace SNIBypassGUI.Models
{
    /// <summary>
    /// 表示一个供域名映射表使用的映射组。
    /// </summary>
    public class DnsMappingGroup : NotifyPropertyChangedBase
    {
        #region Fields
        private string _groupIconBase64;
        private string _groupName;
        private bool _isEnabled;
        private ObservableCollection<DnsMappingRule> _mappingRules;
        private bool _isExpanded;
        #endregion

        #region Properties
        /// <summary>
        /// Base64 编码的规则组图标字符串。
        /// </summary>
        public string GroupIconBase64
        {
            get => _groupIconBase64;
            set
            {
                if (SetProperty(ref _groupIconBase64, value))
                {
                    OnPropertyChanged(nameof(GroupIcon));
                    OnPropertyChanged(nameof(HasGroupIcon));
                }
            }
        }

        /// <summary>
        /// 此规则组是否包含图标。
        /// </summary>
        public bool HasGroupIcon => GroupIcon != null;

        /// <summary>
        /// 规则组图标，供 UI 使用。
        /// </summary>
        public BitmapImage GroupIcon => FileUtils.Base64ToBitmapImage(GroupIconBase64);

        /// <summary>
        /// 此规则组的名称。
        /// </summary>
        public string GroupName
        {
            get => _groupName;
            set
            {
                if (SetProperty(ref _groupName, value))
                    OnPropertyChanged(nameof(DisplayText));
            }
        }

        /// <summary>
        /// 此规则组是否启用。
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        /// <summary>
        /// 此映射组包含的映射规则列表。
        /// </summary>
        public ObservableCollection<DnsMappingRule> MappingRules
        {
            get => _mappingRules;
            set
            {
                if (_mappingRules != null)
                    _mappingRules.CollectionChanged -= OnMappingRulesChanged;

                if (SetProperty(ref _mappingRules, value))
                {
                    if (_mappingRules != null)
                    {
                        foreach (var rule in _mappingRules)
                            rule.Parent = this;
                        _mappingRules.CollectionChanged += OnMappingRulesChanged;
                    }
                }
            }
        }

        /// <summary>
        /// 此映射组在列表中的展示文本，供 UI 使用。
        /// </summary>
        public string DisplayText { get => $"{GroupName} ({MappingRules?.Count ?? 0})"; }

        /// <summary>
        /// 此映射组是否在展开，供 UI 使用。
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }
        #endregion

        #region Methods
        private void OnMappingRulesChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (DnsMappingRule rule in e.NewItems) rule.Parent = this;

            if (e.OldItems != null)
                foreach (DnsMappingRule rule in e.OldItems)
                    if (rule.Parent == this) rule.Parent = null;

            OnPropertyChanged(nameof(DisplayText));
        }

        /// <summary>
        /// 创建当前 <see cref="DnsMappingGroup"/> 实例的完整副本。
        /// </summary>
        /// <returns>当前对象的一个完整副本。</returns>
        public DnsMappingGroup Clone()
        {
            var clone = new DnsMappingGroup
            {
                IsEnabled = IsEnabled,
                GroupName = GroupName,
                GroupIconBase64 = GroupIconBase64,
                IsExpanded = IsExpanded,
                MappingRules = [.. MappingRules.Select(rule => rule.Clone())]
            };

            // 重建父子关系
            foreach (var ruleClone in clone.MappingRules)
                ruleClone.Parent = clone;

            return clone;
        }

        /// <summary>
        /// 使用指定的 <see cref="DnsMappingGroup"/> 实例的属性值更新当前实例的内容。
        /// </summary>
        public void UpdateFrom(DnsMappingGroup source)
        {
            if (source == null) return;
            GroupName = source.GroupName;
            GroupIconBase64 = source.GroupIconBase64;
            IsEnabled = source.IsEnabled;
            MappingRules = [.. source.MappingRules?.Select(w => w.Clone()).OrEmpty()];
        }

        /// <summary>
        /// 将当前 <see cref="DnsMappingGroup"/> 实例转换为 JSON 对象。
        /// </summary>
        public JObject ToJObject()
        {
            var jObject = new JObject
            {
                ["groupName"] = GroupName.OrDefault(),
                ["groupIcon"] = GroupIconBase64.OrDefault(),
                ["isEnabled"] = IsEnabled,
                ["mappingRules"] = new JArray(MappingRules?.Select(s => s.ToJObject()).OrEmpty())
            };

            return jObject;
        }

        /// <summary>
        /// 从 JSON 对象创建一个新的 <see cref="DnsMappingGroup"/> 实例。
        /// </summary>
        public static ParseResult<DnsMappingGroup> FromJObject(JObject jObject)
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
                var parsed = DnsMappingRule.FromJObject(item);
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
        #endregion
    }
}
