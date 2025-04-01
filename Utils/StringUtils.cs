using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SNIBypassGUI.Utils
{
    public static class StringUtils
    {
        /// <summary>
        /// 交换哈希值的位置
        /// </summary>
        public static string SwapHashPositions(string hashString, string hashA, string hashB)
        {
            var hashList = hashString.Split([','], StringSplitOptions.RemoveEmptyEntries).ToList();

            int indexA = hashList.IndexOf(hashA);
            int indexB = hashList.IndexOf(hashB);

            if (indexA != -1 && indexB != -1)
            {
                // 使用元组交换两个元素
                (hashList[indexA], hashList[indexB]) = (hashList[indexB], hashList[indexA]);
            }

            return string.Join(",", hashList);
        }

        /// <summary>
        /// 添加哈希值
        /// </summary>
        public static string AppendHash(string hashString, string hash)
        {
            hashString = RemoveHash(hashString, hash);
            hashString = string.IsNullOrEmpty(hashString) ? hash : $"{hashString},{hash}";
            return hashString;
        }

        /// <summary>
        /// 移除哈希值
        /// </summary>
        public static string RemoveHash(string hashString, string hash)
        {
            return string.Join(",", hashString.Split([','], StringSplitOptions.RemoveEmptyEntries).Where(_hash => _hash != hash));
        }

        /// <summary>
        /// 替换哈希值
        /// </summary>
        public static string ReplaceHash(string hashString, string originHash, string newHash)
        {
            return string.Join(",", hashString.Split([','], StringSplitOptions.RemoveEmptyEntries).Select(hash => hash == originHash ? newHash : hash));
        }

        /// <summary>
        /// 验证邮箱格式
        /// </summary>
        public static bool IsValidEmail(string email)
        {
            try
            {
                var addr = new MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 合并并返回一个字符串
        /// </summary>
        public static string MergeStrings(string separator, params string[] args)
        {
            return string.Join(separator, args.Where(arg => !string.IsNullOrEmpty(arg)));
        }

        /// <summary>
        /// 将字符串分割为列表
        /// </summary>
        public static string[] SplitStrings(string args, params string[] separator)
        {
            // 使用空格分隔并过滤掉空元素
            return [.. args
                .Split(separator, StringSplitOptions.RemoveEmptyEntries)
                .Where(arg => !string.IsNullOrEmpty(arg))];
        }
    }
}
