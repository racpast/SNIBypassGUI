using System;

namespace SNIBypassGUI.Utils
{
    public static class ConvertUtils
    {
        /// <summary>
        /// 将字符串转换为整数。
        /// </summary>
        public static int StringToInt(string input) => int.TryParse(input, out int result) ? result : 0;

        /// <summary>
        /// 将布尔值转换为"是"或"否"的字符串。
        /// </summary>
        /// <param name="value">布尔值</param>
        public static string BoolToYesNo(bool? value)
        {
            return value switch
            {
                true => "是",
                false => "否",
                _ => null
            };
        }

        /// <summary>
        /// 格式化字符串数组为 PowerShell 所需的字符串格式。
        /// </summary>
        /// <param name="inputArray">输入的字符串数组</param>
        /// <returns>格式化后的字符串</returns>
        public static string FormatStringArray(string[] inputArray)
        {
            if (inputArray == null) return null;
            return "(" + string.Join(",", Array.ConvertAll(inputArray, s => "\"" + s + "\"")) + ")";
        }

        /// <summary>
        /// 将字符串转换为布尔值
        /// 支持 "true" 和 "false"（不区分大小写），其他值返回 false。
        /// </summary>
        /// <param name="input">要转换的字符串</param>
        /// <returns>转换后的布尔值</returns>
        public static bool StringToBool(string input) => !string.IsNullOrEmpty(input) && input.Trim().ToLower() == "true";

        /// <summary>
        /// 文件大小转换器。
        /// </summary>
        public static class FileSizeConverter
        {
            private const long BytesInKB = 1024;
            private const long BytesInMB = BytesInKB * 1024;
            private const long BytesInGB = BytesInMB * 1024;
            private const long BytesInTB = BytesInGB * 1024;

            /// <summary>
            /// 表示文件大小的单位。
            /// </summary>
            public enum SizeUnit
            {
                B,
                KB,
                MB,
                GB,
                TB
            }

            /// <summary>
            /// 将字节数转换为指定单位的大小。
            /// </summary>
            /// <param name="bytes">字节数</param>
            /// <param name="toUnit">目标单位</param>
            /// <exception cref="ArgumentException">不支持的单位</exception>
            private static double ConvertFromBytes(long bytes, SizeUnit toUnit)
            {
                return toUnit switch
                {
                    SizeUnit.B => bytes,
                    SizeUnit.KB => bytes / (double)BytesInKB,
                    SizeUnit.MB => bytes / (double)BytesInMB,
                    SizeUnit.GB => bytes / (double)BytesInGB,
                    SizeUnit.TB => bytes / (double)BytesInTB,
                    _ => throw new ArgumentException("不支持的单位：" + toUnit),
                };
            }

            /// <summary>
            /// 将指定大小转换为字节数。
            /// </summary>
            /// <param name="size">指定大小</param>
            /// <param name="fromUnit">源单位</param>
            /// <exception cref="ArgumentException">不支持的单位</exception>
            private static long ConvertToBytes(double size, SizeUnit fromUnit)
            {
                return fromUnit switch
                {
                    SizeUnit.B => (long)size,
                    SizeUnit.KB => (long)(size * BytesInKB),
                    SizeUnit.MB => (long)(size * BytesInMB),
                    SizeUnit.GB => (long)(size * BytesInGB),
                    SizeUnit.TB => (long)(size * BytesInTB),
                    _ => throw new ArgumentException("不支持的单位：" + fromUnit),
                };
            }

            /// <summary>
            /// 将指定大小从一个单位转换为另一个单位。
            /// </summary>
            /// <param name="size">指定大小</param>
            /// <param name="fromUnit">源单位</param>
            /// <param name="toUnit">目标单位</param>
            public static double ConvertBetweenUnits(double size, SizeUnit fromUnit, SizeUnit toUnit) => ConvertFromBytes(ConvertToBytes(size, fromUnit), toUnit);
        }
    }
}
