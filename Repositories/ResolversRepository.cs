using System;
using System.Collections.Generic;
using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Models;

namespace SNIBypassGUI.Repositories
{
    public class ResolversRepository(IConfigStorage<Resolver> storage) : IConfigsRepository<Resolver>
    {
        /// <summary>
        /// 从指定文件加载解析器。
        /// </summary>
        public Resolver GetById(string filePath, Guid id) =>
            storage.Load(filePath, id);

        /// <summary>
        /// 从指定文件加载所有解析器。
        /// </summary>
        public IEnumerable<Resolver> LoadAll(string filePath) =>
            storage.LoadAll(filePath);

        /// <summary>
        /// 将解析器保存到指定文件。
        /// </summary>
        public void Save(string filePath, Resolver config) =>
            storage.Save(filePath, config);

        /// <summary>
        /// 从指定文件移除解析器。
        /// </summary>
        public void RemoveById(string filePath, Guid configId) =>
            storage.Remove(filePath, configId);

        /// <summary>
        /// 压缩解析器库文件，移除未使用的解析器。
        /// </summary>
        public void Compact(string filePath) =>
            storage.Compact(filePath);
    }
}
