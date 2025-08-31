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
    public class DnsMappingTablesService(IConfigsRepository<DnsMappingTable> repository, IFactory<DnsMappingTable> factory) : IConfigSetService<DnsMappingTable>
    {
        /// <summary>
        /// 所有映射表的集合。
        /// </summary>
        public RangeObservableCollection<DnsMappingTable> AllConfigs { get; } = [];

        /// <summary>
        /// 事件，当映射表被重命名时触发。
        /// </summary>
        public event Action<Guid, string> ConfigRenamed;

        /// <summary>
        /// 事件，当映射表被移除时触发。
        /// </summary>
        public event Action<Guid> ConfigRemoved;

        /// <summary>
        /// 加载所有映射表。
        /// </summary>
        public void LoadData()
        {
            var builtInTables = repository.LoadAll(BuiltInDnsMappingTablesPath);
            var userTables = repository.LoadAll(UserDnsMappingTablesPath);
            AllConfigs.ReplaceAll(builtInTables.Concat(userTables));
        }

        /// <summary>
        /// 新建映射表。
        /// </summary>
        public DnsMappingTable CreateDefault() =>
            factory.CreateDefault();

        /// <summary>
        /// 删除映射表。
        /// </summary>
        public void DeleteConfig(DnsMappingTable table)
        {
            if (table == null || table.IsBuiltIn) return;
            AllConfigs.Remove(table);
            repository.RemoveById(UserDnsMappingTablesPath, table.Id);
        }

        /// <summary>
        /// 保存对映射表的修改。
        /// </summary>
        public Task SaveChangesAsync(DnsMappingTable table)
        {
            if (table == null || table.IsBuiltIn) return Task.CompletedTask;

            var originalTable = AllConfigs.FirstOrDefault(c => c.Id == table.Id);
            if (originalTable != null)
            {
                var oldName = originalTable.TableName;
                originalTable.UpdateFrom(table);

                return Task.Run(() =>
                {
                    repository.Save(UserDnsMappingTablesPath, originalTable);

                    if (originalTable.TableName != oldName)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            ConfigRenamed?.Invoke(originalTable.Id, originalTable.TableName);
                        });
                    }
                });
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 导入映射表。
        /// </summary>
        public DnsMappingTable ImportConfig(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                using var fs = File.OpenRead(path);
                using var br = new BinaryReader(fs);

                var magic = Encoding.ASCII.GetString(br.ReadBytes(4));
                if (magic != SmtMagic)
                {
                    WriteLog("导入映射表失败，无效的映射表文件。", LogLevel.Warning);
                    return null;
                }

                int keyLength = br.ReadInt32();
                var keyBytes = br.ReadBytes(keyLength);

                int contentLength = br.ReadInt32();
                var encryptedJsonBytes = br.ReadBytes(contentLength);

                var jsonBytes = CryptoUtils.XorDecrypt(encryptedJsonBytes, keyBytes);
                var json = Encoding.UTF8.GetString(jsonBytes);

                var jObject = JObject.Parse(json);
                var result = DnsMappingTable.FromJObject(jObject);

                if (result.IsSuccess)
                {
                    var imported = result.Value;
                    if (AllConfigs.Any(p => p.Id == imported.Id))
                        imported.Id = Guid.NewGuid();
                    repository.Save(UserDnsMappingTablesPath, imported);
                    AllConfigs.Add(imported);
                    return imported;
                }
                else WriteLog($"导入映射表失败，{result.ErrorMessage}", LogLevel.Warning);
            }
            catch (Exception ex)
            {
                WriteLog("导入映射表时发生错误。", LogLevel.Error, ex);
            }
            return null;
        }

        /// <summary>
        /// 将指定的映射表导出到文件。
        /// </summary>
        public void ExportConfig(DnsMappingTable table, string destinationPath)
        {

            if (table == null ||
                table.IsBuiltIn ||
                string.IsNullOrWhiteSpace(destinationPath))
                return;

            var json = table.ToJObject().ToString(Formatting.None);
            var jsonBytes = Encoding.UTF8.GetBytes(json);

            var random = new Random();
            byte[] keyBytes = new byte[DataKeyLength];
            random.NextBytes(keyBytes);

            var encryptedJsonBytes = CryptoUtils.XorEncrypt(jsonBytes, keyBytes);

            try
            {
                using var fs = File.Create(destinationPath);
                using var bw = new BinaryWriter(fs);

                bw.Write(Encoding.ASCII.GetBytes(SmtMagic));

                bw.Write(DataKeyLength);
                bw.Write(keyBytes);

                bw.Write(encryptedJsonBytes.Length);
                bw.Write(encryptedJsonBytes);
            }
            catch (Exception ex)
            {
                WriteLog("导出映射表时发生错误。", LogLevel.Error, ex);
            }
        }

        /// <summary>
        /// 压缩用户映射表文件，移除未使用的配置。
        /// </summary>
        public void Compact() =>
            repository.Compact(UserDnsMappingTablesPath);
    }
}
