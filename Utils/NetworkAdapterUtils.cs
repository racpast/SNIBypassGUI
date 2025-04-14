using Microsoft.Win32;
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
using static SNIBypassGUI.Utils.WinApiUtils;

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
        /// 异步获取指定条件的网络适配器的信息，并返回 NetworkAdapter 列表。
        /// </summary>
        public static Task<List<NetworkAdapter>> GetNetworkAdaptersAsync(ScopeNeeded scopeNeeded = ScopeNeeded.All)
        {
            string condition = scopeNeeded switch
            {
                ScopeNeeded.All => "",
                ScopeNeeded.EnabledOnly => " WHERE NetEnabled=True",
                ScopeNeeded.ConnectedOnly => " WHERE NetConnectionStatus=2",
                ScopeNeeded.PhysicalOnly => " WHERE PhysicalAdapter=True",
                ScopeNeeded.FriendlyNameNotNullOnly => " WHERE (NetConnectionID IS NOT NULL AND NetConnectionID <> '')",
                _ => throw new ArgumentOutOfRangeException(nameof(scopeNeeded), scopeNeeded, "无效的查询范围。"),
            };
            return GetNetworkAdaptersInternalAsync(condition);
        }

        // Internal async implementation doing the actual work
        private static async Task<List<NetworkAdapter>> GetNetworkAdaptersInternalAsync(string wmiCondition)
        {
            return await Task.Run(() =>
            {
                var adapters = new List<NetworkAdapter>();
                var guidToIPv6DNSServer = new Dictionary<string, string[]>();
                var interfaceIndexToConfig = new Dictionary<uint, ManagementObject>();
                var guidToIPv4DNSAuto = new Dictionary<string, bool>();
                var guidToIPv6DNSAuto = new Dictionary<string, bool>();

                try
                {
                    foreach (var netInterface in NetworkInterface.GetAllNetworkInterfaces())
                    {
                        try
                        {
                            var ipProps = netInterface.GetIPProperties();
                            var ipv6Dns = ipProps.DnsAddresses
                                .Where(ip => ip.AddressFamily == AddressFamily.InterNetworkV6)
                                .Select(ip => ip.ToString())
                                .ToArray();
                            if (!string.IsNullOrEmpty(netInterface.Id) && !guidToIPv6DNSServer.ContainsKey(netInterface.Id))
                            {
                                guidToIPv6DNSServer.Add(netInterface.Id, ipv6Dns);
                            }
                        }
                        catch (Exception ex)
                        {
                            WriteLog($"Error getting properties for NetworkInterface {netInterface.Id} ({netInterface.Name}): {ex.Message}", LogLevel.Warning);
                        }
                    }

                    string configQuery = "SELECT InterfaceIndex, IPEnabled, IPAddress, IPSubnet, DefaultIPGateway, DHCPEnabled, DHCPServer, DHCPLeaseObtained, DHCPLeaseExpires, DNSServerSearchOrder, MACAddress, Description, ServiceName FROM Win32_NetworkAdapterConfiguration";
                    using (var configSearcher = new ManagementObjectSearcher(configQuery))
                    using (var configCollection = configSearcher.Get())
                    {
                        foreach (ManagementObject config in configCollection.Cast<ManagementObject>())
                        {
                            try
                            {
                                uint ifIndex = (uint)config["InterfaceIndex"];
                                if (!interfaceIndexToConfig.ContainsKey(ifIndex))
                                {
                                    interfaceIndexToConfig.Add(ifIndex, config);
                                }
                            }
                            catch (Exception ex)
                            {
                                WriteLog($"处理适配器信息时遇到异常。", LogLevel.Error, ex);
                            }
                        }
                    }

                    try
                    {
                        using (var tcpipBaseKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces"))
                        {
                            if (tcpipBaseKey != null)
                            {
                                foreach (var guidKeyName in tcpipBaseKey.GetSubKeyNames())
                                {
                                    using var key = tcpipBaseKey.OpenSubKey(guidKeyName);
                                    if (key != null)
                                    {
                                        string ns = key.GetValue("NameServer", null) as string;
                                        guidToIPv4DNSAuto.Add(guidKeyName, string.IsNullOrEmpty(ns));
                                    }
                                }
                            }
                        }
                        using var tcpip6BaseKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters\Interfaces");
                        if (tcpip6BaseKey != null)
                        {
                            foreach (var guidKeyName in tcpip6BaseKey.GetSubKeyNames())
                            {
                                using var key = tcpip6BaseKey.OpenSubKey(guidKeyName);
                                if (key != null)
                                {
                                    string ns = key.GetValue("NameServer", null) as string;
                                    guidToIPv6DNSAuto.Add(guidKeyName, string.IsNullOrEmpty(ns));
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteLog($"读取注册表时遇到异常。", LogLevel.Error, ex);
                    }

                    string adapterQuery = "SELECT Name, NetConnectionID, Description, ServiceName, InterfaceIndex, MACAddress, Manufacturer, PhysicalAdapter, GUID, NetEnabled, NetConnectionStatus FROM Win32_NetworkAdapter" + wmiCondition;
                    using var adapterSearcher = new ManagementObjectSearcher(adapterQuery);
                    using var adapterCollection = adapterSearcher.Get();
                    foreach (ManagementObject adapter in adapterCollection.Cast<ManagementObject>())
                    {
                        try
                        {
                            uint interfaceIndex = (uint)(adapter["InterfaceIndex"] ?? 0u);
                            string guid = adapter["GUID"]?.ToString()?.ToUpperInvariant() ?? "";

                            if (interfaceIndexToConfig.TryGetValue(interfaceIndex, out var ipConfig))
                            {
                                string name = adapter["Name"]?.ToString() ?? "";
                                string friendlyName = adapter["NetConnectionID"]?.ToString() ?? "";
                                string description = ipConfig["Description"]?.ToString() ?? adapter["Description"]?.ToString() ?? "";
                                string serviceName = ipConfig["ServiceName"]?.ToString() ?? adapter["ServiceName"]?.ToString() ?? "";
                                string macAddress = ipConfig["MACAddress"]?.ToString() ?? adapter["MACAddress"]?.ToString() ?? ""; // Config MAC is often more reliable
                                string manufacturer = adapter["Manufacturer"]?.ToString() ?? "";
                                bool isPhysicalAdapter = (bool)(adapter["PhysicalAdapter"] ?? false);
                                bool isNetEnabled = (bool)(adapter["NetEnabled"] ?? false);
                                ushort netConnectionStatus = (ushort)(adapter["NetConnectionStatus"] ?? 0);
                                bool isIPv4DNSAuto = string.IsNullOrEmpty(guid) || !guidToIPv4DNSAuto.TryGetValue(guid, out bool v4Auto) || v4Auto; // Default true if GUID invalid or not found
                                bool isIPv6DNSAuto = string.IsNullOrEmpty(guid) || !guidToIPv6DNSAuto.TryGetValue(guid, out bool v6Auto) || v6Auto; // Default true

                                bool isIPEnabled = ipConfig["IPEnabled"] as bool? ?? false;
                                string[] allIPAddresses = ipConfig["IPAddress"] as string[] ?? [];
                                List<string> ipAddressV4List = [];
                                List<string> ipAddressV6List = [];
                                foreach (string ip in allIPAddresses)
                                {
                                    if (ip.Contains(':')) ipAddressV6List.Add(ip);
                                    else if (ip.Contains('.')) ipAddressV4List.Add(ip);
                                }

                                string[] allSubnets = ipConfig["IPSubnet"] as string[] ?? [];
                                List<string> ipSubnetV4List = [];
                                List<ushort> ipSubnetV6PrefixList = [];
                                int v4AddrCount = ipAddressV4List.Count;
                                for (int i = 0; i < allSubnets.Length; i++)
                                {
                                    if (i < v4AddrCount && allSubnets[i].Contains('.')) ipSubnetV4List.Add(allSubnets[i]);
                                    else if (ushort.TryParse(allSubnets[i], out ushort prefix)) ipSubnetV6PrefixList.Add(prefix);
                                }

                                string[] defaultIPGateway = ipConfig["DefaultIPGateway"] as string[] ?? [];
                                bool isDhcpEnabled = ipConfig["DHCPEnabled"] as bool? ?? false;
                                string dhcpServer = ipConfig["DHCPServer"]?.ToString() ?? "";

                                string dhcpLeaseObtainedRaw = ipConfig["DHCPLeaseObtained"]?.ToString();
                                string dhcpLeaseExpiresRaw = ipConfig["DHCPLeaseExpires"]?.ToString();
                                DateTime dhcpLeaseObtained = !string.IsNullOrEmpty(dhcpLeaseObtainedRaw) ? ManagementDateTimeConverter.ToDateTime(dhcpLeaseObtainedRaw) : DateTime.MinValue;
                                DateTime dhcpLeaseExpires = !string.IsNullOrEmpty(dhcpLeaseExpiresRaw) ? ManagementDateTimeConverter.ToDateTime(dhcpLeaseExpiresRaw) : DateTime.MinValue;

                                string[] dnsServers = ipConfig["DNSServerSearchOrder"] as string[] ?? [];
                                List<string> ipv4DnsServerList = [];
                                ipv4DnsServerList.AddRange(dnsServers.Where(dns => dns != null && !dns.Contains(':')));
                                string[] ipv6DnsServerArray = !string.IsNullOrEmpty(guid) && guidToIPv6DNSServer.TryGetValue(guid, out var dns6) ? dns6 : [];

                                adapters.Add(new NetworkAdapter(
                                    name, friendlyName, description, serviceName, interfaceIndex, macAddress, manufacturer,
                                    isPhysicalAdapter, guid, isNetEnabled, netConnectionStatus, isIPEnabled,
                                    [.. ipAddressV4List], [.. ipSubnetV4List], defaultIPGateway,
                                    isDhcpEnabled, dhcpServer, dhcpLeaseObtained, dhcpLeaseExpires,
                                    [.. ipv4DnsServerList], isIPv4DNSAuto, isIPv6DNSAuto,
                                    ipAddressV6List.Count > 0, // isIPv6Enabled based on addresses found
                                    [.. ipAddressV6List], [.. ipSubnetV6PrefixList], ipv6DnsServerArray
                                ));
                            }
                            else WriteLog($"未找到 InterfaceIndex 为 {interfaceIndex} 的适配器 {adapter["Name"]}，跳过详细的 IP 信息。", LogLevel.Debug);
                        }
                        catch (Exception ex)
                        {
                            WriteLog($"获取 InterfaceIndex 为 {adapter["InterfaceIndex"]} 的适配器 {adapter["Name"]} 信息时遇到异常。", LogLevel.Error, ex);
                        }
                    }
                }
                catch (ManagementException mgmtEx)
                {
                    WriteLog($"WMI 查询或操作失败。", LogLevel.Error, mgmtEx);
                }
                catch (Exception ex)
                {
                    WriteLog("获取网络适配器列表时遇到异常。", LogLevel.Error, ex);
                }
                return adapters;
            });
        }

        /// <summary>
        /// 刷新指定的网络适配器信息
        /// </summary>
        public static async Task<NetworkAdapter> RefreshAsync(NetworkAdapter networkAdapter)
        {
            if (networkAdapter == null || string.IsNullOrEmpty(networkAdapter.GUID)) return null;
            // Use the async internal method
            var adapters = await GetNetworkAdaptersInternalAsync($" WHERE GUID='{networkAdapter.GUID}'");
            return adapters.FirstOrDefault();
        }

        /// <summary>
        /// 设置指定网络适配器的 IPv4 DNS 服务器。
        /// </summary>
        public static void SetIPv4DNS(NetworkAdapter networkAdapter, string[] dnsServers)
        {
            string query = $"SELECT * FROM Win32_NetworkAdapterConfiguration WHERE InterfaceIndex = {networkAdapter.InterfaceIndex}";
            try
            {
                using ManagementObjectSearcher searcher = new(query);
                using ManagementObjectCollection adapterCollection = searcher.Get();
                ManagementObject adapter = adapterCollection.Cast<ManagementObject>().FirstOrDefault();
                if (adapter != null)
                {
                    var result = adapter.InvokeMethod("SetDNSServerSearchOrder", [dnsServers]);
                }
                else
                {
                    WriteLog($"未找到 InterfaceIndex 为 {networkAdapter.InterfaceIndex} 的适配器配置。", LogLevel.Warning);
                }
            }
            catch (Exception ex)
            {
                WriteLog($"设置 {networkAdapter.FriendlyName} 的 IPv4 DNS 服务器时遇到异常。", LogLevel.Error, ex);
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
            const uint IpV4AddrAny = 0;
            int result = GetBestInterface(IpV4AddrAny, out uint bestIfIndex);
            if (result == 0) return bestIfIndex;
            else
            {
                WriteLog($"无法使用获取默认路由接口索引，错误码：{result}。", LogLevel.Warning);
                return null;
            }
        }
    }
}
