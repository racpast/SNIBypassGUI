using System;
using System.Collections.Generic;
using System.Linq;
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

#if !NET6_0_OR_GREATER
#warning 在 .NET 6 及更高版本中应使用 System.Convert.FromHexString 方法。
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

#warning 在 .NET 6 及更高版本中应使用 System.Convert.ToHexString 方法。
        public static string ToHexString(this byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;

            var hex = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
                hex.Append(b.ToString("X2"));

            return hex.ToString();
        }
#endif

#if !NET5_0_OR_GREATER
#warning 在 .NET 5 及更高版本中应使用 System.String.StartsWith 方法。
        public static bool StartsWith(this string value, char c)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            return value[0] == c;
        }
#endif

        private static int ParseHexChar(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            throw new FormatException($"Invalid hex character: '{c}'.");
        }

        public static bool ContainsAny(this string source, params object[] values)
        {
            if (string.IsNullOrEmpty(source) || values == null || values.Length == 0)
                return false;

            foreach (var v in values)
            {
                switch (v)
                {
                    case string s when !string.IsNullOrEmpty(s):
                        if (source.Contains(s))
                            return true;
                        break;

                    case char c:
                        if (source.Contains(c))
                            return true;
                        break;

                    case IEnumerable<char> charCollection:
                        if (charCollection.Any(source.Contains))
                            return true;
                        break;
                }
            }

            return false;
        }
    }
}
