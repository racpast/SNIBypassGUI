﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using SNIBypassGUI.Models;
using static SNIBypassGUI.Utils.CommandUtils;
using static SNIBypassGUI.Utils.LogManager;
using static SNIBypassGUI.Utils.NetworkUtils;

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
        /// 获取指定范围的网络适配器的信息，并返回 NetworkAdapter 列表。
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
        /// 获取指定条件的网络适配器的信息，并返回 NetworkAdapter 列表。
        /// </summary>
        public static List<NetworkAdapter> GetNetworkAdapters(string condition)
        {
            Dictionary<string, string[]> GUIDToIPv6DNSServer = [];
            foreach (var netInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                var ipv6Dns = netInterface.GetIPProperties().DnsAddresses
                    .Where(ip => ip.AddressFamily == AddressFamily.InterNetworkV6)
                    .Select(ip => ip.ToString());
                GUIDToIPv6DNSServer.Add(netInterface.Id, [.. ipv6Dns]);
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
                    bool isIPv6DNSAuto = true;

                    if (!string.IsNullOrEmpty(guid))
                    {
                        string path = "HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters\\Interfaces\\" + guid;
                        string ns = (string)Registry.GetValue(path, "NameServer", null);
                        isIPv4DNSAuto = string.IsNullOrEmpty(ns);
                        string path2 = "HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Services\\Tcpip6\\Parameters\\Interfaces\\" + guid;
                        string ns2 = (string)Registry.GetValue(path2, "NameServer", null);
                        isIPv6DNSAuto = string.IsNullOrEmpty(ns2);
                    }

                    // 查询 IP 配置信息
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

                        // 处理 DNS 服务器
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
                        isIPv6DNSAuto,
                        isIPv6Enabled,
                        [.. ipv6Address],
                        [.. ipv6PrefixLength],
                        ipv6DnsServer
                    ));
                }
                catch (Exception ex)
                {
                    WriteLog("获取适配器信息时遇到异常。", LogLevel.Error, ex);
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
        /// 设置指定网络适配器的 IPv4 DNS 服务器。
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
                    WriteLog($"设置 {networkAdapter.FriendlyName} 的 IPv4 DNS 服务器时遇到异常。", LogLevel.Error, ex);
                }
            }
        }

        /// <summary>
        /// 设置指定网络适配器的 IPv6 DNS 服务器。
        /// </summary>
        public static async Task SetIPv6DNS(NetworkAdapter networkAdapter, string[] dnsServers)
        {
            try
            {
                string friendlyName = networkAdapter.FriendlyName;
                if (string.IsNullOrEmpty(friendlyName))
                {
                    WriteLog($"网络适配器 {networkAdapter.Name} 的 FriendlyName 为空，无法设置 DNS。", LogLevel.Warning);
                    return;
                }

                if (dnsServers.Length == 0)
                {
                    var (success, output, error) = await RunCommand($"netsh interface ipv6 set dnsservers name=\"{friendlyName}\" source=dhcp");
                    if (!success)
                    {
                        WriteLog($"设置 {friendlyName} 的 IPv6 DNS 为 DHCP 时遇到异常，错误：{error}。", LogLevel.Error);
                        return;
                    }
                }
                else
                {
                    string firstDNS = dnsServers[0];
                    var (success, output, error) = await RunCommand($"netsh interface ipv6 set dnsservers name=\"{friendlyName}\" static address={firstDNS} primary");
                    if (!success)
                    {
                        WriteLog($"设置 {friendlyName} 的 IPv6 DNS 时遇到异常，错误：{error}。", LogLevel.Error);
                        return;
                    }

                    for (int i = 1; i < dnsServers.Length; i++)
                    {
                        var (successAdd, outputAdd, errorAdd) = await RunCommand($"netsh interface ipv6 add dnsservers name=\"{friendlyName}\" address={dnsServers[i]} index={i + 1}");
                        if (!successAdd)
                        {
                            WriteLog($"设置 {friendlyName} 的 IPv6 DNS 时遇到异常，索引：{i + 1}，错误：{error}。", LogLevel.Error);
                            return;
                        }
                    }
                }
                FlushDNSCache();
            }
            catch (Exception ex)
            {
                WriteLog($"设置 {networkAdapter.FriendlyName} 的 IPv6 DNS 时遇到异常。", LogLevel.Error, ex);
            }
        }

        /// <summary>
        /// 获取默认路由的接口索引。
        /// </summary>
        public static uint? GetDefaultRouteInterfaceIndex()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_IP4RouteTable WHERE Destination='0.0.0.0' AND Mask='0.0.0.0'");
                var routes = searcher.Get().Cast<ManagementObject>()
                    .Where(r => r["Metric1"] != null && (int)r["Metric1"] != -1)
                    .OrderBy(r => (int)r["Metric1"])
                    .ToList();

                if (routes.Any())
                {
                    var firstRoute = routes.First();
                    var interfaceIndex = firstRoute["InterfaceIndex"];

                    // 检查类型并转换
                    if (interfaceIndex is int indexInt)
                    {
                        if (indexInt >= 0) return (uint)indexInt;
                        else
                        {
                            WriteLog("InterfaceIndex 为负数，无法转换为 uint。", LogLevel.Error);
                            return null;
                        }
                    }
                    else
                    {
                        WriteLog($"InterfaceIndex 不是 int 类型，而是 {interfaceIndex?.GetType().ToString() ?? "null"}。", LogLevel.Error);
                        return null;
                    }
                }
                else
                {
                    WriteLog("未找到符合条件的默认路由。", LogLevel.Debug);
                    return null;
                }
            }
            catch (Exception ex)
            {
                WriteLog("获取默认路由的接口索引时遇到异常。", LogLevel.Error, ex);
                return null;
            }
        }
    }
}
