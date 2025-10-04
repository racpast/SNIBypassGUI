using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Models;

namespace SNIBypassGUI.Factories
{
    public class DnsMappingGroupFactory : IFactory<DnsMappingGroup>
    {
        /// <summary>
        /// 新建映射组。
        /// </summary>
        public DnsMappingGroup CreateDefault()
        {
            return new DnsMappingGroup
            {
                GroupName = "新映射组",
                IsEnabled = true,
                GroupIconBase64 = ""
            };
        }
    }
}
