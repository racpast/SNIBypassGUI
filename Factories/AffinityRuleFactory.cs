using SNIBypassGUI.Enums;
using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Models;

namespace SNIBypassGUI.Factories
{
    public class AffinityRuleFactory : IFactory<AffinityRule>
    {
        public AffinityRule CreateDefault()
        {
            return new AffinityRule()
            {
                Pattern = string.Empty,
                Mode = AffinityRuleMatchMode.Include
            };
        }
    }
}
