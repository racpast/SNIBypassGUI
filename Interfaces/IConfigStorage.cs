using System;
using System.Collections.Generic;

namespace SNIBypassGUI.Interfaces
{
    public interface IConfigStorage<T> where T : IStorable
    {
        /// <summary>
        /// 压缩指定的 <typeparamref name="T"/> 库文件，移除未使用的 <typeparamref name="T"/>。
        /// </summary>
        void Compact(string filePath);

        /// <summary>
        /// 从指定文件移除 <typeparamref name="T"/>。
        /// </summary>
        void Remove(string filePath, Guid configId);

        /// <summary>
        /// 将 <typeparamref name="T"/> 保存到指定文件。
        /// </summary>
        void Save(string filePath, T config);

        /// <summary>
        /// 从指定文件加载 <typeparamref name="T"/>。
        /// </summary>
        T Load(string filePath, Guid configId);

        /// <summary>
        /// 从指定文件加载所有 <typeparamref name="T"/>。
        /// </summary>
        IEnumerable<T> LoadAll(string filePath);
    }
}
