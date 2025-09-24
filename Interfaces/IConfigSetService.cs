using System;
using System.Threading.Tasks;
using SNIBypassGUI.Common.Extensions;

namespace SNIBypassGUI.Interfaces
{
    public interface IConfigSetService<T> : IFactory<T>
    {
        /// <summary>
        /// 所有 <typeparamref name="T"/> 配置的集合。
        /// </summary>
        public RangeObservableCollection<T> AllConfigs { get; }

        /// <summary>
        /// 事件，当 <typeparamref name="T"/> 配置被更新时触发。
        /// </summary>
        public event Action<Guid> ConfigUpdated;

        /// <summary>
        /// 事件，当 <typeparamref name="T"/> 配置被重命名时触发。
        /// </summary>
        public event Action<Guid, string> ConfigRenamed;

        /// <summary>
        /// 事件，当 <typeparamref name="T"/> 配置被移除时触发。
        /// </summary>
        public event Action<Guid> ConfigRemoved;

        /// <summary>
        /// 加载所有 <typeparamref name="T"/> 配置的数据。
        /// </summary>
        public void LoadData();

        /// <summary>
        /// 删除指定的 <typeparamref name="T"/> 配置。
        /// </summary>
        public void DeleteConfig(T config);

        /// <summary>
        /// 保存对指定的 <typeparamref name="T"/> 配置的更改。
        /// </summary>
        public Task SaveChangesAsync(T original);

        /// <summary>
        /// 导入指定路径的 <typeparamref name="T"/> 配置。
        /// </summary>
        public T ImportConfig(string path);

        /// <summary>
        /// 导出指定的 <typeparamref name="T"/> 配置到指定路径。
        /// </summary>
        public void ExportConfig(T config, string destinationPath);

        /// <summary>
        /// 压缩存储，移除未使用的 <typeparamref name="T"/> 配置。
        /// </summary>
        void Compact();
    }
}
