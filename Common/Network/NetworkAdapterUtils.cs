using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using SNIBypassGUI.Common.System;
using SNIBypassGUI.Models;
using static SNIBypassGUI.Common.LogManager;
using SNIBypassGUI.Common.Interop;

namespace SNIBypassGUI.Common.Network
{
    /// <summary>
    /// Provides utility methods for managing network adapters and DNS configurations via WMI, Netsh, and Registry.
    /// Implements fallback mechanisms to ensure configuration application.
    /// </summary>
    public static class NetworkAdapterUtils
    {
        /// <summary>
        /// Defines the filtering scope when querying network adapters.
        /// </summary>
        public enum ScopeNeeded
        {
            All,
            EnabledOnly,
            ConnectedOnly,
            PhysicalOnly,
            FriendlyNameNotNullOnly
        }

        /// <summary>
        /// Asynchronously retrieves a list of network adapters matching the specified scope.
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
                _ => throw new ArgumentOutOfRangeException(nameof(scopeNeeded), scopeNeeded, "Invalid scope."),
            };
            return GetNetworkAdaptersInternalAsync(condition);
        }

        /// <summary>
        /// Refreshes the information of a specific network adapter.
        /// </summary>
        public static async Task<NetworkAdapter> RefreshAsync(NetworkAdapter networkAdapter)
        {
            if (networkAdapter == null) return null;
            var adapters = await GetNetworkAdaptersInternalAsync($" WHERE GUID='{networkAdapter.GUID:B}'"); // Use :B for braced GUID format
            return adapters.FirstOrDefault();
        }

        private static async Task<List<NetworkAdapter>> GetNetworkAdaptersInternalAsync(string wmiCondition)
        {
            return await Task.Run(() =>
            {
                var adapters = new List<NetworkAdapter>();

                // 1. Pre-fetch IPv6 DNS via NetworkInterface (WMI support for IPv6 is poor)
                var guidToIPv6DNSServer = new Dictionary<Guid, string[]>();
                foreach (var netInterface in NetworkInterface.GetAllNetworkInterfaces())
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(netInterface.Id))
                        {
                            var ipv6Dns = netInterface.GetIPProperties().DnsAddresses
                                .Where(ip => ip.AddressFamily == AddressFamily.InterNetworkV6)
                                .Select(ip => ip.ToString()).ToArray();

                            if (ipv6Dns.Length > 0)
                                guidToIPv6DNSServer[new Guid(netInterface.Id)] = ipv6Dns;
                        }
                    }
                    catch { /* Ignore access denied or other errors */ }
                }

                // 2. Check Registry for "Obtain DNS Automatically" status
                var guidToIPv4DNSAuto = new Dictionary<Guid, bool>();
                var guidToIPv6DNSAuto = new Dictionary<Guid, bool>();

                try
                {
                    // Check IPv4
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
                                    // If NameServer is empty, it's usually set to Auto
                                    if (Guid.TryParse(guidKeyName, out Guid guid))
                                        guidToIPv4DNSAuto[guid] = string.IsNullOrEmpty(ns);
                                }
                            }
                        }
                    }

                    // Check IPv6
                    using var tcpip6BaseKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters\Interfaces");
                    if (tcpip6BaseKey != null)
                    {
                        foreach (var guidKeyName in tcpip6BaseKey.GetSubKeyNames())
                        {
                            using var key = tcpip6BaseKey.OpenSubKey(guidKeyName);
                            if (key != null)
                            {
                                string ns = key.GetValue("NameServer", null) as string;
                                if (Guid.TryParse(guidKeyName, out Guid guid))
                                    guidToIPv6DNSAuto[guid] = string.IsNullOrEmpty(ns);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteLog("Exception occurred checking DNS Auto status from Registry.", LogLevel.Error, ex);
                }

                // 3. WMI Query for main adapter info
                string adapterQuery = "SELECT Name, NetConnectionID, Description, InterfaceIndex, GUID, NetEnabled, NetConnectionStatus FROM Win32_NetworkAdapter" + wmiCondition;

                using var adapterSearcher = new ManagementObjectSearcher(adapterQuery);
                using var adapterCollection = adapterSearcher.Get();

                foreach (ManagementObject adapter in adapterCollection.Cast<ManagementObject>())
                {
                    try
                    {
                        string guidString = adapter["GUID"]?.ToString();
                        if (string.IsNullOrEmpty(guidString) || !Guid.TryParse(guidString, out Guid guid)) continue;

                        uint interfaceIndex = (uint)(adapter["InterfaceIndex"] ?? 0u);

                        // Supplementary query for IPv4 DNS from WMI Configuration class
                        string configQuery = $"SELECT DNSServerSearchOrder FROM Win32_NetworkAdapterConfiguration WHERE InterfaceIndex = {interfaceIndex}";
                        using var configSearcher = new ManagementObjectSearcher(configQuery);
                        using var configColl = configSearcher.Get();
                        var config = configColl.Cast<ManagementObject>().FirstOrDefault();

                        string[] ipv4Dns = [];
                        if (config != null)
                        {
                            ipv4Dns = (config["DNSServerSearchOrder"] as string[])
                                      ?.Where(d => !d.Contains(':')).ToArray() ?? [];
                        }

                        // Retrieve IPv6 DNS from our pre-fetched dictionary
                        string[] ipv6Dns = guidToIPv6DNSServer.TryGetValue(guid, out var v6) ? v6 : [];

                        // Determine Auto status (Default to true if not found in registry map)
                        bool isIPv4Auto = !guidToIPv4DNSAuto.TryGetValue(guid, out bool v4Auto) || v4Auto;
                        bool isIPv6Auto = !guidToIPv6DNSAuto.TryGetValue(guid, out bool v6Auto) || v6Auto;

                        adapters.Add(new NetworkAdapter(
                            adapter["Name"]?.ToString() ?? "",
                            adapter["NetConnectionID"]?.ToString() ?? "",
                            adapter["Description"]?.ToString() ?? "",
                            interfaceIndex,
                            guid,
                            (bool)(adapter["NetEnabled"] ?? false),
                            (ushort)(adapter["NetConnectionStatus"] ?? 0),
                            ipv4Dns,
                            isIPv4Auto,
                            ipv6Dns,
                            isIPv6Auto
                        ));
                    }
                    catch (Exception ex)
                    {
                        WriteLog($"Error processing adapter {adapter["Name"]}.", LogLevel.Debug, ex);
                    }
                }
                return adapters;
            });
        }

        /// <summary>
        /// Sets the IPv4 DNS servers for the specified adapter.
        /// Automatically falls back to Registry injection if WMI fails (e.g. disconnected cable).
        /// </summary>
        public static bool SetIPv4DNS(NetworkAdapter networkAdapter, string[] dnsServers)
        {
            bool wmiSuccess = false;

            // 1. Try Standard WMI Method
            try
            {
                string query = $"SELECT * FROM Win32_NetworkAdapterConfiguration WHERE InterfaceIndex = {networkAdapter.InterfaceIndex}";
                using ManagementObjectSearcher searcher = new(query);
                using ManagementObjectCollection adapterCollection = searcher.Get();
                ManagementObject adapter = adapterCollection.Cast<ManagementObject>().FirstOrDefault();

                if (adapter != null)
                {
                    object resultObj = adapter.InvokeMethod("SetDNSServerSearchOrder", [dnsServers]);
                    uint result = resultObj == null ? uint.MaxValue : Convert.ToUInt32(resultObj);

                    // 0: Success, 1: Success (Reboot needed - treated as success here)
                    if (result == 0 || result == 1) wmiSuccess = true;
                    else WriteLog($"WMI failed to set IPv4 DNS, error code: {result}. Attempting Registry fallback...", LogLevel.Warning);
                }
                else WriteLog($"Adapter configuration not found for InterfaceIndex {networkAdapter.InterfaceIndex}. Attempting Registry fallback.", LogLevel.Warning);
            }
            catch (Exception ex)
            {
                WriteLog($"Exception setting IPv4 DNS via WMI: {ex.Message}. Attempting Registry fallback...", LogLevel.Warning, ex);
            }

            // 2. Fallback to Registry if WMI failed
            if (!wmiSuccess)
            {
                try
                {
                    string dnsString = (dnsServers != null && dnsServers.Length > 0) ? string.Join(",", dnsServers) : "";
                    string keyPath = $@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{networkAdapter.Id}";

                    using RegistryKey key = Registry.LocalMachine.OpenSubKey(keyPath, true);
                    if (key != null)
                    {
                        key.SetValue("NameServer", dnsString);
                        WriteLog($"Registry IPv4 fallback set successfully.", LogLevel.Info);
                        return true;
                    }
                    else
                    {
                        WriteLog($"Registry path not found: {keyPath}", LogLevel.Error);
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    WriteLog($"Registry IPv4 write failed.", LogLevel.Error, ex);
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Sets the IPv6 DNS servers for the specified adapter.
        /// Automatically falls back to Registry injection if Netsh fails.
        /// </summary>
        public static async Task<bool> SetIPv6DNS(NetworkAdapter networkAdapter, string[] dnsServers)
        {
            bool netshSuccess = false;

            // 1. Try Standard Netsh Method
            try
            {
                string friendlyName = networkAdapter.FriendlyName;
                if (!string.IsNullOrEmpty(friendlyName))
                {
                    if (dnsServers == null || dnsServers.Length == 0)
                    {
                        // Reset to DHCP
                        var (success, _, error) = await CmdUtils.RunCommand($"netsh interface ipv6 set dnsservers name=\"{friendlyName}\" source=dhcp");
                        if (success) netshSuccess = true;
                        else WriteLog($"Netsh failed to set IPv6 DHCP: {error}. Attempting Registry fallback...", LogLevel.Warning);
                    }
                    else
                    {
                        // Set Primary
                        var (successPri, _, errorPri) = await CmdUtils.RunCommand($"netsh interface ipv6 set dnsservers name=\"{friendlyName}\" static address={dnsServers[0]} primary");
                        if (successPri)
                        {
                            netshSuccess = true;
                            // Set Secondaries (Best effort, ignore errors here)
                            for (int i = 1; i < dnsServers.Length; i++) await CmdUtils.RunCommand($"netsh interface ipv6 add dnsservers name=\"{friendlyName}\" address={dnsServers[i]} index={i + 1}");
                        }
                        else WriteLog($"Netsh failed to set IPv6 Primary DNS: {errorPri}. Attempting Registry fallback...", LogLevel.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLog($"Exception setting IPv6 DNS via Netsh. Attempting Registry fallback...", LogLevel.Warning, ex);
            }

            // 2. Fallback to Registry if Netsh failed
            if (!netshSuccess)
            {
                try
                {
                    string dnsString = (dnsServers != null && dnsServers.Length > 0) ? string.Join(",", dnsServers) : "";
                    string keyPath = $@"SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters\Interfaces\{networkAdapter.Id}";

                    using RegistryKey key = Registry.LocalMachine.OpenSubKey(keyPath, true);
                    if (key != null)
                    {
                        key.SetValue("NameServer", dnsString);
                        WriteLog($"Registry IPv6 fallback set successfully.", LogLevel.Info);
                        return true;
                    }
                    else
                    {
                        WriteLog($"Registry path not found: {keyPath}", LogLevel.Error);
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    WriteLog($"Registry IPv6 write failed.", LogLevel.Error, ex);
                    return false;
                }
            }

            NetworkUtils.FlushDNS();
            return true;
        }

        /// <summary>
        /// Retrieves the interface index of the default IPv4 route.
        /// </summary>
        public static uint? GetDefaultRouteInterfaceIndex()
        {
            const uint IpV4AddrAny = 0;
            int result = Iphlpapi.GetBestInterface(IpV4AddrAny, out uint bestIfIndex);

            if (result == 0) return bestIfIndex;
            else
            {
                WriteLog($"Failed to get default route interface index. Error code: {result}", LogLevel.Warning);
                return null;
            }
        }
    }
}
