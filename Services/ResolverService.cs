using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SNIBypassGUI.Common.Cryptography;
using SNIBypassGUI.Common.Extensions;
using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Models;
using static SNIBypassGUI.Consts.AppConsts;
using static SNIBypassGUI.Consts.PathConsts;
using static SNIBypassGUI.Common.LogManager;

namespace SNIBypassGUI.Services
{
    public class ResolverService(
        IConfigsRepository<Resolver> repository, 
        IFactory<Resolver> factory, 
        IMapper<Resolver> mapper)
        : IConfigSetService<Resolver>
    {
        /// <summary>
        /// 所有 DNS 解析器的集合。
        /// </summary>
        public RangeObservableCollection<Resolver> AllConfigs { get; } = [];

        /// <summary>
        /// 事件，当 DNS 解析器被重命名时触发。
        /// </summary>
        public event Action<Guid, string> ConfigRenamed;

        /// <summary>
        /// 事件，当 DNS 解析器被更新时触发。
        /// </summary>
        public event Action<Guid> ConfigUpdated;

        /// <summary>
        /// 事件，当 DNS 解析器被移除时触发。
        /// </summary>
        public event Action<Guid> ConfigRemoved;

        /// <summary>
        /// 加载所有 DNS 解析器。
        /// </summary>
        public void LoadData()
        {
            var builtInConfigs = repository.LoadAll(BuiltInResolversPath);
            var userConfigs = repository.LoadAll(UserResolversPath);
            AllConfigs.ReplaceAll(builtInConfigs.Concat(userConfigs));
        }

        /// <summary>
        /// 新建 DNS 解析器。
        /// </summary>
        public Resolver CreateDefault() =>
            factory.CreateDefault();

        /// <summary>
        /// 删除 DNS 解析器。
        /// </summary>
        public void DeleteConfig(Resolver config)
        {
            if (config == null || config.IsBuiltIn) return;
            AllConfigs.Remove(config);
            repository.RemoveById(UserResolversPath, config.Id);
            ConfigRemoved?.Invoke(config.Id); // 广播
        }

        /// <summary>
        /// 保存对 DNS 解析器的修改。
        /// </summary>
        public Task SaveChangesAsync(Resolver config)
        {
            if (config == null || config.IsBuiltIn) return Task.CompletedTask;

            var originalConfig = AllConfigs.FirstOrDefault(c => c.Id == config.Id);
            if (originalConfig != null)
            {
                var oldName = originalConfig.ResolverName;
                originalConfig.UpdateFrom(config);

                return Task.Run(() =>
                {
                    repository.Save(UserResolversPath, originalConfig);

                    ConfigUpdated?.Invoke(originalConfig.Id);

                    if (originalConfig.ResolverName != oldName)
                    {
                        if (Application.Current != null)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                ConfigRenamed?.Invoke(originalConfig.Id, originalConfig.ResolverName);
                            });
                        }
                        else ConfigRenamed?.Invoke(originalConfig.Id, originalConfig.ResolverName);
                    }
                });
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 导入 DNS 解析器。
        /// </summary>
        public Resolver ImportConfig(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                using var fs = File.OpenRead(path);
                using var br = new BinaryReader(fs);

                var magic = Encoding.ASCII.GetString(br.ReadBytes(4));
                if (magic != SrcMagic)
                {
                    WriteLog("导入解析器失败，无效的解析器文件。", LogLevel.Warning);
                    return null;
                }

                int keyLength = br.ReadInt32();
                var keyBytes = br.ReadBytes(keyLength);

                int contentLength = br.ReadInt32();
                var encryptedJsonBytes = br.ReadBytes(contentLength);

                var jsonBytes = CryptoUtils.XorDecrypt(encryptedJsonBytes, keyBytes);
                var json = Encoding.UTF8.GetString(jsonBytes);

                var jObject = JObject.Parse(json);
                var result = mapper.FromJObject(jObject);

                if (result.IsSuccess)
                {
                    var imported = result.Value;
                    if (AllConfigs.Any(p => p.Id == imported.Id))
                        imported.Id = Guid.NewGuid();
                    repository.Save(UserResolversPath, imported);
                    AllConfigs.Add(imported);
                    return imported;
                }
                else
                    WriteLog($"导入解析器失败，{result.ErrorMessage}", LogLevel.Warning);
            }
            catch (Exception ex)
            {
                WriteLog("导入解析器时发生错误。", LogLevel.Error, ex);
            }
            return null;
        }

        /// <summary>
        /// 将指定的 DNS 解析器导出到文件。
        /// </summary>
        public void ExportConfig(Resolver config, string destinationPath)
        {

            if (config == null ||
                config.IsBuiltIn ||
                string.IsNullOrWhiteSpace(destinationPath))
                return;

            var json = mapper.ToJObject(config).ToString(Formatting.None);
            var jsonBytes = Encoding.UTF8.GetBytes(json);

            var random = new Random();
            byte[] keyBytes = new byte[DataKeyLength];
            random.NextBytes(keyBytes);

            var encryptedJsonBytes = CryptoUtils.XorEncrypt(jsonBytes, keyBytes);

            try
            {
                using var fs = File.Create(destinationPath);
                using var bw = new BinaryWriter(fs);

                bw.Write(Encoding.ASCII.GetBytes(SrcMagic));

                bw.Write(DataKeyLength);
                bw.Write(keyBytes);

                bw.Write(encryptedJsonBytes.Length);
                bw.Write(encryptedJsonBytes);
            }
            catch (Exception ex)
            {
                WriteLog("导出解析器时发生错误。", LogLevel.Error, ex);
            }
        }

        /// <summary>
        /// 压缩用户 DNS 解析器库文件，移除未使用的配置。
        /// </summary>
        public void Compact() =>
            repository.Compact(UserResolversPath);
    }
}
