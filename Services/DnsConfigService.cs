using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SNIBypassGUI.Models;
using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Utils.Cryptography;
using SNIBypassGUI.Utils.Extensions;
using static SNIBypassGUI.Consts.AppConsts;
using static SNIBypassGUI.Consts.PathConsts;
using static SNIBypassGUI.Utils.LogManager;

namespace SNIBypassGUI.Services
{
    public class DnsConfigService(IConfigsRepository<DnsConfig> repository, IFactory<DnsConfig> factory) : IConfigSetService<DnsConfig>
    {
        /// <summary>
        /// 所有 DNS 配置的集合。
        /// </summary>
        public RangeObservableCollection<DnsConfig> AllConfigs { get; } = [];

        /// <summary>
        /// 事件，当 DNS 配置被重命名时触发。
        /// </summary>
        public event Action<Guid, string> ConfigRenamed;

        /// <summary>
        /// 事件，当 DNS 配置被移除时触发。
        /// </summary>
        public event Action<Guid> ConfigRemoved;

        /// <summary>
        /// 加载所有 DNS 配置。
        /// </summary>
        public void LoadData()
        {
            var builtInConfigs = repository.LoadAll(BuiltInDnsConfigsPath);
            var userConfigs = repository.LoadAll(UserDnsConfigsPath);
            AllConfigs.ReplaceAll(builtInConfigs.Concat(userConfigs));
        }

        /// <summary>
        /// 新建 DNS 配置。
        /// </summary>
        public DnsConfig CreateDefault() =>
            factory.CreateDefault();

        /// <summary>
        /// 删除 DNS 配置。
        /// </summary>
        public void DeleteConfig(DnsConfig config)
        {
            if (config == null || config.IsBuiltIn) return;
            AllConfigs.Remove(config);
            repository.RemoveById(UserDnsConfigsPath, config.Id);
        }

        /// <summary>
        /// 保存对 DNS 配置的修改。
        /// </summary>
        public Task SaveChangesAsync(DnsConfig config)
        {
            if (config == null || config.IsBuiltIn)
                return Task.CompletedTask;

            var originalConfig = AllConfigs.FirstOrDefault(c => c.Id == config.Id);
            if (originalConfig != null)
            {
                var oldName = originalConfig.ConfigName;
                originalConfig.UpdateFrom(config);

                return Task.Run(() =>
                {
                    repository.Save(UserDnsConfigsPath, originalConfig);

                    if (originalConfig.ConfigName != oldName)
                    {
                        if (Application.Current != null)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                ConfigRenamed?.Invoke(originalConfig.Id, originalConfig.ConfigName);
                            });
                        }
                        else ConfigRenamed?.Invoke(originalConfig.Id, originalConfig.ConfigName);
                    }
                });
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 导入 DNS 配置。
        /// </summary>
        public DnsConfig ImportConfig(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                using var fs = File.OpenRead(path);
                using var br = new BinaryReader(fs);

                var magic = Encoding.ASCII.GetString(br.ReadBytes(4));
                if (magic != SdcMagic)
                {
                    WriteLog("导入 DNS 配置失败，无效的 DNS 配置文件。", LogLevel.Warning);
                    return null;
                }

                int keyLength = br.ReadInt32();
                var keyBytes = br.ReadBytes(keyLength);

                int contentLength = br.ReadInt32();
                var encryptedJsonBytes = br.ReadBytes(contentLength);

                var jsonBytes = CryptoUtils.XorDecrypt(encryptedJsonBytes, keyBytes);
                var json = Encoding.UTF8.GetString(jsonBytes);

                var jObject = JObject.Parse(json);
                var result = DnsConfig.FromJObject(jObject);

                if (result.IsSuccess)
                {
                    var imported = result.Value;
                    if (AllConfigs.Any(p => p.Id == imported.Id))
                        imported.Id = Guid.NewGuid();
                    repository.Save(UserDnsConfigsPath, imported);
                    AllConfigs.Add(imported);
                    return imported;
                }
                else WriteLog($"导入 DNS 配置失败，{result.ErrorMessage}", LogLevel.Warning);
            }
            catch (Exception ex)
            {
                WriteLog("导入 DNS 配置时发生错误。", LogLevel.Error, ex);
            }
            return null;
        }

        /// <summary>
        /// 将指定的 DNS 配置导出到文件。
        /// </summary>
        public void ExportConfig(DnsConfig config, string destinationPath)
        {

            if (config == null ||
                config.IsBuiltIn ||
                string.IsNullOrWhiteSpace(destinationPath))
                return;

            var json = config.ToJObject().ToString(Formatting.None);
            var jsonBytes = Encoding.UTF8.GetBytes(json);

            var random = new Random();
            byte[] keyBytes = new byte[DataKeyLength];
            random.NextBytes(keyBytes);

            var encryptedJsonBytes = CryptoUtils.XorEncrypt(jsonBytes, keyBytes);

            try
            {
                using var fs = File.Create(destinationPath);
                using var bw = new BinaryWriter(fs);

                bw.Write(Encoding.ASCII.GetBytes(SdcMagic));

                bw.Write(DataKeyLength);
                bw.Write(keyBytes);

                bw.Write(encryptedJsonBytes.Length);
                bw.Write(encryptedJsonBytes);
            }
            catch (Exception ex)
            {
                WriteLog("导出 DNS 配置时发生错误。", LogLevel.Error, ex);
            }
        }

        /// <summary>
        /// 压缩用户 DNS 配置文件，移除未使用的配置。
        /// </summary>
        public void Compact() =>
            repository.Compact(UserDnsConfigsPath);
    }
}
