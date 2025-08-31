using System;
using System.Collections.Generic;
using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Models;
using SNIBypassGUI.Utils.Storage;

namespace SNIBypassGUI.Repositories
{
    public class UpstreamGroupsRepository : IConfigsRepository<UpstreamGroup>
    {
        /// <summary>
        /// 从指定文件加载上游组。
        /// </summary>
        public UpstreamGroup GetById(string filePath, Guid id) =>
            ConfigStorage.Load(filePath, id, UpstreamGroup.FromJObject);

        /// <summary>
        /// 从指定文件加载所有上游组。
        /// </summary>
        public IEnumerable<UpstreamGroup> LoadAll(string filePath) =>
            ConfigStorage.LoadAll(filePath, UpstreamGroup.FromJObject);

        /// <summary>
        /// 将上游组保存到指定文件。
        /// </summary>
        public void Save(string filePath, UpstreamGroup group) =>
            ConfigStorage.Save(filePath, group);

        /// <summary>
        /// 从指定文件移除上游组。
        /// </summary>
        public void RemoveById(string filePath, Guid groupId) =>
            ConfigStorage.Remove(filePath, groupId);

        /// <summary>
        /// 压缩上游组库文件，移除未使用的上游组。
        /// </summary>
        public void Compact(string filePath) =>
            ConfigStorage.Compact(filePath, UpstreamGroup.FromJObject);
    }

}
