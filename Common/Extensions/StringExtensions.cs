using System;
using System.Text;

namespace SNIBypassGUI.Common.Extensions
{
    public static class StringExtensions
    {
        /// <summary>
        /// Returns the original string if it is not null, empty, or consists only of white-space characters; 
        /// otherwise, returns the specified default value (defaults to empty string).
        /// </summary>
        /// <param name="value">The input string to evaluate.</param>
        /// <param name="defaultValue">The default value to return if the input is null, empty, or white-space. Defaults to <c>""</c>.</param>
        /// <returns>
        /// The original string if it contains non-white-space characters; otherwise, the <paramref name="defaultValue"/>.
        /// </returns>
        /// <example>
        /// <code>
        /// string input = null;
        /// string result = input.OrDefault("Default Value"); // result is "Default Value"
        /// string result2 = input.OrDefault(); // result2 is ""
        /// </code>
        /// </example>
        public static string OrDefault(this string value, string defaultValue = "") =>
            string.IsNullOrWhiteSpace(value) ? defaultValue : value;

        [Obsolete("升级到 .NET 6 或更高版本时，可以使用 System.Convert.FromHexString 方法。")]
        public static byte[] FromHexString(this string s)
        {
            if (s == null)
                throw new ArgumentNullException(nameof(s));

            if (s.Length % 2 != 0)
                throw new FormatException("The input string must have an even number of characters.");

            int len = s.Length / 2;
            byte[] result = new byte[len];

            for (int i = 0; i < len; i++)
            {
                int hi = ParseHexChar(s[2 * i]) << 4;
                int lo = ParseHexChar(s[2 * i + 1]);
                result[i] = (byte)(hi | lo);
            }

            return result;
        }

        [Obsolete("升级到 .NET 6 或更高版本时，可以使用 System.Convert.ToHexString 方法。")]
        public static string ToHexString(this byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;

            var hex = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
                hex.Append(b.ToString("X2"));

            return hex.ToString();
        }

        private static int ParseHexChar(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            throw new FormatException($"Invalid hex character: '{c}'.");
        }
    }
}
