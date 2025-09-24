using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using SNIBypassGUI.Enums;

namespace SNIBypassGUI.Common.Network
{
    public static class NetworkUtils
    {
        private static readonly Regex IPv6Regex = new(
            @"^(" +
            @"(?:[0-9A-Fa-f]{1,4}:){7}[0-9A-Fa-f]{1,4}|" +
            @"(?:[0-9A-Fa-f]{1,4}:){1,7}:|" +
            @"(?:[0-9A-Fa-f]{1,4}:){1,6}:[0-9A-Fa-f]{1,4}|" +
            @"(?:[0-9A-Fa-f]{1,4}:){1,5}(?::[0-9A-Fa-f]{1,4}){1,2}|" +
            @"(?:[0-9A-Fa-f]{1,4}:){1,4}(?::[0-9A-Fa-f]{1,4}){1,3}|" +
            @"(?:[0-9A-Fa-f]{1,4}:){1,3}(?::[0-9A-Fa-f]{1,4}){1,4}|" +
            @"(?:[0-9A-Fa-f]{1,4}:){1,2}(?::[0-9A-Fa-f]{1,4}){1,5}|" +
            @"[0-9A-Fa-f]{1,4}:(?:(?::[0-9A-Fa-f]{1,4}){1,6})|" +
            @":(?:(?::[0-9A-Fa-f]{1,4}){1,7}|:)|" +
            @"fe80:(?::[0-9A-Fa-f]{0,4}){0,4}%[0-9A-Za-z]+|" +
            @"::(?:ffff(:0{1,4}){0,1}:){0,1}" +
            @"(?:(?:25[0-5]|(?:2[0-4]|1?\d|)\d)\.){3,3}" +
            @"(?:25[0-5]|(?:2[0-4]|1?\d|)\d)|" +
            @"(?:[0-9A-Fa-f]{1,4}:){1,4}:" +
            @"(?:(?:25[0-5]|(?:2[0-4]|1?\d|)\d)\.){3,3}" +
            @"(?:25[0-5]|(?:2[0-4]|1?\d|)\d)" +
            @")$", RegexOptions.Compiled);
        private static readonly Regex LabelRegex = new(@"^[a-zA-Z0-9](?:[a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?$", RegexOptions.Compiled);
        private static readonly Regex UrlPathRegex = new(@"^(/[A-Za-z0-9\-._~!$&'()*+,;=:@%]*)*$", RegexOptions.Compiled);
        private static readonly Regex HttpHeaderNameRegex = new(@"^[!#$%&'*+\-.^_`|~0-9A-Za-z]+$", RegexOptions.Compiled);
        private static readonly Regex HttpHeaderValueRegex = new(@"^[\t\x20-\x7E\x80-\xFF]*$", RegexOptions.Compiled);
        private static readonly Regex AlpnNameRegex = new(@"^[\x20-\x7E]{1,255}$", RegexOptions.Compiled);

        /// <summary>
        /// Determines whether the specified string is a valid IPv4 address.
        /// </summary>
        /// <param name="input">The IP address string to validate.</param>
        /// <returns><c>true</c> if the string is a valid IPv4 address; otherwise, <c>false</c>.</returns>
        public static bool IsValidIPv4(string ip)
        {
            if (string.IsNullOrEmpty(ip))
                return false;

            // 用点分割，必须有4段
            string[] parts = ip.Split('.');
            if (parts.Length != 4)
                return false;

            foreach (string part in parts)
            {
                // 每段非空
                if (string.IsNullOrEmpty(part))
                    return false;

                // 不能有非数字字符
                foreach (char c in part)
                    if (!char.IsDigit(c))
                        return false;

                // 不允许前导零，且单独的0允许
                if (part.Length > 1 && part.StartsWith("0"))
                    return false;

                // 转数字判断范围
                if (!int.TryParse(part, out int num))
                    return false;

                if (num < 0 || num > 255)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Determines whether the specified string is a valid IPv6 address.
        /// </summary>
        /// <param name="input">The IP address string to validate.</param>
        /// <returns><c>true</c> if the string is a valid IPv6 address; otherwise, <c>false</c>.</returns>
        public static bool IsValidIPv6(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            return IPv6Regex.IsMatch(input);
        }

        /// <summary>
        /// Determines whether the specified string is a valid IP address (IPv4 or IPv6).
        /// </summary>
        /// <param name="input">The IP address string to validate.</param>
        /// <returns><c>true</c> if the string is a valid IP address; otherwise, <c>false</c>.</returns>
        public static bool IsValidIP(string input) =>
            IsValidIPv4(input) || IsValidIPv6(input);

        /// <summary>
        /// Determines whether the specified string is a valid domain name,
        /// including support for internationalized domain names (IDN).
        /// <para><i>Note to self: Maybe I should download https://data.iana.org/TLD/tlds-alpha-by-domain.txt to actually validate top-level domains. But hey, one can dream, right?</i></para>
        /// </summary>
        /// <param name="domain">The domain name to validate.</param>
        /// <returns>
        /// <c>true</c> if the input is a valid domain name (ASCII or IDN); otherwise, <c>false</c>.
        /// </returns>
        public static bool IsValidDomain(string domain)
        {
            if (string.IsNullOrWhiteSpace(domain))
                return false;

            domain = domain.Trim();

            if (domain.StartsWith(".") || domain.EndsWith("."))
                return false;

            if (domain.Contains(".."))
                return false;

            try
            {
                var idn = new IdnMapping();
                domain = idn.GetAscii(domain);
            }
            catch
            {
                return false;
            }

            if (domain.Length > 253)
                return false;

            var labels = domain.Split('.');
            if (labels.Length < 2)
                return false;

            for (int i = 0; i < labels.Length; i++)
            {
                var label = labels[i];

                if (label.Length < 1 || label.Length > 63)
                    return false;

                if (!LabelRegex.IsMatch(label))
                    return false;

                if (i == labels.Length - 1 && !string.IsNullOrEmpty(label) && label.All(char.IsDigit))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Determines whether the specified integer is a valid TCP/UDP port number (0–65535).
        /// </summary>
        /// <param name="port">The port number to validate.</param>
        /// <returns><c>true</c> if the port is within the valid range; otherwise, <c>false</c>.</returns>
        public static bool IsValidPort(int port) => port >= 0 && port <= 65535;

        /// <summary>
        /// Determines whether the specified string represents a valid TCP/UDP port number (0–65535).
        /// </summary>
        /// <param name="input">The port number string to validate.</param>
        /// <returns><c>true</c> if the string is a valid port number; otherwise, <c>false</c>.</returns>
        public static bool IsValidPort(string input) =>
            Regex.IsMatch(input, @"^(0|[1-9]\d{0,4})$") &&
            int.TryParse(input, out int port) &&
            IsValidPort(port);

        /// <summary>
        /// Determines whether the specified string is a valid URL path.
        /// </summary>
        /// <param name="path">The URL path string to validate.</param>
        /// <returns><c>true</c> if the string is a valid URL path; otherwise, <c>false</c>.</returns>
        public static bool IsValidUrlPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            if (!path.StartsWith("/"))
                return false;

            return UrlPathRegex.IsMatch(path);
        }

        /// <summary>
        /// Determines whether the specified string is a valid HTTP header name,
        /// following RFC 9110. Only ASCII letters, digits, and these characters are allowed:
        /// <c>!#$%&'*+-.^_`|~</c>.
        /// <para><i>Note to self: Header names are case-insensitive, but this validation
        /// only checks for allowed characters and non-empty string.</i></para>
        /// </summary>
        /// <param name="name">The HTTP header name to validate.</param>
        /// <returns>
        /// <c>true</c> if the input is a valid HTTP header name; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsValidHttpHeaderName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return HttpHeaderNameRegex.IsMatch(name);
        }

        /// <summary>
        /// Determines whether the specified string is a valid HTTP header value,
        /// following RFC 9110. Allows visible ASCII, whitespace, and extended ASCII characters.
        /// Control characters (except HTAB) are not allowed.
        /// <para><i>Note to self: Empty string is allowed, but <c>null</c> is not.</i></para>
        /// </summary>
        /// <param name="value">The HTTP header value to validate.</param>
        /// <returns>
        /// <c>true</c> if the input is a valid HTTP header value; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsValidHttpHeaderValue(string value)
        {
            if (value == null) return false;
            return HttpHeaderValueRegex.IsMatch(value);
        }

        /// <summary>
        /// Determines whether the specified string is a valid ALPN (Application-Layer Protocol Negotiation) protocol name.
        /// <para>ALPN protocol names must be 1 to 255 bytes long and contain only printable ASCII characters (0x20-0x7E).</para>
        /// </summary>
        /// <param name="protocolName">The ALPN protocol name to validate.</param>
        /// <returns>
        /// <c>true</c> if the input is a valid ALPN protocol name; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsValidAlpnName(string protocolName)
        {
            if (string.IsNullOrEmpty(protocolName)) return false;
            return AlpnNameRegex.IsMatch(protocolName);
        }

        /// <summary>
        /// A collection of reserved IPv6 prefixes that are excluded from being
        /// classified as routable Global Unicast addresses.
        /// Based on IANA IPv6 Address Space Registry and relevant RFCs.
        /// </summary>
        private static readonly List<(byte[] Prefix, int PrefixLength)> ReservedPrefixes =
        [
            // Documentation address block 2001:db8::/32 (RFC 3849)
            (new byte[] { 0x20, 0x01, 0x0d, 0xb8 }, 32),

            // ORCHIDv2 2001:20::/28 (RFC 7343)
            (new byte[] { 0x20, 0x01, 0x20 }, 28),

            // 6to4 transition addresses 2002::/16 (RFC 3056, deprecated by RFC 7526)
            (new byte[] { 0x20, 0x02 }, 16),

            // 6bone test addresses 3ffe::/16 (deprecated, RFC 3701)
            (new byte[] { 0x3f, 0xfe }, 16)
        ];

        /// <summary>
        /// Determines the IPv6 address type from a string representation.
        /// Returns <see cref="IPv6AddressType.Unknown"/> if the input is null,
        /// whitespace, or not a valid IPv6 address.
        /// </summary>
        public static IPv6AddressType GetIPv6AddressType(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return IPv6AddressType.Unknown;

            return IPAddress.TryParse(input, out var address)
                ? GetIPv6AddressType(address)
                : IPv6AddressType.Unknown;
        }

        /// <summary>
        /// Determines the IPv6 address type from an <see cref="IPAddress"/> instance.
        /// Supports Loopback, Link-local, Unique Local, Multicast, and Global Unicast.
        /// Returns <see cref="IPv6AddressType.Unknown"/> if the address is not IPv6.
        /// </summary>
        public static IPv6AddressType GetIPv6AddressType(IPAddress address)
        {
            if (address.AddressFamily != AddressFamily.InterNetworkV6)
                return IPv6AddressType.Unknown;

            byte[] bytes = address.GetAddressBytes();

            if (IPAddress.IPv6Loopback.Equals(address))
                return IPv6AddressType.Loopback;

            if ((bytes[0] == 0xfe) && ((bytes[1] & 0xc0) == 0x80))
                return IPv6AddressType.LinkLocal;

            if ((bytes[0] & 0xfe) == 0xfc)
                return IPv6AddressType.UniqueLocal;

            if (bytes[0] == 0xff)
                return IPv6AddressType.Multicast;

            ushort prefix = (ushort)((bytes[0] << 8) | bytes[1]);
            if (prefix >= 0x2000 && prefix <= 0x3fff)
            {
                if (IsReservedGlobalUnicast(bytes))
                    return IPv6AddressType.Other;

                return IPv6AddressType.GlobalUnicast;
            }

            return IPv6AddressType.Other;
        }


        /// <summary>
        /// Checks if the given IPv6 address belongs to one of the reserved
        /// Global Unicast ranges defined in <see cref="ReservedPrefixes"/>.
        /// </summary>
        private static bool IsReservedGlobalUnicast(byte[] bytes)
        {
            foreach (var (prefix, length) in ReservedPrefixes)
            {
                if (MatchesPrefix(bytes, prefix, length))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Tests whether the provided IPv6 address matches a given prefix
        /// up to the specified prefix length in bits.
        /// </summary>
        private static bool MatchesPrefix(byte[] address, byte[] prefix, int prefixLength)
        {
            int fullBytes = prefixLength / 8;
            int remainingBits = prefixLength % 8;

            for (int i = 0; i < fullBytes; i++)
                if (address[i] != prefix[i]) return false;

            if (remainingBits > 0)
            {
                byte mask = (byte)(0xFF << (8 - remainingBits));
                if ((address[fullBytes] & mask) != (prefix[fullBytes] & mask))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Determines whether the specified IPv6 address requires
        /// public IPv6 connectivity to be reachable.
        /// Returns <c>true</c> for valid Global Unicast addresses,
        /// excluding reserved or non-routable ranges.
        /// </summary>
        public static bool RequiresPublicIPv6(string input)
        {
            var type = GetIPv6AddressType(input);
            return type == IPv6AddressType.GlobalUnicast;
        }
    }
}
