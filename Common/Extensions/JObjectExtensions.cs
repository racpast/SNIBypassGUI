using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace SNIBypassGUI.Common.Extensions
{
    public static class JObjectExtensions
    {
        /// <summary>
        /// Tries to get a string value from the specified <see cref="JObject"/> by key.
        /// </summary>
        /// <param name="obj">The JSON object.</param>
        /// <param name="key">The property name.</param>
        /// <param name="value">When this method returns, contains the string value if found; otherwise, <c>string.Empty</c>.</param>
        /// <returns><c>true</c> if the value exists and is a string; otherwise, <c>false</c>.</returns>
        public static bool TryGetString(this JObject obj, string key, out string value)
        {
            value = string.Empty;
            if (!obj.TryGetValue(key, out var token) || token.Type != JTokenType.String)
                return false;

            value = token.ToString();
            return true;
        }

        /// <summary>
        /// Tries to get a boolean value from the specified <see cref="JObject"/> by key.
        /// </summary>
        /// <param name="obj">The JSON object.</param>
        /// <param name="key">The property name.</param>
        /// <param name="value">When this method returns, contains the boolean value if found; otherwise, <c>false</c>.</param>
        /// <returns><c>true</c> if the value exists and is a boolean; otherwise, <c>false</c>.</returns>
        public static bool TryGetBool(this JObject obj, string key, out bool value)
        {
            value = false;
            if (!obj.TryGetValue(key, out var token) || token.Type != JTokenType.Boolean)
                return false;

            value = token.Value<bool>();
            return true;
        }

        /// <summary>
        /// Tries to get a <see cref="Guid"/> value from the specified <see cref="JObject"/> by key.
        /// </summary>
        /// <param name="obj">The JSON object to search.</param>
        /// <param name="key">The property name to locate.</param>
        /// <param name="value">When this method returns, contains the GUID value if found and valid; otherwise, <see cref="Guid.Empty"/>.</param>
        /// <returns>
        /// <c>true</c> if the value exists, is a string, and is a valid GUID; otherwise, <c>false</c>.
        /// </returns>
        public static bool TryGetGuid(this JObject obj, string key, out Guid value)
        {
            if (TryParseGuidInternal(obj, key, allowNullOrEmpty: false, out var result))
            {
                value = result ?? Guid.Empty;
                return true;
            }

            value = Guid.Empty;
            return false;
        }

        /// <summary>
        /// Tries to get a nullable <see cref="Guid"/> value from the specified <see cref="JObject"/> by key.
        /// </summary>
        /// <param name="obj">The JSON object to search.</param>
        /// <param name="key">The property name to locate.</param>
        /// <param name="value">When this method returns, contains the GUID value if found and valid; otherwise, <c>null</c>.</param>
        /// <returns>
        /// <c>true</c> if the value exists and is either a valid GUID string or an empty/whitespace string (treated as null); otherwise, <c>false</c>.
        /// </returns>
        public static bool TryGetNullableGuid(this JObject obj, string key, out Guid? value) =>
            TryParseGuidInternal(obj, key, allowNullOrEmpty: true, out value);

        /// <summary>
        /// Internal method to parse a GUID from the specified <see cref="JObject"/> by key.
        /// </summary>
        /// <param name="obj">The JSON object to search.</param>
        /// <param name="key">The property name to locate.</param>
        /// <param name="allowNullOrEmpty">
        /// If <c>true</c>, empty or whitespace strings are treated as <c>null</c> and return <c>true</c>. 
        /// If <c>false</c>, empty strings cause the method to return <c>false</c>.
        /// </param>
        /// <param name="value">When this method returns, contains the GUID value if found and valid; otherwise, <c>null</c>.</param>
        /// <returns>
        /// <c>true</c> if the value exists and meets the criteria; otherwise, <c>false</c>.
        /// </returns>
        private static bool TryParseGuidInternal(JObject obj, string key, bool allowNullOrEmpty, out Guid? value)
        {
            value = null;

            if (!obj.TryGetValue(key, out var token) || token.Type != JTokenType.String)
                return false;

            var str = token.ToString().Trim();

            if (string.IsNullOrEmpty(str))
                return allowNullOrEmpty;

            if (Guid.TryParse(str, out var parsed))
            {
                value = parsed;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Tries to parse an enum value from the specified <see cref="JObject"/> by key.
        /// </summary>
        /// <typeparam name="T">The enum type to parse.</typeparam>
        /// <param name="obj">The JSON object.</param>
        /// <param name="key">The property name.</param>
        /// <param name="value">When this method returns, contains the enum value if parsing succeeded; otherwise, the default of <typeparamref name="T"/>.</param>
        /// <returns><c>true</c> if the value exists, is a string, and was successfully parsed as the enum; otherwise, <c>false</c>.</returns>
        public static bool TryGetEnum<T>(this JObject obj, string key, out T value) where T : struct
        {
            value = default!;
            if (!obj.TryGetValue(key, out var token) || token.Type != JTokenType.String)
                return false;

            return Enum.TryParse(token.ToString(), true, out value);
        }

        /// <summary>
        /// Tries to get a read-only list of values from the specified <see cref="JObject"/> by key.
        /// </summary>
        /// <typeparam name="T">The target element type for the list.</typeparam>
        /// <param name="obj">The JSON object.</param>
        /// <param name="key">The property name.</param>
        /// <param name="value">When this method returns, contains the read-only list of parsed values if successful; otherwise, an empty list.</param>
        /// <returns><c>true</c> if the property exists, is a JSON array, and all elements could be converted; otherwise, <c>false</c>.</returns>
        public static bool TryGetArray<T>(this JObject obj, string key, out IReadOnlyList<T> value)
        {
            value = [];
            if (!obj.TryGetValue(key, out var token) || token.Type != JTokenType.Array)
                return false;

            try
            {
                value = [.. token.Values<T>()];
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
