using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Models;
using SNIBypassGUI.Utils.Cryptography;
using SNIBypassGUI.Utils.Extensions;
using static SNIBypassGUI.Consts.AppConsts;
using static SNIBypassGUI.Consts.PathConsts;
using static SNIBypassGUI.Utils.LogManager;

namespace SNIBypassGUI.Services
{
    public class UpstreamGroupService(IConfigsRepository<UpstreamGroup> repository, IFactory<UpstreamGroup> factory) : IConfigSetService<UpstreamGroup>
    {
        /// <summary>
        /// 所有上游组的集合。
        /// </summary>
        public RangeObservableCollection<UpstreamGroup> AllConfigs { get; } = [];

        /// <summary>
        /// 事件，当上游组被重命名时触发。
        /// </summary>
        public event Action<Guid, string> ConfigRenamed;

        /// <summary>
        /// 事件，当上游组被移除时触发。
        /// </summary>
        public event Action<Guid> ConfigRemoved;

        /// <summary>
        /// 加载所有上游组。
        /// </summary>
        public void LoadData()
        {
            var builtInGroups = repository.LoadAll(BuiltInUpstreamGroupsPath);
            var userGroups = repository.LoadAll(UserUpstreamGroupsPath);
            AllConfigs.ReplaceAll(builtInGroups.Concat(userGroups));
        }

        /// <summary>
        /// 新建上游组。
        /// </summary>
        public UpstreamGroup CreateDefault() =>
            factory.CreateDefault();

        /// <summary>
        /// 删除上游组。
        /// </summary>
        public void DeleteConfig(UpstreamGroup group)
        {
            if (group == null || group.IsBuiltIn) return;
            AllConfigs.Remove(group);
            repository.RemoveById(UserUpstreamGroupsPath, group.Id);
            ConfigRemoved?.Invoke(group.Id); // 广播
        }

        /// <summary>
        /// 保存对上游组的修改。
        /// </summary>
        public Task SaveChangesAsync(UpstreamGroup group)
        {
            if (group == null || group.IsBuiltIn) return Task.CompletedTask;

            var originalGroup = AllConfigs.FirstOrDefault(c => c.Id == group.Id);
            if (originalGroup != null)
            {
                var oldName = originalGroup.GroupName;
                originalGroup.UpdateFrom(group);

                return Task.Run(() =>
                {
                    repository.Save(UserUpstreamGroupsPath, originalGroup);

                    if (originalGroup.GroupName != oldName)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            ConfigRenamed?.Invoke(originalGroup.Id, originalGroup.GroupName);
                        });
                    }
                });
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 导入上游组。
        /// </summary>
        public UpstreamGroup ImportConfig(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                using var fs = File.OpenRead(path);
                using var br = new BinaryReader(fs);

                var magic = Encoding.ASCII.GetString(br.ReadBytes(4));
                if (magic != SugMagic)
                {
                    WriteLog("导入上游组失败，无效的上游组文件。", LogLevel.Warning);
                    return null;
                }

                int keyLength = br.ReadInt32();
                var keyBytes = br.ReadBytes(keyLength);

                int contentLength = br.ReadInt32();
                var encryptedJsonBytes = br.ReadBytes(contentLength);

                var jsonBytes = CryptoUtils.XorDecrypt(encryptedJsonBytes, keyBytes);
                var json = Encoding.UTF8.GetString(jsonBytes);

                var jObject = JObject.Parse(json);
                var result = UpstreamGroup.FromJObject(jObject);

                if (result.IsSuccess)
                {
                    var imported = result.Value;
                    if (AllConfigs.Any(p => p.Id == imported.Id))
                        imported.Id = Guid.NewGuid();
                    repository.Save(UserUpstreamGroupsPath, imported);
                    AllConfigs.Add(imported);
                    return imported;
                }
                else
                    WriteLog($"导入上游组失败，{result.ErrorMessage}", LogLevel.Warning);
            }
            catch (Exception ex)
            {
                WriteLog("导入上游组时发生错误。", LogLevel.Error, ex);
            }
            return null;
        }

        /// <summary>
        /// 将指定的上游组导出到文件。
        /// </summary>
        public void ExportConfig(UpstreamGroup group, string destinationPath)
        {

            if (group == null ||
                group.IsBuiltIn ||
                string.IsNullOrWhiteSpace(destinationPath))
                return;

            var json = group.ToJObject().ToString(Formatting.None);
            var jsonBytes = Encoding.UTF8.GetBytes(json);

            var random = new Random();
            byte[] keyBytes = new byte[DataKeyLength];
            random.NextBytes(keyBytes);

            var encryptedJsonBytes = CryptoUtils.XorEncrypt(jsonBytes, keyBytes);

            try
            {
                using var fs = File.Create(destinationPath);
                using var bw = new BinaryWriter(fs);

                bw.Write(Encoding.ASCII.GetBytes(SugMagic));

                bw.Write(DataKeyLength);
                bw.Write(keyBytes);

                bw.Write(encryptedJsonBytes.Length);
                bw.Write(encryptedJsonBytes);
            }
            catch (Exception ex)
            {
                WriteLog("导出上游组时发生错误。", LogLevel.Error, ex);
            }
        }

        /// <summary>
        /// 压缩用户上游组库文件，移除未使用的配置。
        /// </summary>
        public void Compact() =>
            repository.Compact(UserUpstreamGroupsPath);
    }

}
