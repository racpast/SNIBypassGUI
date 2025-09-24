using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Models;
using SNIBypassGUI.Common.Cryptography;
using SNIBypassGUI.Common.Extensions;
using static SNIBypassGUI.Consts.AppConsts;
using static SNIBypassGUI.Consts.PathConsts;
using static SNIBypassGUI.Common.LogManager;

namespace SNIBypassGUI.Services
{
    public class DnsMappingTablesService(IConfigsRepository<DnsMappingTable> repository, IFactory<DnsMappingTable> factory, IConfigSetService<ResolverConfig> resolverService) : IConfigSetService<DnsMappingTable>
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
        /// 事件，当映射表被更新时触发。
        /// </summary>
        public event Action<Guid> ConfigUpdated;

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

                    ConfigUpdated?.Invoke(originalTable.Id);

                    if (originalTable.TableName != oldName)
                    {
                        if (Application.Current != null)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                ConfigRenamed?.Invoke(originalTable.Id, originalTable.TableName);
                            });
                        }
                        else ConfigRenamed?.Invoke(originalTable.Id, originalTable.TableName);
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

                // 解析JSON数据
                var jObject = JObject.Parse(json);

                JObject mappingTableJObject;
                JArray resolversJArray = null;

                mappingTableJObject = jObject["MappingTable"] as JObject;
                resolversJArray = jObject["Resolvers"] as JArray;

                // 首先处理解析器依赖
                var resolverIdMap = new Dictionary<Guid, Guid>(); // 旧 ID 到新 ID 的映射

                if (resolversJArray != null)
                {
                    foreach (var resolverObj in resolversJArray.OfType<JObject>())
                    {
                        var resolverResult = ResolverConfig.FromJObject(resolverObj);
                        if (!resolverResult.IsSuccess)
                        {
                            WriteLog($"解析关联解析器时出错：{resolverResult.ErrorMessage}", LogLevel.Warning);
                            continue;
                        }

                        var importedResolver = resolverResult.Value;

                        // 跳过内置解析器
                        if (importedResolver.IsBuiltIn)
                        {
                            // 对于内置解析器，直接使用原 ID
                            resolverIdMap[importedResolver.Id] = importedResolver.Id;
                            continue;
                        }

                        var existingResolver = resolverService.AllConfigs.FirstOrDefault(r => r.Id == importedResolver.Id);

                        if (existingResolver != null)
                        {
                            // 使用 JToken.DeepEquals 进行深度比较
                            var existingJObject = existingResolver.ToJObject();
                            var importedJObject = importedResolver.ToJObject();

                            if (!JToken.DeepEquals(existingJObject, importedJObject))
                            {
                                // 内容不同，创建新解析器
                                var newResolver = importedResolver.Clone();
                                newResolver.Id = Guid.NewGuid();
                                newResolver.ConfigName = $"{importedResolver.ConfigName} - 关联导入";
                                newResolver.IsBuiltIn = false;

                                resolverService.AllConfigs.Add(newResolver);
                                resolverService.SaveChangesAsync(newResolver).Wait();

                                resolverIdMap[importedResolver.Id] = newResolver.Id;
                            }
                            else resolverIdMap[importedResolver.Id] = importedResolver.Id; // 内容相同，使用现有解析器
                        }
                        else
                        {
                            // 全新解析器，直接导入
                            resolverService.AllConfigs.Add(importedResolver);
                            resolverService.SaveChangesAsync(importedResolver).Wait();

                            resolverIdMap[importedResolver.Id] = importedResolver.Id;
                        }
                    }
                }

                // 然后处理映射表
                var tableResult = DnsMappingTable.FromJObject(mappingTableJObject);
                if (!tableResult.IsSuccess)
                {
                    WriteLog($"导入映射表失败，{tableResult.ErrorMessage}", LogLevel.Warning);
                    return null;
                }

                var importedTable = tableResult.Value;

                // 更新映射表中的解析器引用（现在在 TargetSources 中）
                foreach (var group in importedTable.MappingGroups)
                    foreach (var rule in group.MappingRules)
                        if (rule.TargetSources != null)
                            foreach (var source in rule.TargetSources.Where(s => s.ResolverId.HasValue))
                                if (resolverIdMap.TryGetValue(source.ResolverId.Value, out var newResolverId))
                                    source.ResolverId = newResolverId;

                // 处理映射表本身的 ID 冲突
                if (AllConfigs.Any(t => t.Id == importedTable.Id))
                    importedTable.Id = Guid.NewGuid();

                importedTable.IsBuiltIn = false;

                // 保存并返回
                repository.Save(UserDnsMappingTablesPath, importedTable);
                AllConfigs.Add(importedTable);
                return importedTable;
            }
            catch (Exception ex)
            {
                WriteLog("导入映射表时发生错误。", LogLevel.Error, ex);
                return null;
            }
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

            // 收集所有在 TargetSources 中引用的解析器 ID
            var resolverIds = table.MappingGroups
                .SelectMany(g => g.MappingRules)
                .Where(r => r.TargetSources != null)
                .SelectMany(r => r.TargetSources)
                .Where(s => s.ResolverId.HasValue)
                .Select(s => s.ResolverId.Value)
                .Distinct()
                .ToList();

            var associatedResolvers = resolverService.AllConfigs
                .Where(r => resolverIds.Contains(r.Id))
                .ToList();

            var exportData = new
            {
                MappingTable = table.ToJObject(),
                Resolvers = associatedResolvers.Select(r => r.ToJObject()).ToArray()
            };

            var json = JsonConvert.SerializeObject(exportData, Formatting.None);
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
