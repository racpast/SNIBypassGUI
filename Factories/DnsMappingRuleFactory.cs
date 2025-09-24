using SNIBypassGUI.Enums;
using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Models;

namespace SNIBypassGUI.Factories
{
    public class DnsMappingRuleFactory : IFactory<DnsMappingRule>
    {
        /// <summary>
        /// 新建映射规则。
        /// </summary>
        public DnsMappingRule CreateDefault()
        {
            return new DnsMappingRule
            {
                DomainPatterns = [],
                RuleAction = DnsMappingRuleAction.IP,
                TargetSources = []
            };
        }
    }
}
