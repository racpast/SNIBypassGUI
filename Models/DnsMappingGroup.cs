using System.Collections.ObjectModel;
using System.Linq;
using SNIBypassGUI.Common;
using SNIBypassGUI.Common.Extensions;

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
        private ObservableCollection<DnsMappingRule> _mappingRules = [];
        #endregion

        #region Properties
        /// <summary>
        /// Base64 编码的规则组图标字符串。
        /// </summary>
        public string GroupIconBase64
        {
            get => _groupIconBase64;
            set => SetProperty(ref _groupIconBase64, value);
        }

        /// <summary>
        /// 此规则组的名称。
        /// </summary>
        public string GroupName
        {
            get => _groupName;
            set => SetProperty(ref _groupName, value);
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
            set => SetProperty(ref _mappingRules, value);
        }
        #endregion

        #region Methods
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
                MappingRules = [.. MappingRules.Select(rule => rule.Clone())]
            };

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
        #endregion
    }
}