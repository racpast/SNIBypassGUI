using System;
using System.Collections.Generic;
using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Models;
using SNIBypassGUI.Utils.Storage;

namespace SNIBypassGUI.Repositories
{
    public class DnsConfigsRepository : IConfigsRepository<DnsConfig>
    {
        /// <summary>
        /// 从指定文件加载配置。
        /// </summary>
        public DnsConfig GetById(string filePath, Guid id) =>
            ConfigStorage.Load(filePath, id, DnsConfig.FromJObject);

        /// <summary>
        /// 从指定文件加载所有配置。
        /// </summary>
        public IEnumerable<DnsConfig> LoadAll(string filePath) =>
            ConfigStorage.LoadAll(filePath, DnsConfig.FromJObject);

        /// <summary>
        /// 将配置保存到指定文件。
        /// </summary>
        public void Save(string filePath, DnsConfig config) =>
            ConfigStorage.Save(filePath, config);

        /// <summary>
        /// 从指定文件移除配置。
        /// </summary>
        public void RemoveById(string filePath, Guid configId) =>
            ConfigStorage.Remove(filePath, configId);

        /// <summary>
        /// 压缩用户 DNS 解析器配置库文件，移除未使用的配置。
        /// </summary>
        public void Compact(string filePath) =>
            ConfigStorage.Compact(filePath, DnsConfig.FromJObject);
    }
}
