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
                DomainPattern = "",
                RuleAction = DnsMappingRuleAction.IP,
                TargetIpType = IpAddressSourceType.Static,
                TargetIp = "",
                ResolverId = null,
                QueryDomain = "",
                IpAddressType = IpAddressType.IPv4Only,
                FallbackIpAddresses = []
            };
        }
    }
}
