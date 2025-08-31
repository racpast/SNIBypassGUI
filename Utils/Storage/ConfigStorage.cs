using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Utils.Cryptography;
using SNIBypassGUI.Utils.Extensions;
using SNIBypassGUI.Utils.IO;
using SNIBypassGUI.Utils.Results;
using static SNIBypassGUI.Consts.AppConsts;
using static SNIBypassGUI.Utils.LogManager;

namespace SNIBypassGUI.Utils.Storage
{
    internal static class ConfigStorage
    {
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> s_fileLocks = new();

        private sealed class RecordIndex
        {
            public Guid Id { get; set; }
            public long DataOffset { get; set; }
            public int DataLength { get; set; }
            public int Flags { get; set; }
        }

        public static void Compact<T>(string filePath, Func<JObject, ParseResult<T>> factory) where T : IStorable
        {
            var fileLock = s_fileLocks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));

            fileLock.Wait();
            try
            {
                if (!File.Exists(filePath)) return;

                WriteLog($"开始尝试整理文件：{filePath}。", LogLevel.Info);

                var allActiveConfigs = LoadAll(filePath, factory).ToList();
                WriteLog($"找到 {allActiveConfigs.Count} 个有效配置。", LogLevel.Debug);

                var oldSize = new FileInfo(filePath).Length;
                SafeWrite(filePath, (tempFs) =>
                {
                    tempFs.SetLength(0);
                    WriteLog($"临时文件已清空。", LogLevel.Debug);

                    // 初始化新文件和密钥
                    var newXorKey = new byte[DataKeyLength];
                    InitializeNewFile(tempFs, newXorKey);

                    // 显式计算数据起始位置
                    long dataStartOffset = DataHeaderBaseSize + DataKeyLength;
                    long currentPosition = tempFs.Position;

                    // 双重位置验证
                    if (currentPosition != dataStartOffset)
                    {
                        WriteLog($"位置校正：{currentPosition} 为 {dataStartOffset}。", LogLevel.Debug);
                        tempFs.Seek(dataStartOffset, SeekOrigin.Begin);
                        currentPosition = dataStartOffset;
                    }

                    WriteLog($"数据起始位置：{currentPosition}。", LogLevel.Debug);

                    List<RecordIndex> newIndexList = [];

                    foreach (var config in allActiveConfigs)
                    {
                        // 记录写入前位置
                        long startPos = tempFs.Position;

                        // 准备和写入数据
                        var json = config.ToJObject().ToString(Formatting.None);
                        var jsonBytes = Encoding.UTF8.GetBytes(json);
                        var encryptedData = CryptoUtils.XorEncrypt(jsonBytes, newXorKey);
                        tempFs.Write(encryptedData, 0, encryptedData.Length);

                        // 记录写入后位置
                        long endPos = tempFs.Position;
                        WriteLog($"写入数据块，ID：{config.Id}，起始位置：{startPos}，长度：{encryptedData.Length}，结束位置：{endPos}。", LogLevel.Debug);

                        newIndexList.Add(new RecordIndex
                        {
                            Id = config.Id,
                            DataOffset = startPos,
                            DataLength = encryptedData.Length,
                            Flags = 0
                        });
                    }

                    RewriteIndexTable(tempFs, newIndexList);
                });

                var newSize = new FileInfo(filePath).Length;
                WriteLog($"整理完成，原始大小：{oldSize}，新大小：{newSize}。", LogLevel.Info);
            }
            finally
            {
                fileLock.Release();
            }
        }

        public static void Remove(string filePath, Guid configId)
        {
            var fileLock = s_fileLocks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));

            fileLock.Wait();
            try
            {
                // If the file doesn't exist, there's nothing to do.
                if (!File.Exists(filePath)) return;

                WriteLog($"尝试移除配置，ID：{configId}。", LogLevel.Info);
                SafeWrite(filePath, (fs) =>
                {
                    if (fs.Length < DataHeaderBaseSize + DataKeyLength)
                    {
                        WriteLog($"文件头不完整，移除操作中止。", LogLevel.Error);
                        return;
                    }

                    // 读取文件头
                    var header = new byte[DataHeaderBaseSize + DataKeyLength];
                    fs.Seek(0, SeekOrigin.Begin);
                    fs.ReadExactly(header, 0, header.Length);
                    if (!ValidateFileHeader(header))
                    {
                        WriteLog($"文件头校验失败，移除操作中止。", LogLevel.Error);
                        return;
                    }

                    // 提取元数据
                    var xorKey = new byte[DataKeyLength];
                    Array.Copy(header, DataHeaderBaseSize, xorKey, 0, DataKeyLength);
                    int recordCount = BitConverter.ToInt32(header, 8);
                    long indexOffset = BitConverter.ToInt64(header, 12);

                    // 验证索引位置
                    if (indexOffset < DataHeaderBaseSize + DataKeyLength || indexOffset > fs.Length)
                    {
                        WriteLog($"索引偏移量无效：{indexOffset}，文件长度：{fs.Length}，移除操作中止。", LogLevel.Error);
                        return;
                    }

                    // 加载索引表
                    var indexList = LoadIndexTable(fs, recordCount, indexOffset);

                    // 查找目标记录
                    var targetIndex = indexList.FirstOrDefault(i => i.Id == configId && i.Flags == 0);
                    if (targetIndex == null) return;

                    targetIndex.Flags = 1; // 标记为已删除
                    RewriteIndexTable(fs, indexList);
                });
            }
            finally
            {
                fileLock.Release();
            }
        }

        public static void Save<T>(string filePath, T config) where T : IStorable
        {
            var fileLock = s_fileLocks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));
            fileLock.Wait();
            try
            {
                var json = config.ToJObject().ToString(Formatting.None);
                var jsonBytes = Encoding.UTF8.GetBytes(json);

                WriteLog($"尝试保存配置，ID：{config.Id}，原始数据起始：{json.Substring(0, Math.Min(50, json.Length))}。", LogLevel.Debug);
                SafeWrite(filePath, (fs) =>
                {
                    byte[] xorKey;
                    List<RecordIndex> indexList;

                    if (fs.Length == 0)
                    {
                        // File is new (or was empty), initialize it
                        xorKey = new byte[DataKeyLength];
                        InitializeNewFile(fs, xorKey);
                        indexList = [];
                    }
                    else
                    {
                        // File exists, read its metadata
                        var header = new byte[DataHeaderBaseSize + DataKeyLength];
                        fs.Seek(0, SeekOrigin.Begin);
                        fs.ReadExactly(header, 0, header.Length);
                        if (!ValidateFileHeader(header))
                        {
                            WriteLog($"文件头校验失败，保存操作中止。", LogLevel.Error);
                            return;
                        }

                        xorKey = new byte[DataKeyLength];
                        Array.Copy(header, DataHeaderBaseSize, xorKey, 0, DataKeyLength);

                        int recordCount = BitConverter.ToInt32(header, 8);
                        long indexOffset = BitConverter.ToInt64(header, 12);

                        if (indexOffset < DataHeaderBaseSize + DataKeyLength || indexOffset > fs.Length)
                        {
                            WriteLog($"索引偏移量无效：{indexOffset}，文件长度：{fs.Length}，保存操作中止。", LogLevel.Error);
                            return;
                        }
                        indexList = LoadIndexTable(fs, recordCount, indexOffset);
                    }

                    byte[] finalKey = (byte[])xorKey.Clone();
                    WriteLog($"保存使用密钥：{BitConverter.ToString(finalKey)}。", LogLevel.Debug);

                    // Find and mark any existing version of this config as deleted
                    var existingIndex = indexList.FirstOrDefault(x => x.Id == config.Id && x.Flags == 0);
                    if (existingIndex != null) existingIndex.Flags = 1;

                    // Encrypt new data
                    byte[] encryptedData = CryptoUtils.XorEncrypt(jsonBytes, finalKey);

                    // The data will be appended before the old index. The RewriteIndexTable will place the new index at the end.
                    long newDataOffset = fs.Length;
                    fs.Seek(newDataOffset, SeekOrigin.Begin);
                    fs.Write(encryptedData, 0, encryptedData.Length);
                    fs.Flush();
                    WriteLog($"写入数据后文件长度：{fs.Length}。", LogLevel.Debug);

                    // Add new index entry
                    indexList.Add(new RecordIndex
                    {
                        Id = config.Id,
                        DataOffset = newDataOffset,
                        DataLength = encryptedData.Length,
                        Flags = 0
                    });

                    // Rewrite the index at the end of the file and update header
                    RewriteIndexTable(fs, indexList);
                    fs.Flush();
                    WriteLog($"重写索引表后文件长度：{fs.Length}。", LogLevel.Debug);
                });
            }
            finally
            {
                fileLock.Release();
            }
        }

        private static void SafeWrite(string filePath, Action<FileStream> writeAction)
        {
            string tempFilePath = filePath + ".tmp";
            string backupFilePath = filePath + ".bak";

            // If a previous operation failed, we might have leftover files.
            FileUtils.TryDeleteFile(tempFilePath);
            FileUtils.TryDeleteFile(backupFilePath);

            // If the original file exists, copy it to our temporary file to work on it.
            // This preserves the original in case the write action fails.
            if (File.Exists(filePath)) File.Copy(filePath, tempFilePath, true);

            try
            {
                // Perform all write operations on the temporary file.
                using (var fs = new FileStream(tempFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                    writeAction(fs);

                if (File.Exists(filePath))
                {
                    // Atomically replace the original file with the new one.
                    // This moves the original to the backup path and the temp file to the original path.
                    WriteLog($"尝试替换文件 {filePath}。", LogLevel.Debug);
                    File.Replace(tempFilePath, filePath, backupFilePath);
                    FileUtils.TryDeleteFile(backupFilePath); // Clean up the backup file
                }
                else
                {
                    WriteLog($"尝试创建新文件 {filePath}。", LogLevel.Debug);
                    File.Move(tempFilePath, filePath);
                }
            }
            catch (Exception ex)
            {
                // If anything goes wrong, delete the temporary file to clean up.
                // The original file remains untouched.
                FileUtils.TryDeleteFile(tempFilePath);
                WriteLog($"写入文件时发生异常。", LogLevel.Error, ex);
            }
        }

        public static T Load<T>(string filePath, Guid configId, Func<JObject, ParseResult<T>> factory) where T : IStorable
        {
            if (!File.Exists(filePath)) return default;

            WriteLog($"尝试加载文件 {filePath} 中的指定配置，ID：{configId}。", LogLevel.Info);
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (fs.Length < DataHeaderBaseSize + DataKeyLength)
            {
                WriteLog($"文件头不完整，加载操作中止。", LogLevel.Error);
                return default;
            }

            // 读取文件头
            var header = new byte[DataHeaderBaseSize + DataKeyLength];
            fs.Seek(0, SeekOrigin.Begin);
            fs.ReadExactly(header, 0, header.Length);
            if (!ValidateFileHeader(header))
            {
                WriteLog($"文件头校验失败，加载操作中止。", LogLevel.Error);
                return default;
            }

            // 提取元数据
            var xorKey = new byte[DataKeyLength];
            Array.Copy(header, DataHeaderBaseSize, xorKey, 0, DataKeyLength);
            int recordCount = BitConverter.ToInt32(header, 8);
            long indexOffset = BitConverter.ToInt64(header, 12);

            WriteLog($"加载使用密钥：{BitConverter.ToString(xorKey)}，文件长度：{fs.Length}。", LogLevel.Debug);

            // 验证索引位置
            if (indexOffset < DataHeaderBaseSize + DataKeyLength || indexOffset > fs.Length)
            {
                WriteLog($"索引偏移量无效：{indexOffset}，文件长度：{fs.Length}，加载操作中止。", LogLevel.Error);
                return default;
            }

            // 加载索引表
            var indexList = LoadIndexTable(fs, recordCount, indexOffset);

            // 查找目标记录
            var targetIndex = indexList.FirstOrDefault(i => i.Id == configId && i.Flags == 0);
            if (targetIndex == null) return default;

            // 验证数据位置
            if (targetIndex.DataOffset < DataHeaderBaseSize + DataKeyLength ||
                targetIndex.DataOffset + targetIndex.DataLength > fs.Length)
                WriteLog($"数据块位置边缘无效：{targetIndex.DataOffset}-{targetIndex.DataOffset + targetIndex.DataLength}，文件长度：{fs.Length}。", LogLevel.Warning);

            // 读取并处理数据
            return ProcessDataBlock<T>(fs, targetIndex, xorKey, factory, configId);
        }

        public static IEnumerable<T> LoadAll<T>(string filePath, Func<JObject, ParseResult<T>> factory) where T : IStorable
        {
            WriteLog($"尝试加载文件 {filePath} 中的所有配置。", LogLevel.Info);

            if (!File.Exists(filePath)) yield break;

            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (fs.Length < DataHeaderBaseSize + DataKeyLength)
            {
                WriteLog($"文件头不完整，加载所有操作中止。", LogLevel.Error);
                yield break;
            }

            // 读取文件头
            var header = new byte[DataHeaderBaseSize + DataKeyLength];
            fs.Seek(0, SeekOrigin.Begin);
            fs.ReadExactly(header, 0, header.Length);
            if (!ValidateFileHeader(header))
            {
                WriteLog($"文件头校验失败，加载所有操作中止。", LogLevel.Error);
                yield break;
            }

            // 提取元数据
            var xorKey = new byte[DataKeyLength];
            Array.Copy(header, DataHeaderBaseSize, xorKey, 0, DataKeyLength);
            int recordCount = BitConverter.ToInt32(header, 8);
            long indexOffset = BitConverter.ToInt64(header, 12);
            WriteLog($"加载所有使用密钥：{BitConverter.ToString(xorKey)}，文件长度：{fs.Length}。", LogLevel.Debug);

            // 验证索引位置
            if (indexOffset < DataHeaderBaseSize + DataKeyLength || indexOffset > fs.Length)
            {
                WriteLog($"索引偏移量无效：{indexOffset}，文件长度：{fs.Length}。", LogLevel.Error);
                yield break;
            }

            // 加载索引表
            var indexList = LoadIndexTable(fs, recordCount, indexOffset);

            // 只处理有效记录
            var activeRecords = indexList.Where(i => i.Flags == 0).ToList();
            foreach (var index in activeRecords)
            {

                // 跳过明显无效的位置
                if (index.DataOffset < DataHeaderBaseSize + DataKeyLength - 4 ||
                    index.DataOffset + index.DataLength > fs.Length)
                {
                    WriteLog($"跳过无效数据块：{index.Id}，位置：{index.DataOffset}-{index.DataOffset + index.DataLength}，文件长度：{fs.Length}。", LogLevel.Warning);
                    continue;
                }

                T result = default;
                try
                {
                    result = ProcessDataBlock<T>(fs, index, xorKey, factory, index.Id);
                }
                catch (Exception ex)
                {
                    WriteLog($"加载配置 {index.Id} 时遇到异常。", LogLevel.Error, ex);
                    continue;
                }
                if (result != null) yield return result;
            }
        }

        private static void InitializeNewFile(FileStream fs, byte[] xorKey)
        {
            WriteLog($"尝试初始化新文件。", LogLevel.Info);

            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(xorKey);

            fs.SetLength(0);
            fs.Seek(0, SeekOrigin.Begin);

            // 计算文件头总大小
            int headerSize = DataHeaderBaseSize + DataKeyLength;

            // 准备完整的文件头缓冲区
            byte[] header = new byte[headerSize];
            int offset = 0;

            // 填充魔数
            Encoding.ASCII.GetBytes(DataMagic).CopyTo(header, offset);
            offset += 4;

            // 版本和密钥长度
            header[offset++] = DataVersion;
            header[offset++] = (byte)DataKeyLength;

            // 保留字段
            offset += 2; // 不需要额外操作

            // 记录数
            BitConverter.GetBytes(0).CopyTo(header, offset);
            offset += 4;

            // 索引偏移，初始值为文件头大小
            BitConverter.GetBytes(headerSize).CopyTo(header, offset);
            offset += 8;

            // 密钥
            Array.Copy(xorKey, 0, header, offset, DataKeyLength);

            // 一次性写入完整文件头
            fs.Write(header, 0, header.Length);
            fs.Flush();

            // 验证写入位置
            long endPos = fs.Position;
            WriteLog($"文件头写入完成，大小：{headerSize}，当前位置：{endPos}。", LogLevel.Debug);

            // 强制位置验证和校正
            if (endPos != headerSize)
            {
                WriteLog($"预期位置：{headerSize} 与实际位置：{endPos} 不符，强制重置。", LogLevel.Warning);
                fs.Seek(headerSize, SeekOrigin.Begin);
            }
        }

        private static List<RecordIndex> LoadIndexTable(FileStream fs, int recordCount, long indexOffset)
        {
            if (recordCount == 0) return [];

            try
            {
                long indexSize = (long)recordCount * DataIndexEntrySize;
                if (indexOffset + indexSize > fs.Length)
                {
                    WriteLog($"索引表超出文件范围。预期大小：{indexSize} @ {indexOffset}，文件总长：{fs.Length}。", LogLevel.Error);
                    return [];
                }

                fs.Seek(indexOffset, SeekOrigin.Begin);
                var indexBytes = new byte[indexSize];

                // 如果文件在这里被外部截断，ReadExactly 会抛出异常
                fs.ReadExactly(indexBytes, 0, indexBytes.Length);

                return DecodeIndexTable(indexBytes, recordCount);
            }
            catch (Exception ex)
            {
                WriteLog($"加载索引表时发生异常。", LogLevel.Error, ex);
                return [];
            }
        }

        private static void RewriteIndexTable(FileStream fs, List<RecordIndex> indexList)
        {
            long startPosition = fs.Position;
            long fileLength = fs.Length;
            WriteLog($"开始重写索引表，当前位置：{startPosition}，文件长度：{fileLength}。", LogLevel.Debug);

            // 定位到文件末尾
            long newIndexOffset = fs.Seek(0, SeekOrigin.End);
            WriteLog($"新索引表位置：{newIndexOffset}。", LogLevel.Debug);

            // 只处理活动记录
            var activeRecords = indexList.Where(i => i.Flags == 0).ToList();
            byte[] newIndexBytes = EncodeIndexTable(activeRecords);
            WriteLog($"索引表大小：{newIndexBytes.Length} 字节，记录数：{activeRecords.Count}。", LogLevel.Debug);

            // 写入索引表
            fs.Write(newIndexBytes, 0, newIndexBytes.Length);
            WriteLog($"索引表已写入。", LogLevel.Debug);

            // 更新文件头
            fs.Seek(8, SeekOrigin.Begin); // 记录数偏移位置
            var countBytes = BitConverter.GetBytes(activeRecords.Count);
            fs.Write(countBytes, 0, countBytes.Length);

            var offsetBytes = BitConverter.GetBytes(newIndexOffset);
            fs.Write(offsetBytes, 0, offsetBytes.Length);

            WriteLog($"更新文件头，记录数：{activeRecords.Count}，索引偏移：{newIndexOffset}。", LogLevel.Debug);

            // 计算新文件长度并截断
            long newLength = newIndexOffset + newIndexBytes.Length;
            fs.SetLength(newLength);

            // 验证文件长度
            long actualLength = fs.Length;
            if (actualLength != newLength)
                WriteLog($"预期长度：{newLength} 与实际长度：{actualLength} 不符。", LogLevel.Warning);

            WriteLog($"文件截断完成，新长度：{actualLength}。", LogLevel.Debug);

            // 记录索引表内容
            if (newIndexBytes.Length > 0)
                WriteLog($"索引表内容：{BitConverter.ToString(newIndexBytes).Replace("-", " ")}。", LogLevel.Debug);
        }

        private static bool ValidateFileHeader(byte[] header)
        {
            if (Encoding.ASCII.GetString(header, 0, 4) != DataMagic)
            {
                WriteLog("文件魔数不匹配。", LogLevel.Error);
                return false;
            }

            if (header[4] != DataVersion)
            {
                WriteLog("不支持的文件版本。", LogLevel.Error);
                return false;
            }

            return true;
        }

        private static T ProcessDataBlock<T>(FileStream fs, RecordIndex index, byte[] xorKey, Func<JObject, ParseResult<T>> factory, Guid expectedId) where T : IStorable
        {
            // 验证数据位置
            if (index.DataOffset < DataHeaderBaseSize + DataKeyLength - 4 ||
                index.DataOffset >= fs.Length)
                WriteLog($"数据偏移量边缘无效：{index.DataOffset}，文件长度：{fs.Length}。", LogLevel.Warning);

            fs.Seek(index.DataOffset, SeekOrigin.Begin);
            var encryptedData = new byte[index.DataLength];
            fs.ReadExactly(encryptedData, 0, encryptedData.Length);

            var jsonBytes = CryptoUtils.XorEncrypt(encryptedData, xorKey);
            WriteLog($"解密数据块，ID：{expectedId}，偏移：{index.DataOffset}，长度：{index.DataLength}，解密密钥：{BitConverter.ToString(xorKey)}。", LogLevel.Debug);

            try
            {
                var json = Encoding.UTF8.GetString(jsonBytes);
                WriteLog($"解密后数据起始：{json.Substring(0, Math.Min(50, json.Length))}。", LogLevel.Debug);

                var jObject = JObject.Parse(json);

                var result = factory(jObject);
                if (result.IsSuccess)
                {
                    if (result.Value.Id != expectedId)
                        WriteLog($"ID 校验失败，预期：{expectedId}, 实际：{result.Value.Id}。", LogLevel.Warning);
                    return result.Value;
                }
                WriteLog($"加载配置失败：{result.ErrorMessage}", LogLevel.Error);
                return default;
            }
            catch (JsonReaderException jex)
            {
                WriteLog($"数据解析失败，解密后原始数据：{Encoding.UTF8.GetString(jsonBytes)}。", LogLevel.Error, jex);
                return default;
            }
            catch (Exception ex)
            {
                WriteLog($"处理数据块失败：{Encoding.UTF8.GetString(jsonBytes)}，ID：{expectedId}。", LogLevel.Error, ex);
                return default;
            }
        }

        private static byte[] EncodeIndexTable(List<RecordIndex> indexList)
        {
            using var ms = new MemoryStream();
            using (var writer = new BinaryWriter(ms))
            {
                foreach (var idx in indexList)
                {
                    writer.Write(idx.Id.ToByteArray());
                    writer.Write(idx.DataOffset);
                    writer.Write(idx.DataLength);
                    writer.Write(idx.Flags);
                }
            }
            return ms.ToArray();
        }

        private static List<RecordIndex> DecodeIndexTable(byte[] data, int recordCount)
        {
            var list = new List<RecordIndex>(recordCount);
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                for (int i = 0; i < recordCount; i++)
                {
                    list.Add(new RecordIndex
                    {
                        Id = new Guid(reader.ReadBytes(16)),
                        DataOffset = reader.ReadInt64(),
                        DataLength = reader.ReadInt32(),
                        Flags = reader.ReadInt32()
                    });
                }
            }
            return list;
        }
    }
}