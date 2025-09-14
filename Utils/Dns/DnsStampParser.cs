using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using SNIBypassGUI.Enums;
using SNIBypassGUI.Models;
using SNIBypassGUI.Utils.Extensions;

namespace SNIBypassGUI.Utils.Dns
{
    public static class DnsStampParser
    {
        private const int DefaultPort = 443;
        private const int DefaultDnsPort = 53;
        private const string StampScheme = "sdns://";

        /// <summary>
        /// Tries to parse a DNS stamp string into a ServerStamp object.
        /// This method does not throw exceptions for invalid formats.
        /// </summary>
        /// <param name="stampStr">The stamp string to parse.</param>
        /// <param name="stamp">When this method returns, contains the parsed ServerStamp object if parsing succeeded, or null if it failed.</param>
        /// <returns>true if the stamp string was parsed successfully; otherwise, false.</returns>
        public static bool TryParse(string stampStr, out ServerStamp stamp)
        {
            try
            {
                stamp = Parse(stampStr);
                return true;
            }
            catch (Exception)
            {
                stamp = null;
                return false;
            }
        }

        /// <summary>
        /// Tries to parse a combined Relay and Server stamp string.
        /// This method does not throw exceptions for invalid formats.
        /// </summary>
        /// <param name="stampStr">The combined stamp string to parse.</param>
        /// <param name="result">When this method returns, contains a tuple with the Relay and Server stamps if parsing succeeded, or a default tuple (null, null) if it failed.</param>
        /// <returns>true if the stamp string was parsed successfully; otherwise, false.</returns>
        public static bool TryParseRelayAndServerStamp(string stampStr, out (ServerStamp Relay, ServerStamp Server) result)
        {
            try
            {
                result = ParseRelayAndServerStamp(stampStr);
                return true;
            }
            catch (Exception)
            {
                result = default; // (null, null)
                return false;
            }
        }

        /// <summary>
        /// Parses a DNS stamp string into a ServerStamp object.
        /// </summary>
        /// <param name="stampStr">The stamp string, starting with "sdns://".</param>
        /// <returns>A ServerStamp object.</returns>
        /// <exception cref="ArgumentException">Thrown if the stamp string is invalid or unsupported.</exception>
        public static ServerStamp Parse(string stampStr)
        {
            if (string.IsNullOrEmpty(stampStr) || !stampStr.StartsWith(StampScheme, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Stamps must start with \"sdns://\"");

            string b64 = stampStr.Substring(StampScheme.Length);
            byte[] bin = FromRawUrlBase64(b64);

            if (bin.Length < 1)
                throw new ArgumentException("Stamp is too short.");

            var proto = (StampProtoType)bin[0];
            return proto switch
            {
                StampProtoType.Plain => ParsePlainStamp(bin),
                StampProtoType.DnsCrypt => ParseDnsCryptStamp(bin),
                StampProtoType.DoH => ParseDoHStamp(bin),
                StampProtoType.ODoHTarget => ParseODoHTargetStamp(bin),
                StampProtoType.DnsCryptRelay => ParseDnsCryptRelayStamp(bin),
                StampProtoType.ODoHRelay => ParseODoHRelayStamp(bin),
                _ => throw new ArgumentException($"Unsupported stamp protocol: {proto.GetName()}"),
            };
        }

        /// <summary>
        /// Parses a combined Relay and Server stamp string.
        /// </summary>
        /// <param name="stampStr">The combined stamp string, e.g., "sdns://relaystamp/serverstamp".</param>
        /// <returns>A tuple containing the Relay stamp and the Server stamp.</returns>
        /// <exception cref="ArgumentException">Thrown if the format is incorrect or stamps are invalid.</exception>
        public static (ServerStamp Relay, ServerStamp Server) ParseRelayAndServerStamp(string stampStr)
        {
            if (string.IsNullOrEmpty(stampStr) || !stampStr.StartsWith(StampScheme, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Stamps must start with \"sdns://\"");

            string path = stampStr.Substring(StampScheme.Length);
            string[] parts = path.Split(['/'], 2);
            if (parts.Length != 2)
                throw new ArgumentException("This is not a relay+server stamp.");

            var relayStamp = Parse(StampScheme + parts[0]);
            var serverStamp = Parse(StampScheme + parts[1]);

            if (relayStamp.Proto != StampProtoType.DnsCryptRelay && relayStamp.Proto != StampProtoType.ODoHRelay)
                throw new ArgumentException("First stamp is not a relay.");
            if (serverStamp.Proto == StampProtoType.DnsCryptRelay || serverStamp.Proto == StampProtoType.ODoHRelay)
                throw new ArgumentException("Second stamp is a relay.");

            return (relayStamp, serverStamp);
        }

        /// <summary>
        /// Creates a DNSCrypt ServerStamp from legacy parameters.
        /// </summary>
        /// <param name="serverAddrStr">Server address, with or without port.</param>
        /// <param name="serverPkStr">The server's public key as a hex string (colons are ignored).</param>
        /// <param name="providerName">The provider name.</param>
        /// <param name="props">The server informal properties.</param>
        /// <returns>A new DNSCrypt ServerStamp.</returns>
        public static ServerStamp CreateDnsCryptLegacy(string serverAddrStr, string serverPkStr, string providerName, ServerInformalProperties props)
        {
            if (!IPAddress.TryParse(serverAddrStr, out _))
                serverAddrStr = $"{serverAddrStr}:{DefaultPort}";

            byte[] serverPk;
            try
            {
                serverPk = serverPkStr.Replace(":", "").FromHexString();
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Unsupported public key: [{serverPkStr}]", ex);
            }

            if (serverPk.Length != 32)
            {
                throw new ArgumentException($"Unsupported public key: [{serverPkStr}]");
            }

            return new ServerStamp
            {
                Proto = StampProtoType.DnsCrypt,
                Props = props,
                ServerAddrStr = serverAddrStr,
                ServerPk = serverPk,
                ProviderName = providerName
            };
        }

        /// <summary>
        /// Gets a human-readable name for a protocol type.
        /// </summary>
        public static string GetName(this StampProtoType protoType) => protoType switch
        {
            StampProtoType.Plain => "Plain",
            StampProtoType.DnsCrypt => "DNSCrypt",
            StampProtoType.DoH => "DoH",
            StampProtoType.Tls => "TLS",
            StampProtoType.DoQ => "QUIC",
            StampProtoType.ODoHTarget => "oDoH target",
            StampProtoType.DnsCryptRelay => "DNSCrypt relay",
            StampProtoType.ODoHRelay => "oDoH relay",
            _ => "(unknown)"
        };

        /// <summary>
        /// Converts the ServerStamp object back to its string representation (e.g., "sdns://...").
        /// </summary>
        /// <returns>The DNS stamp string.</returns>
        public static string ToString(ServerStamp stamp) => stamp.Proto switch
        {
            StampProtoType.Plain => ToPlainString(stamp),
            StampProtoType.DnsCrypt => ToDnsCryptString(stamp),
            StampProtoType.DoH => ToDohString(stamp),
            StampProtoType.ODoHTarget => ToODohTargetString(stamp),
            StampProtoType.DnsCryptRelay => ToDnsCryptRelayString(stamp),
            StampProtoType.ODoHRelay => ToODohRelayString(stamp),
            _ => throw new NotSupportedException("Unsupported protocol for stringification.")
        };

        private static ServerStamp ParsePlainStamp(byte[] bin)
        {
            var stamp = new ServerStamp { Proto = StampProtoType.Plain };
            if (bin.Length < 10) throw new ArgumentException("Stamp is too short");

            int pos = 1;
            stamp.Props = (ServerInformalProperties)ReadUInt64(bin, ref pos);
            stamp.ServerAddrStr = ReadLpString(bin, ref pos);

            stamp.ServerAddrStr = NormalizeAddress(stamp.ServerAddrStr, DefaultDnsPort);
            ValidateAddress(stamp.ServerAddrStr);

            if (pos != bin.Length) throw new ArgumentException("Invalid stamp (garbage after end)");
            return stamp;
        }

        private static ServerStamp ParseDnsCryptStamp(byte[] bin)
        {
            var stamp = new ServerStamp { Proto = StampProtoType.DnsCrypt };
            if (bin.Length < 66) throw new ArgumentException("Stamp is too short");

            int pos = 1;
            stamp.Props = (ServerInformalProperties)ReadUInt64(bin, ref pos);
            stamp.ServerAddrStr = ReadLpString(bin, ref pos);
            stamp.ServerAddrStr = NormalizeAddress(stamp.ServerAddrStr, DefaultPort);
            ValidateAddress(stamp.ServerAddrStr);

            stamp.ServerPk = ReadLpBlock(bin, ref pos);
            stamp.ProviderName = ReadLpString(bin, ref pos);

            if (pos != bin.Length) throw new ArgumentException("Invalid stamp (garbage after end)");
            return stamp;
        }

        private static ServerStamp ParseDoHStamp(byte[] bin)
        {
            var stamp = new ServerStamp { Proto = StampProtoType.DoH };
            if (bin.Length < 15) throw new ArgumentException("Stamp is too short");

            int pos = 1;
            stamp.Props = (ServerInformalProperties)ReadUInt64(bin, ref pos);
            stamp.ServerAddrStr = ReadLpString(bin, ref pos);
            stamp.Hashes = ReadHashes(bin, ref pos);
            stamp.ProviderName = ReadLpString(bin, ref pos);
            stamp.Path = ReadLpString(bin, ref pos);

            if (!string.IsNullOrEmpty(stamp.ServerAddrStr))
            {
                stamp.ServerAddrStr = NormalizeAddress(stamp.ServerAddrStr, DefaultPort);
                ValidateAddress(stamp.ServerAddrStr);
            }

            if (pos != bin.Length) throw new ArgumentException("Invalid stamp (garbage after end)");
            return stamp;
        }

        private static ServerStamp ParseODoHTargetStamp(byte[] bin)
        {
            var stamp = new ServerStamp { Proto = StampProtoType.ODoHTarget };
            if (bin.Length < 12) throw new ArgumentException("Stamp is too short");

            int pos = 1;
            stamp.Props = (ServerInformalProperties)ReadUInt64(bin, ref pos);
            stamp.ProviderName = ReadLpString(bin, ref pos);
            stamp.Path = ReadLpString(bin, ref pos);

            if (pos != bin.Length) throw new ArgumentException("Invalid stamp (garbage after end)");
            return stamp;
        }

        private static ServerStamp ParseDnsCryptRelayStamp(byte[] bin)
        {
            var stamp = new ServerStamp { Proto = StampProtoType.DnsCryptRelay };
            if (bin.Length < 9) throw new ArgumentException("Stamp is too short");

            int pos = 1;
            stamp.ServerAddrStr = ReadLpString(bin, ref pos);
            stamp.ServerAddrStr = NormalizeAddress(stamp.ServerAddrStr, DefaultPort);
            ValidateAddress(stamp.ServerAddrStr);

            if (pos != bin.Length) throw new ArgumentException("Invalid stamp (garbage after end)");
            return stamp;
        }

        private static ServerStamp ParseODoHRelayStamp(byte[] bin)
        {
            var stamp = new ServerStamp { Proto = StampProtoType.ODoHRelay };
            if (bin.Length < 13) throw new ArgumentException("Stamp is too short");

            int pos = 1;
            stamp.Props = (ServerInformalProperties)ReadUInt64(bin, ref pos);
            stamp.ServerAddrStr = ReadLpString(bin, ref pos);
            stamp.Hashes = ReadHashes(bin, ref pos);
            stamp.ProviderName = ReadLpString(bin, ref pos);
            stamp.Path = ReadLpString(bin, ref pos);

            if (!string.IsNullOrEmpty(stamp.ServerAddrStr))
            {
                stamp.ServerAddrStr = NormalizeAddress(stamp.ServerAddrStr, DefaultPort);
                ValidateAddress(stamp.ServerAddrStr);
            }

            if (pos != bin.Length) throw new ArgumentException("Invalid stamp (garbage after end)");
            return stamp;
        }

        private static string NormalizeAddress(string address, int defaultPort)
        {
            if (string.IsNullOrEmpty(address)) return address;

            int bracketIndex = address.LastIndexOf(']');
            int colIndex = address.LastIndexOf(':');

            // If colon is part of IPv6 address, ignore it
            if (colIndex < bracketIndex) colIndex = -1;

            if (colIndex == -1) return $"{address}:{defaultPort}";
            return address;
        }

        private static void ValidateAddress(string fullAddress)
        {
            int bracketIndex = fullAddress.LastIndexOf(']');
            int colIndex = fullAddress.LastIndexOf(':');

            // If colon is part of IPv6 address, ignore it
            if (colIndex < bracketIndex)
            {
                throw new ArgumentException("Invalid stamp (missing port)");
            }

            if (colIndex >= fullAddress.Length - 1)
            {
                throw new ArgumentException("Invalid stamp (empty port)");
            }

            string ipOnly = fullAddress.Substring(0, colIndex).TrimStart('[').TrimEnd(']');
            string portOnly = fullAddress.Substring(colIndex + 1);

            if (!IPAddress.TryParse(ipOnly, out _))
            {
                throw new ArgumentException("Invalid stamp (IP address)");
            }
            if (!ushort.TryParse(portOnly, out _))
            {
                throw new ArgumentException("Invalid stamp (port range)");
            }
        }

        private static ulong ReadUInt64(byte[] buffer, ref int pos)
        {
            if (pos + 8 > buffer.Length) throw new ArgumentException("Invalid stamp (truncated properties)");
            // DNS stamps use Little Endian. BitConverter respects system endianness.
            // We assume we are on a Little Endian system, which is true for most modern hardware.
            // For full portability, a manual conversion would be required if Big Endian is possible.
            if (!BitConverter.IsLittleEndian) Array.Reverse(buffer, pos, 8);
            var val = BitConverter.ToUInt64(buffer, pos);
            pos += 8;
            return val;
        }

        private static string ReadLpString(byte[] buffer, ref int pos)
        {
            var block = ReadLpBlock(buffer, ref pos);
            return Encoding.UTF8.GetString(block);
        }

        private static byte[] ReadLpBlock(byte[] buffer, ref int pos)
        {
            if (pos >= buffer.Length) throw new ArgumentException("Invalid stamp (missing length prefix)");
            int len = buffer[pos];
            pos++;
            if (pos + len > buffer.Length) throw new ArgumentException("Invalid stamp (truncated content)");

            var block = new byte[len];
            Buffer.BlockCopy(buffer, pos, block, 0, len);
            pos += len;
            return block;
        }

        private static List<byte[]> ReadHashes(byte[] buffer, ref int pos)
        {
            var hashes = new List<byte[]>();
            while (true)
            {
                if (pos >= buffer.Length) throw new ArgumentException("Invalid stamp (truncated hashes)");

                int vlen = buffer[pos];
                pos++;
                int len = vlen & ~0x80;

                if (pos + len > buffer.Length) throw new ArgumentException("Invalid stamp (truncated hash content)");

                if (len > 0)
                {
                    var hash = new byte[len];
                    Buffer.BlockCopy(buffer, pos, hash, 0, len);
                    hashes.Add(hash);
                }
                pos += len;

                if ((vlen & 0x80) != 0x80) break;
            }
            return hashes;
        }

        private static string ToPlainString(ServerStamp stamp)
        {
            var bytes = new List<byte>(64) { (byte)stamp.Proto };
            bytes.AddRange(BitConverter.GetBytes((ulong)stamp.Props));

            string addr = stamp.ServerAddrStr;
            if (addr != null && addr.EndsWith($":{DefaultDnsPort}"))
                addr = addr.Substring(0, addr.Length - (1 + DefaultDnsPort.ToString().Length));
            WriteLpString(bytes, addr);

            return StampScheme + ToRawUrlBase64([.. bytes]);
        }

        private static string ToDnsCryptString(ServerStamp stamp)
        {
            var bytes = new List<byte>(128) { (byte)stamp.Proto };
            bytes.AddRange(BitConverter.GetBytes((ulong)stamp.Props));

            string addr = stamp.ServerAddrStr;
            if (addr != null && addr.EndsWith($":{DefaultPort}"))
            {
                addr = addr.Substring(0, addr.Length - (1 + DefaultPort.ToString().Length));
            }
            WriteLpString(bytes, addr);
            WriteLpBlock(bytes, stamp.ServerPk);
            WriteLpString(bytes, stamp.ProviderName);

            return StampScheme + ToRawUrlBase64([.. bytes]);
        }

        private static string ToDohString(ServerStamp stamp)
        {
            var bytes = new List<byte>(256) { (byte)stamp.Proto };
            bytes.AddRange(BitConverter.GetBytes((ulong)stamp.Props));

            string addr = stamp.ServerAddrStr;
            if (addr != null && addr.EndsWith($":{DefaultPort}"))
                addr = addr.Substring(0, addr.Length - (1 + DefaultPort.ToString().Length));
            WriteLpString(bytes, addr);
            WriteHashes(bytes, stamp.Hashes);
            WriteLpString(bytes, stamp.ProviderName);
            WriteLpString(bytes, stamp.Path);

            return StampScheme + ToRawUrlBase64([.. bytes]);
        }

        private static string ToODohTargetString(ServerStamp stamp)
        {
            var bytes = new List<byte>(256) { (byte)stamp.Proto };
            bytes.AddRange(BitConverter.GetBytes((ulong)stamp.Props));

            WriteLpString(bytes, stamp.ProviderName);
            WriteLpString(bytes, stamp.Path);

            return StampScheme + ToRawUrlBase64([.. bytes]);
        }

        private static string ToDnsCryptRelayString(ServerStamp stamp)
        {
            var bytes = new List<byte>(64) { (byte)stamp.Proto };

            string addr = stamp.ServerAddrStr;
            if (addr != null && addr.EndsWith($":{DefaultPort}"))
                addr = addr.Substring(0, addr.Length - (1 + DefaultPort.ToString().Length));
            WriteLpString(bytes, addr);

            return StampScheme + ToRawUrlBase64([.. bytes]);
        }

        private static string ToODohRelayString(ServerStamp stamp)
        {
            var bytes = new List<byte>(256) { (byte)stamp.Proto };
            bytes.AddRange(BitConverter.GetBytes((ulong)stamp.Props));

            string addr = stamp.ServerAddrStr;
            if (addr != null && addr.EndsWith($":{DefaultPort}"))
                addr = addr.Substring(0, addr.Length - (1 + DefaultPort.ToString().Length));
            WriteLpString(bytes, addr);
            WriteHashes(bytes, stamp.Hashes);
            WriteLpString(bytes, stamp.ProviderName);
            WriteLpString(bytes, stamp.Path);

            return StampScheme + ToRawUrlBase64([.. bytes]);
        }

        private static void WriteLpString(List<byte> buffer, string value)
        {
            value ??= "";
            var bytes = Encoding.UTF8.GetBytes(value);
            WriteLpBlock(buffer, bytes);
        }

        private static void WriteLpBlock(List<byte> buffer, byte[] value)
        {
            value ??= [];
            if (value.Length > 255) throw new ArgumentException("Length-prefixed value cannot exceed 255 bytes.");
            buffer.Add((byte)value.Length);
            buffer.AddRange(value);
        }

        private static void WriteHashes(List<byte> buffer, List<byte[]> hashes)
        {
            hashes ??= [];
            if (hashes.Count == 0)
            {
                buffer.Add(0);
                return;
            }

            for (int i = 0; i < hashes.Count; i++)
            {
                var hash = hashes[i];
                if (hash.Length > 127) throw new ArgumentException("Hash length cannot exceed 127 bytes.");

                int vlen = hash.Length;
                if (i < hashes.Count - 1) vlen |= 0x80;
                buffer.Add((byte)vlen);
                buffer.AddRange(hash);
            }
        }

        private static string ToRawUrlBase64(byte[] data)
        {
            return Convert.ToBase64String(data)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static byte[] FromRawUrlBase64(string s)
        {
            try
            {
                s = s.Replace('-', '+').Replace('_', '/');
                s = s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=');
                return Convert.FromBase64String(s);
            }
            catch (Exception ex)
            {
                throw new ArgumentException("Invalid Base64 string", ex);
            }
        }
    }
}
