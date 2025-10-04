using System;
using System.Collections.Generic;
using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Models;

namespace SNIBypassGUI.Repositories
{
    public class DnsMappingTablesRepository(IConfigStorage<DnsMappingTable> storage) : IConfigsRepository<DnsMappingTable>
    {
        /// <summary>
        /// 从指定文件加载映射表。
        /// </summary>
        public DnsMappingTable GetById(string filePath, Guid id) =>
            storage.Load(filePath, id);

        /// <summary>
        /// 从指定文件加载所有映射表。
        /// </summary>
        public IEnumerable<DnsMappingTable> LoadAll(string filePath) =>
            storage.LoadAll(filePath);

        /// <summary>
        /// 将映射表保存到指定文件。
        /// </summary>
        public void Save(string filePath, DnsMappingTable table) =>
            storage.Save(filePath, table);

        /// <summary>
        /// 从指定文件移除映射表。
        /// </summary>
        public void RemoveById(string filePath, Guid tableId) =>
            storage.Remove(filePath, tableId);

        /// <summary>
        /// 压缩用户域名映射表库文件，移除未使用的映射表。
        /// </summary>
        public void Compact(string filePath) =>
            storage.Compact(filePath);
    }
}
