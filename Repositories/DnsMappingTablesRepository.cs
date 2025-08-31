using System;
using System.Collections.Generic;
using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Models;
using SNIBypassGUI.Utils.Storage;

namespace SNIBypassGUI.Repositories
{
    public class DnsMappingTablesRepository : IConfigsRepository<DnsMappingTable>
    {
        /// <summary>
        /// 从指定文件加载映射表。
        /// </summary>
        public DnsMappingTable GetById(string filePath, Guid id) =>
            ConfigStorage.Load(filePath, id, DnsMappingTable.FromJObject);

        /// <summary>
        /// 从指定文件加载所有映射表。
        /// </summary>
        public IEnumerable<DnsMappingTable> LoadAll(string filePath) =>
            ConfigStorage.LoadAll(filePath, DnsMappingTable.FromJObject);

        /// <summary>
        /// 将映射表保存到指定文件。
        /// </summary>
        public void Save(string filePath, DnsMappingTable table) =>
            ConfigStorage.Save(filePath, table);

        /// <summary>
        /// 从指定文件移除映射表。
        /// </summary>
        public void RemoveById(string filePath, Guid tableId) =>
            ConfigStorage.Remove(filePath, tableId);

        /// <summary>
        /// 压缩用户域名映射表库文件，移除未使用的映射表。
        /// </summary>
        public void Compact(string filePath) =>
            ConfigStorage.Compact(filePath, DnsMappingTable.FromJObject);
    }
}
