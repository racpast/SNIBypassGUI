using System.Collections.ObjectModel;
using System.Linq;
using SNIBypassGUI.Common;
using SNIBypassGUI.Common.Extensions;
using SNIBypassGUI.Enums;

namespace SNIBypassGUI.Models
{
    /// <summary>
    /// 表示一条供域名映射组使用的映射规则。
    /// </summary>
    public class DnsMappingRule : NotifyPropertyChangedBase
    {
        #region Fields
        private ObservableCollection<string> _domainPatterns = [];
        private DnsMappingRuleAction _ruleAction;
        private ObservableCollection<TargetIpSource> _targetSources = [];
        #endregion

        #region Properties
        /// <summary>
        /// 此规则匹配的域名模式。
        /// </summary>
        public ObservableCollection<string> DomainPatterns
        {
            get => _domainPatterns;
            set => SetProperty(ref _domainPatterns, value);
        }

        /// <summary>
        /// 此规则要执行的操作类型。
        /// </summary>
        public DnsMappingRuleAction RuleAction
        {
            get => _ruleAction;
            set => SetProperty(ref _ruleAction, value);
        }

        /// <summary>
        /// 此规则的目标地址来源列表。
        /// </summary>
        public ObservableCollection<TargetIpSource> TargetSources
        {
            get => _targetSources;
            set => SetProperty(ref _targetSources, value);
        }
        #endregion

        #region Methods
        /// <summary>
        /// 创建当前 <see cref="DnsMappingRule"/> 实例的完整副本。
        /// </summary>
        /// <returns>当前对象的一个完整副本。</returns>
        public DnsMappingRule Clone()
        {
            var clone = new DnsMappingRule
            {
                RuleAction = RuleAction,
                DomainPatterns = [.. DomainPatterns.OrEmpty()],
                TargetSources = [.. TargetSources.OrEmpty().Select(s => s.Clone())]
            };

            return clone;
        }

        /// <summary>
        /// 使用指定的 <see cref="DnsMappingRule"/> 实例的属性值更新当前实例的内容。
        /// </summary>
        public void UpdateFrom(DnsMappingRule source)
        {
            if (source == null) return;
            RuleAction = source.RuleAction;
            DomainPatterns = [.. source.DomainPatterns.OrEmpty()];
            TargetSources = [.. source.TargetSources.OrEmpty().Select(s => s.Clone())];
        }
        #endregion
    }
}