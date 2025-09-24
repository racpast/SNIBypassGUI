using System;

namespace SNIBypassGUI.Common.Cryptography
{
    public static class CryptoUtils
    {
        /// <summary>
        /// 对输入数据执行基于异或操作的加密或解密处理。
        /// 该方法将输入字节数组 <paramref name="data"/> 的每个字节
        /// 与密钥字节数组 <paramref name="key"/> 对应位置的字节进行异或操作。
        /// 当密钥长度小于数据长度时，密钥字节按循环方式重复使用。
        /// </summary>
        /// <param name="data">待处理的输入字节数组，不能为 null。</param>
        /// <param name="key">用于异或操作的密钥字节数组，长度必须大于 0，不能为 null。</param>
        /// <returns>返回处理后的字节数组，长度与输入数据相同。</returns>
        /// <exception cref="ArgumentNullException">当 <paramref name="data"/> 或 <paramref name="key"/> 为 null 时抛出。</exception>
        /// <exception cref="ArgumentException">当 <paramref name="key"/> 长度为 0 时抛出。</exception>
        public static byte[] XorEncrypt(byte[] data, byte[] key)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (key.Length == 0) throw new ArgumentException("Key length must be greater than zero.", nameof(key));

            var result = new byte[data.Length];
            for (int i = 0; i < data.Length; i++)
                result[i] = (byte)(data[i] ^ key[i % key.Length]);
            return result;
        }

        /// <summary>
        /// 使用异或算法对数据进行解密。
        /// </summary>
        /// <param name="data">待解密的输入字节数组，不能为 null。</param>
        /// <param name="key">用于异或操作的密钥字节数组，长度必须大于 0，不能为 null。</param>
        /// <returns>返回解密后的字节数组，长度与输入数据相同。</returns>
        /// <exception cref="ArgumentNullException">当 <paramref name="data"/> 或 <paramref name="key"/> 为 null 时抛出。</exception>
        /// <exception cref="ArgumentException">当 <paramref name="key"/> 长度为 0 时抛出。</exception>
        public static byte[] XorDecrypt(byte[] data, byte[] key) => XorEncrypt(data, key);
    }
}
