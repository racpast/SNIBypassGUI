using System;
using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Models;

namespace SNIBypassGUI.Factories
{
    public class DnsMappingTableFactory : IFactory<DnsMappingTable>
    {
        /// <summary>
        /// 新建域名映射表。
        /// </summary>
        public DnsMappingTable CreateDefault()
        {
            return new DnsMappingTable
            {
                Id = Guid.NewGuid(),
                IsBuiltIn = false,
                TableName = "新映射表",
                MappingGroups = []
            };
        }
    }
}
