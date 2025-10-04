using System;
using System.Collections.Generic;
using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Models;

namespace SNIBypassGUI.Repositories
{
    public class ResolverConfigsRepository(IConfigStorage<ResolverConfig> storage) : IConfigsRepository<ResolverConfig>
    {
        /// <summary>
        /// 从指定文件加载解析器配置。
        /// </summary>
        public ResolverConfig GetById(string filePath, Guid id) =>
            storage.Load(filePath, id);

        /// <summary>
        /// 从指定文件加载所有解析器配置。
        /// </summary>
        public IEnumerable<ResolverConfig> LoadAll(string filePath) =>
            storage.LoadAll(filePath);

        /// <summary>
        /// 将解析器配置保存到指定文件。
        /// </summary>
        public void Save(string filePath, ResolverConfig config) =>
            storage.Save(filePath, config);

        /// <summary>
        /// 从指定文件移除解析器配置。
        /// </summary>
        public void RemoveById(string filePath, Guid configId) =>
            storage.Remove(filePath, configId);

        /// <summary>
        /// 压缩解析器配置库文件，移除未使用的解析器配置。
        /// </summary>
        public void Compact(string filePath) =>
            storage.Compact(filePath);
    }
}
