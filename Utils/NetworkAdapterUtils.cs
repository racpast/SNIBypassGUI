using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Win32;
using SNIBypassGUI.Models;
using static SNIBypassGUI.Utils.LogManager;
using static SNIBypassGUI.Utils.CommandUtils;

namespace SNIBypassGUI.Utils
{
    public static class NetworkAdapterUtils
    {
        public enum ScopeNeeded
        {
            All,
            EnabledOnly,
            ConnectedOnly,
            PhysicalOnly,
            FriendlyNameNotNullOnly
        }

        /// <summary>
        /// 获取指定范围的网络适配器的信息，并返回NetworkAdapter列表
        /// </summary>
        public static List<NetworkAdapter> GetNetworkAdapters(ScopeNeeded scopeNeeded = ScopeNeeded.All)
        {
            string condition = scopeNeeded switch
            {
                ScopeNeeded.All => "",
                ScopeNeeded.EnabledOnly => " WHERE NetEnabled=True",
                ScopeNeeded.ConnectedOnly => " WHERE NetConnectionStatus=2",
                ScopeNeeded.PhysicalOnly => " WHERE PhysicalAdapter=True",
                ScopeNeeded.FriendlyNameNotNullOnly => " WHERE (NetConnectionID IS NOT NULL)",
                _ => throw new ArgumentOutOfRangeException(nameof(scopeNeeded), scopeNeeded, "无效的查询范围。"),
            };
            return GetNetworkAdapters(condition);
        }

        /// <summary>
        /// 获取指定条件的网络适配器的信息，并返回NetworkAdapter列表
        /// </summary>
        public static List<NetworkAdapter> GetNetworkAdapters(string condition)
        {
            Dictionary<string, string[]> GUIDToIPv6DNSServer = [];
            foreach (var netInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                var ipv6Dns = netInterface.GetIPProperties().DnsAddresses
                    .Where(ip => ip.AddressFamily == AddressFamily.InterNetworkV6)
                    .Select(ip => ip.ToString());
                GUIDToIPv6DNSServer.Add(netInterface.Id, ipv6Dns.ToArray());
            }

            List<NetworkAdapter> adapters = [];
            using ManagementObjectSearcher searcher = new("SELECT * FROM Win32_NetworkAdapter" + condition);
            using ManagementObjectCollection adapterCollection = searcher.Get();

            foreach (ManagementObject adapter in adapterCollection.Cast<ManagementObject>())
            {
                try
                {
                    string name = adapter["Name"]?.ToString() ?? "";
                    string friendlyName = adapter["NetConnectionID"]?.ToString() ?? "";
                    string description = adapter["Description"]?.ToString() ?? "";
                    string serviceName = adapter["ServiceName"]?.ToString() ?? "";
                    uint interfaceIndex = (uint)(adapter["InterfaceIndex"] ?? 0);
                    string macAddress = adapter["MACAddress"]?.ToString() ?? "";
                    string manufacturer = adapter["Manufacturer"]?.ToString() ?? "";
                    bool isPhysicalAdapter = (bool)(adapter["PhysicalAdapter"] ?? false);
                    string guid = adapter["GUID"]?.ToString() ?? "";
                    bool isNetEnabled = (bool)(adapter["NetEnabled"] ?? false);
                    ushort netConnectionStatus = (ushort)(adapter["NetConnectionStatus"] ?? 0);
                    bool isIPv4DNSAuto = true;

                    if (!string.IsNullOrEmpty(guid))
                    {
                        string path = "HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters\\Interfaces\\" + guid;
                        string ns = (string)Registry.GetValue(path, "NameServer", null);
                        isIPv4DNSAuto = string.IsNullOrEmpty(ns);
                    }

                    // 查询IP配置信息
                    string ipQuery = $"SELECT * FROM Win32_NetworkAdapterConfiguration WHERE InterfaceIndex = {interfaceIndex}";
                    using ManagementObjectSearcher ipSearcher = new(ipQuery);
                    using ManagementObjectCollection ipConfigs = ipSearcher.Get();

                    bool isIPEnabled = false;
                    List<string> ipAddress = [];
                    List<string> ipSubnet = [];
                    string[] defaultIPGateway = [];
                    bool isDhcpEnabled = false;
                    string dhcpServer = "";
                    DateTime dhcpLeaseObtained = DateTime.MinValue;
                    DateTime dhcpLeaseExpires = DateTime.MinValue;
                    List<string> ipv4DnsServer = [];
                    bool isIPv6Enabled = false;
                    List<string> ipv6Address = [];
                    List<ushort> ipv6PrefixLength = [];
                    string[] ipv6DnsServer = [];

                    foreach (ManagementObject ipConfig in ipConfigs.Cast<ManagementObject>())
                    {
                        isIPEnabled = ipConfig["IPEnabled"] as bool? ?? false;
                        string[] iPAddresses = ipConfig["IPAddress"] as string[] ?? [];
                        foreach (string ip in iPAddresses)
                        {
                            if (ip.Contains(':')) ipv6Address.Add(ip);
                            else ipAddress.Add(ip);
                        }
                        isIPv6Enabled = ipv6Address.Count > 0;
                        string[] ipSubnets = ipConfig["IPSubnet"] as string[] ?? [];
                        foreach (string ip in ipSubnets)
                        {
                            if (ip.Contains('.')) ipSubnet.Add(ip);
                            else if (ushort.TryParse(ip, out ushort prefixLength)) ipv6PrefixLength.Add(prefixLength);
                        }
                        defaultIPGateway = ipConfig["DefaultIPGateway"] as string[] ?? [];
                        isDhcpEnabled = ipConfig["DHCPEnabled"] as bool? ?? false;
                        dhcpServer = ipConfig["DHCPServer"]?.ToString() ?? "";

                        // 解析DHCP租约时间
                        string dhcpLeaseObtainedRaw = ipConfig["DHCPLeaseObtained"]?.ToString();
                        string dhcpLeaseExpiresRaw = ipConfig["DHCPLeaseExpires"]?.ToString();
                        dhcpLeaseObtained = !string.IsNullOrEmpty(dhcpLeaseObtainedRaw) ? ManagementDateTimeConverter.ToDateTime(dhcpLeaseObtainedRaw) : DateTime.MinValue;
                        dhcpLeaseExpires = !string.IsNullOrEmpty(dhcpLeaseExpiresRaw) ? ManagementDateTimeConverter.ToDateTime(dhcpLeaseExpiresRaw) : DateTime.MinValue;

                        // 处理DNS服务器
                        string[] dnsServers = ipConfig["DNSServerSearchOrder"] as string[] ?? [];
                        ipv4DnsServer.AddRange(dnsServers.Where(dns => !dns.Contains(':')));
                        ipv6DnsServer = GUIDToIPv6DNSServer.ContainsKey(guid) ? GUIDToIPv6DNSServer[guid] : [];
                    }

                    adapters.Add(new NetworkAdapter(
                        name,
                        friendlyName,
                        description,
                        serviceName,
                        interfaceIndex,
                        macAddress,
                        manufacturer,
                        isPhysicalAdapter,
                        guid,
                        isNetEnabled,
                        netConnectionStatus,
                        isIPEnabled,
                        [.. ipAddress],
                        [.. ipSubnet],
                        defaultIPGateway,
                        isDhcpEnabled,
                        dhcpServer,
                        dhcpLeaseObtained,
                        dhcpLeaseExpires,
                        [.. ipv4DnsServer],
                        isIPv4DNSAuto,
                        isIPv6Enabled,
                        [.. ipv6Address],
                        [.. ipv6PrefixLength],
                        ipv6DnsServer
                    ));
                }
                catch (Exception ex)
                {
                    WriteLog("获取适配器信息时遇到错误。", LogLevel.Error, ex);
                    continue;
                }
            }
            return adapters;
        }

        /// <summary>
        /// 刷新指定的网络适配器信息
        /// </summary>
        public static NetworkAdapter Refresh(NetworkAdapter networkAdapter) => GetNetworkAdapters($" WHERE GUID=\"{networkAdapter.GUID}\"").FirstOrDefault();

        /// <summary>
        /// 设置指定网络适配器的IPv4 DNS服务器
        /// </summary>
        public static void SetIPv4DNS(NetworkAdapter networkAdapter, string[] dnsServers)
        {
            string query = $"SELECT * FROM Win32_NetworkAdapterConfiguration WHERE InterfaceIndex = {networkAdapter.InterfaceIndex}";
            using ManagementObjectSearcher searcher = new(query);
            using ManagementObjectCollection adapterCollection = searcher.Get();
            foreach (ManagementObject adapter in adapterCollection.Cast<ManagementObject>())
            {
                try
                {
                    adapter.InvokeMethod("SetDNSServerSearchOrder", [dnsServers]);
                }
                catch (Exception ex)
                {
                    WriteLog($"设置 {networkAdapter.FriendlyName} 的 IPv4 DNS 服务器时遇到错误。", LogLevel.Error, ex);
                }
            }
        }

        /// <summary>
        /// 设置指定网络适配器的IPv6 DNS服务器
        /// </summary>
        public static async void SetIPv6DNS(NetworkAdapter networkAdapter, string[] dnsServers)
        {
            try
            {
                var vs = new string[] { "SYSTEM", "CurrentControlSet", "Services", "Tcpip6", "Parameters", "Interfaces", networkAdapter.GUID };
                var path = string.Join(@"\", vs);
                var reg = Registry.LocalMachine.OpenSubKey(path, true);
                reg.SetValue("NameServer", string.Join(",", dnsServers));
                await RunCommand("ipconfig /renew6");
            }
            catch (Exception ex)
            {
                WriteLog($"设置 {networkAdapter.FriendlyName} 的 IPv6 DNS 服务器时遇到错误。", LogLevel.Error, ex);
            }
        }
    }
}
