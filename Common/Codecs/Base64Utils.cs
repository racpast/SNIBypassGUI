using System;

namespace SNIBypassGUI.Common.Codecs
{
    public static class Base64Utils
    {
        /// <summary>  
        /// 将字符串编码为 Base64 字符串。  
        /// </summary>  
        public static string EncodeString(string plainText)
        {
            if (plainText == null) return null;
            var bytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(bytes);
        }

        /// <summary>  
        /// 将 Base64 字符串解码为原始字符串。  
        /// </summary>  
        public static string DecodeString(string base64)
        {
            if (string.IsNullOrEmpty(base64)) return null;
            try
            {
                var bytes = Convert.FromBase64String(base64);
                return System.Text.Encoding.UTF8.GetString(bytes);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        /// <summary>  
        /// 将字节数组编码为 Base64 字符串。  
        /// </summary>  
        public static string EncodeBytes(byte[] data)
        {
            if (data == null || data.Length == 0) return null;
            return Convert.ToBase64String(data);
        }

        /// <summary>  
        /// 将 Base64 字符串解码为字节数组。  
        /// </summary>  
        public static byte[] DecodeToBytes(string base64)
        {
            if (string.IsNullOrEmpty(base64)) return null;
            try
            {
                return Convert.FromBase64String(base64);
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}
