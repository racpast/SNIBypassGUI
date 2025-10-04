using System;
using System.Collections.Generic;
using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Models;

namespace SNIBypassGUI.Repositories
{
    public class DnsConfigsRepository(IConfigStorage<DnsConfig> storage) : IConfigsRepository<DnsConfig>
    {
        /// <summary>
        /// 从指定文件加载配置。
        /// </summary>
        public DnsConfig GetById(string filePath, Guid id) =>
            storage.Load(filePath, id);

        /// <summary>
        /// 从指定文件加载所有配置。
        /// </summary>
        public IEnumerable<DnsConfig> LoadAll(string filePath) =>
            storage.LoadAll(filePath);

        /// <summary>
        /// 将配置保存到指定文件。
        /// </summary>
        public void Save(string filePath, DnsConfig config) =>
            storage.Save(filePath, config);

        /// <summary>
        /// 从指定文件移除配置。
        /// </summary>
        public void RemoveById(string filePath, Guid configId) =>
            storage.Remove(filePath, configId);

        /// <summary>
        /// 压缩用户 DNS 解析器配置库文件，移除未使用的配置。
        /// </summary>
        public void Compact(string filePath) =>
            storage.Compact(filePath);
    }
}
