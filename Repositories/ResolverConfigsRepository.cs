using System;
using System.Collections.Generic;
using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Models;
using SNIBypassGUI.Utils.Storage;

namespace SNIBypassGUI.Repositories
{
    public class ResolverConfigsRepository : IConfigsRepository<ResolverConfig>
    {
        /// <summary>
        /// 从指定文件加载解析器配置。
        /// </summary>
        public ResolverConfig GetById(string filePath, Guid id) =>
            ConfigStorage.Load(filePath, id, ResolverConfig.FromJObject);

        /// <summary>
        /// 从指定文件加载所有解析器配置。
        /// </summary>
        public IEnumerable<ResolverConfig> LoadAll(string filePath) =>
            ConfigStorage.LoadAll(filePath, ResolverConfig.FromJObject);

        /// <summary>
        /// 将解析器配置保存到指定文件。
        /// </summary>
        public void Save(string filePath, ResolverConfig config) =>
            ConfigStorage.Save(filePath, config);

        /// <summary>
        /// 从指定文件移除解析器配置。
        /// </summary>
        public void RemoveById(string filePath, Guid configId) =>
            ConfigStorage.Remove(filePath, configId);

        /// <summary>
        /// 压缩解析器配置库文件，移除未使用的解析器配置。
        /// </summary>
        public void Compact(string filePath) =>
            ConfigStorage.Compact(filePath, ResolverConfig.FromJObject);
    }
}
