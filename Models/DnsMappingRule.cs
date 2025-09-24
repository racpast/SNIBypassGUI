using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Newtonsoft.Json.Linq;
using SNIBypassGUI.Common;
using SNIBypassGUI.Common.Extensions;
using SNIBypassGUI.Common.Results;
using SNIBypassGUI.Enums;

namespace SNIBypassGUI.Models
{
    /// <summary>
    /// 表示一条供域名映射组使用的映射规则。
    /// </summary>
    public class DnsMappingRule : NotifyPropertyChangedBase
    {
        #region Fields
        private ObservableCollection<string> _domainPatterns;
        private DnsMappingRuleAction _ruleAction;
        private ObservableCollection<TargetIpSource> _targetSources;
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

        /// <summary>
        /// 将当前 <see cref="DnsMappingRule"/> 实例转换为 JSON 对象。
        /// </summary>
        public JObject ToJObject()
        {
            var jObject = new JObject
            {
                ["domainPatterns"] = new JArray(DomainPatterns.OrEmpty()),
                ["ruleAction"] = RuleAction.ToString()
            };

            if (RuleAction == DnsMappingRuleAction.IP)
                jObject["targetSources"] = new JArray(TargetSources?.Select(s => s.ToJObject()).OrEmpty());

            return jObject;
        }

        /// <summary>
        /// 从 JSON 对象创建一个新的 <see cref="DnsMappingRule"/> 实例。
        /// </summary>
        public static ParseResult<DnsMappingRule> FromJObject(JObject jObject)
        {
            if (jObject == null)
                return ParseResult<DnsMappingRule>.Failure("JSON 对象为空。");

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
                    var parsed = TargetIpSource.FromJObject(item);
                    if (!parsed.IsSuccess)
                        return ParseResult<DnsMappingRule>.Failure($"解析 targetSources 时出错：{parsed.ErrorMessage}");
                    targetSources.Add(parsed.Value);
                }
                rule.TargetSources = targetSources;
            }

            return ParseResult<DnsMappingRule>.Success(rule);
        }
        #endregion
    }
}