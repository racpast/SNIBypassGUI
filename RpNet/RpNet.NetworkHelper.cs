using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Management;
using System.Runtime.InteropServices;
using static SNIBypassGUI.PathsSet;
using static SNIBypassGUI.LogHelper;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Management.Automation;
using System.Net;

namespace RpNet.NetworkHelper
{
    /// <summary>
    /// 表示一个网络适配器，提供获取和设置网络适配器的配置信息，包括IP地址、子网掩码、默认网关、DNS服务器等。 
    /// 支持启用和禁用IPv4与IPv6协议，配置DHCP，获取网络适配器的信息报告，以及与网络适配器相关的其他操作。
    /// 通过WMI (Windows Management Instrumentation) 实现与系统的交互，支持网络适配器的动态配置和管理。
    /// 信息的获取基于 Win32_NetworkAdapterConfiguration 类与 Win32_NetworkAdapter 类实现，通过共有的 InterfaceIndex 属性来连接，此属性中的值与表示路由表中网络接口的 Win32_IP4RouteTable 实例中的 InterfaceIndex 属性中的值相同。
    /// 但 Win32_IP4RouteTable 类只适用于IPv4，不返回IPX或IPv6数据。因此应该避免对仅IPv6协议启用的适配器进行操作。本类仅提供部分通过 Powershell 实现的IPv6协议支持。
    /// </summary>
    public class NetworkAdapter
    {
        /// <summary>
        /// 网络适配器的硬件名称，从 Win32_NetworkAdapter 类获取。
        /// </summary>
        public string Name { get { return name; } }
        /// <summary>
        /// 网络适配器在控制面板中所显示的接口名称，从 Win32_NetworkAdapter 类获取。
        /// </summary>
        public string Caption { get { return caption; } }
        /// <summary>
        /// 网络适配器的描述，可以从 Win32_NetworkAdapter 或 Win32_NetworkAdapterConfiguration 类获取。
        /// </summary>
        public string Description { get { return description; } }
        /// <summary>
        /// 网络适配器服务名称，可以从 Win32_NetworkAdapter 或 Win32_NetworkAdapterConfiguration 类获取。
        /// </summary>
        public string ServiceName { get { return serviceName; } }
        /// <summary>
        /// DNS服务器地址。从 Win32_NetworkAdapterConfiguration 类获取。可设置为一个字符串数组，但其中不应包含IPv6地址。设置为null时将重置DNS服务器地址。
        /// </summary>
        public string[] DNS
        {
            get { return dNS == null ? [] : dNS; }
            set
            {
                ManagementClass wmi = new ManagementClass("Win32_NetworkAdapterConfiguration");
                ManagementObjectCollection moc = wmi.GetInstances();
                ManagementBaseObject o = null;
                foreach (ManagementObject mo in moc)
                {
                    if ((uint)mo["InterfaceIndex"] == InterfaceIndex)
                    {
                        if (value == null)
                        {
                            uint t = (uint)mo.InvokeMethod("SetDNSServerSearchOrder", null);
                            if (t != 0)
                                throw new NetworkAdapterSetException((uint)o["returnValue"]);
                            break;
                        }
                        else
                        {
                            ManagementBaseObject i = mo.GetMethodParameters("SetDNSServerSearchOrder");
                            i["DNSServerSearchOrder"] = value;
                            o = mo.InvokeMethod("SetDNSServerSearchOrder", i, null);
                            if ((uint)o["returnValue"] != 0)
                                throw new NetworkAdapterSetException((uint)o["returnValue"]);
                            break;
                        }
                    }
                }
            }
        }
        /// <summary>
        /// 网络接口唯一标识索引。从 Win32_NetworkAdapter 或 Win32_NetworkAdapterConfiguration 类获取。
        /// </summary>
        public uint InterfaceIndex { get { return interfaceIndex; } }
        /// <summary>
        /// 默认网关地址。从 Win32_NetworkAdapterConfiguration 类获取。可设置为一个字符串数组，但其中不应包含IPv6地址。设置为null时将启用DHCP。也可以通过 SetStatic 方法设置。
        /// </summary>
        public string[] Gateway
        {
            get { return gateway; }
            set
            {
                ManagementClass wmi = new ManagementClass("Win32_NetworkAdapterConfiguration");
                ManagementObjectCollection moc = wmi.GetInstances();
                ManagementBaseObject i = null;
                ManagementBaseObject o = null;
                foreach (ManagementObject mo in moc)
                {
                    if ((uint)mo["InterfaceIndex"] == InterfaceIndex)
                    {
                        if (value == null)
                        {
                            i = mo.GetMethodParameters("EnableDHCP");
                            o = mo.InvokeMethod("EnableDHCP", i, null);
                            if ((uint)o["returnValue"] != 0)
                                throw new NetworkAdapterSetException((uint)o["returnValue"]);
                            break;
                        }
                        else
                        {
                            i = mo.GetMethodParameters("SetGateways");
                            i["DefaultIPGateway"] = value;
                            o = mo.InvokeMethod("SetGateways", i, null);
                            if ((uint)o["returnValue"] != 0)
                                throw new NetworkAdapterSetException((uint)o["returnValue"]);
                            break;
                        }
                    }
                }
            }
        }
        /// <summary>
        /// IP地址。从 Win32_NetworkAdapterConfiguration 类获取。欲设置该值，请使用 SetStatic 方法。
        /// </summary>
        public string[] IP { get { return ip; } }
        /// <summary>
        /// 子网掩码。从 Win32_NetworkAdapterConfiguration 类获取。欲设置该值，请使用 SetStatic 方法。
        /// </summary>
        public string[] Mask { get { return mask; } }
        /// <summary>
        /// GUID。从 Win32_NetworkAdapter 类获取。
        /// </summary>
        public string GUID { get { return gUID; } }
        /// <summary>
        /// 是否启用IP。从 Win32_NetworkAdapterConfiguration 类获取。
        /// </summary>
        public bool IPEnabled { get { return iPEnabled; } }
        /// <summary>
        /// 网络适配器连接到网络的状态。从 Win32_NetworkAdapter 类获取。详情见：https://learn.microsoft.com/zh-cn/windows/win32/cimwin32prov/win32-networkadapter
        /// </summary>
        public ushort? NetConnectionStatus { get { return netConnectionStatus; } }
        /// <summary>
        /// 指示适配器是否已启用（经测试，该值未必准确）。从 Win32_NetworkAdapter 类获取。
        /// </summary>
        public bool? NetEnabled { get { return netEnabled; } }
        /// <summary>
        /// DHCP是否启用。从 Win32_NetworkAdapterConfiguration 类获取。
        /// </summary>
        public bool? DHCPEnabled { get { return dHCPEnabled; } }
        /// <summary>
        /// DHCP服务器地址。从 Win32_NetworkAdapterConfiguration 类获取。
        /// </summary>
        public string DHCPServer { get { return dHCPServer; } }
        /// <summary>
        /// 网络适配器的MAC地址。从 Win32_NetworkAdapterConfiguration 类获取。
        /// </summary>
        public string MACAddress { get { return mACAddress; } }
        /// <summary>
        /// 网络适配器的制造商的名称（经测试，该值未必准确）。从 Win32_NetworkAdapter 类获取。
        /// </summary>
        public string Manufacturer { get { return manufacturer; } }
        /// <summary>
        /// 指示适配器是物理适配器还是逻辑适配器。从 Win32_NetworkAdapter 类获取。
        /// </summary>
        public bool IsPhysicalAdapter { get { return isPhysicalAdapter; } }
        /// <summary>
        /// 指示网络适配器是否为自动获取DNS服务器。从注册表判断。
        /// </summary>
        public bool? IsDnsAutomatic { get { return isDnsAutomatic; } }

        private string caption;
        private string name;
        private string description;
        private string serviceName;
        private string[] dNS;
        private uint interfaceIndex;
        private string[] gateway;
        private string[] ip;
        private string[] mask;
        private string gUID;
        private bool iPEnabled;
        private ushort? netConnectionStatus;
        private bool? netEnabled;
        private string mACAddress;
        private string manufacturer;
        private bool isPhysicalAdapter;
        private bool? dHCPEnabled;
        private string dHCPServer;
        private bool? isDnsAutomatic;

        /// <summary>
        /// 需要查找的适配器范围
        /// </summary>
        public enum ScopeNeeded
        {
            All,
            EnabledOnly,
            ConnectedOnly,
            PhysicalOnly,
            NetConnectIDNotNullOnly
        }

        /// <summary>
        /// 设置当前网络适配器的静态IP配置。
        /// </summary>
        /// <param name="ip">IP地址数组。</param>
        /// <param name="submask">子网掩码数组。</param>
        /// <param name="gateway">默认网关地址数组。</param>
        /// <param name="dns">DNS服务器地址数组。</param>
        public void SetStatic(string[] ip, string[] submask, string[] gateway, string[] dns)
        {
            SetStatic(this, ip, submask, gateway, dns);
        }

        /// <summary>
        /// 设置网络适配器的静态IP配置。
        /// </summary>
        /// <param name="adapter">指定的网络适配器。</param>
        /// <param name="ip">IP地址数组。</param>
        /// <param name="submask">子网掩码数组。</param>
        /// <param name="gateway">默认网关地址数组。</param>
        /// <param name="dns">DNS服务器地址数组。</param>
        public static void SetStatic(NetworkAdapter adapter, string[] ip, string[] submask, string[] gateway, string[] dns)
        {
            ManagementClass wmi = new ManagementClass("Win32_NetworkAdapterConfiguration");
            ManagementObjectCollection moc = wmi.GetInstances();
            ManagementBaseObject inPar;
            ManagementBaseObject outPar;
            foreach (ManagementObject mo in moc)
            {
                if ((uint)mo["InterfaceIndex"] == adapter.interfaceIndex)
                {
                    if (!(bool)mo["IPEnabled"])
                        continue;

                    if (ip != null && submask != null)
                    {
                        inPar = mo.GetMethodParameters("EnableStatic");
                        inPar["IPAddress"] = ip;
                        inPar["SubnetMask"] = submask;
                        outPar = mo.InvokeMethod("EnableStatic", inPar, null);

                        uint returnValue = (uint)outPar["returnValue"];
                        if (returnValue != 0)
                        {
                            throw new NetworkAdapterSetException(returnValue);
                        }
                    }

                    if (gateway != null)
                    {
                        inPar = mo.GetMethodParameters("SetGateways");
                        inPar["DefaultIPGateway"] = gateway;
                        outPar = mo.InvokeMethod("SetGateways", inPar, null);

                        uint returnValue = (uint)outPar["returnValue"];
                        if (returnValue != 0)
                        {
                            throw new NetworkAdapterSetException(returnValue);
                        }
                    }

                    if (dns != null)
                    {
                        inPar = mo.GetMethodParameters("SetDNSServerSearchOrder");
                        inPar["DNSServerSearchOrder"] = dns;
                        outPar = mo.InvokeMethod("SetDNSServerSearchOrder", inPar, null);

                        uint returnValue = (uint)outPar["returnValue"];
                        if (returnValue != 0)
                        {
                            throw new NetworkAdapterSetException(returnValue);
                        }
                    }
                    else
                    {
                        inPar = mo.GetMethodParameters("SetDNSServerSearchOrder");
                        inPar["DNSServerSearchOrder"] = new string[0];
                        outPar = mo.InvokeMethod("SetDNSServerSearchOrder", inPar, null);

                        uint returnValue = (uint)outPar["returnValue"];
                        if (returnValue != 0)
                        {
                            throw new NetworkAdapterSetException(returnValue);
                        }
                    }
                }
                break;
            }
        }

        /// <summary>
        /// 获取符合指定范围条件的所有网络适配器。
        /// </summary>
        /// <param name="scope">查询范围，默认为全部。</param>
        /// <returns>网络适配器列表。</returns>
        public static List<NetworkAdapter> GetNetworkAdapters(ScopeNeeded scope = ScopeNeeded.All)
        {
            /*
            //使用 C# 8.0 中的“递归模式”：https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-version-history#c-version-80
            string query = scope switch
            {
                ScopeNeeded.All => "SELECT * FROM Win32_NetworkAdapter",
                ScopeNeeded.EnabledOnly => "SELECT * FROM Win32_NetworkAdapter WHERE NetEnabled=True",
                ScopeNeeded.ConnectedOnly => "SELECT * FROM Win32_NetworkAdapter WHERE NetConnectionStatus=2",
                ScopeNeeded.PhysicalOnly => "SELECT * FROM Win32_NetworkAdapter WHERE PhysicalAdapter=True",
                ScopeNeeded.NetConnectIDNotNullOnly => "SELECT * FROM Win32_NetworkAdapter WHERE (NetConnectionID IS NOT NULL)",
                _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null)
            };
            */

            string query = null;
            switch (scope)
            {
                case ScopeNeeded.All:
                    query = "SELECT * FROM Win32_NetworkAdapter";
                    break;
                case ScopeNeeded.EnabledOnly:
                    query = "SELECT * FROM Win32_NetworkAdapter WHERE NetEnabled=True";
                    break;
                case ScopeNeeded.ConnectedOnly:
                    query = "SELECT * FROM Win32_NetworkAdapter WHERE NetConnectionStatus=2";
                    break;
                case ScopeNeeded.PhysicalOnly:
                    query = "SELECT * FROM Win32_NetworkAdapter WHERE PhysicalAdapter=True";
                    break;
                case ScopeNeeded.NetConnectIDNotNullOnly:
                    query = "SELECT * FROM Win32_NetworkAdapter WHERE (NetConnectionID IS NOT NULL)";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(scope), scope, "无效的查询范围。");
            }

            Dictionary<uint, NetworkAdapter> InterfaceIndexToNetworkAdapter = new Dictionary<uint, NetworkAdapter>();
            ObjectQuery oquery = new ObjectQuery(query);
            ManagementObjectSearcher searcher = new ManagementObjectSearcher(oquery);
            ManagementObjectCollection queryCollection = searcher.Get();
            foreach (ManagementObject m in queryCollection)
            {
                NetworkAdapter networkAdapter = new NetworkAdapter();
                networkAdapter.interfaceIndex = (uint)m["InterfaceIndex"];
                networkAdapter.serviceName = (string)m["ServiceName"];
                networkAdapter.gUID = (string)m["GUID"];
                networkAdapter.caption = (string)m["NetConnectionID"];
                networkAdapter.description = (string)m["Description"];
                networkAdapter.name = (string)m["Name"];
                networkAdapter.mACAddress = (string)m["MACAddress"];
                networkAdapter.manufacturer = (string)m["Manufacturer"];
                networkAdapter.netEnabled = (bool?)m["NetEnabled"];
                networkAdapter.isPhysicalAdapter = (bool)m["PhysicalAdapter"];
                networkAdapter.netConnectionStatus = (ushort?)m["NetConnectionStatus"];
                if (!string.IsNullOrEmpty(networkAdapter.gUID))
                {
                    string path = "HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters\\Interfaces\\" + (string)m["GUID"];
                    string ns = (string)Registry.GetValue(path, "NameServer", null);
                    networkAdapter.isDnsAutomatic = string.IsNullOrEmpty(ns);
                }
                else
                {
                    networkAdapter.isDnsAutomatic = null;
                }
                InterfaceIndexToNetworkAdapter.Add(networkAdapter.interfaceIndex, networkAdapter);
            }

            oquery = new ObjectQuery("SELECT * FROM Win32_NetworkAdapterConfiguration");
            searcher = new ManagementObjectSearcher(oquery);
            queryCollection = searcher.Get();
            foreach (ManagementObject m in queryCollection)
            {
                uint interfaceIndex = (uint)m["InterfaceIndex"];
                if (InterfaceIndexToNetworkAdapter.ContainsKey(interfaceIndex))
                {
                    var networkAdapter = InterfaceIndexToNetworkAdapter[interfaceIndex];
                    networkAdapter.dNS = (string[])m["DNSServerSearchOrder"];
                    networkAdapter.gateway = (string[])m["DefaultIPGateway"];
                    networkAdapter.ip = (string[])m["IPAddress"];
                    networkAdapter.mask = (string[])m["IPSubnet"];
                    networkAdapter.iPEnabled = (bool)m["IPEnabled"];
                    networkAdapter.dHCPEnabled = (bool?)m["DHCPEnabled"];
                    networkAdapter.dHCPServer = (string)m["DHCPServer"];
                }
            }

            return new List<NetworkAdapter>(InterfaceIndexToNetworkAdapter.Values);
        }

        /// <summary>
        /// 获取当前网络适配器的配置信息。
        /// </summary>
        /// <returns>网络适配器配置信息数组。</returns>
        public string[] NetworkAdaptersInfo()
        {
            return NetworkAdaptersInfo(this);
        }

        /// <summary>
        /// 获取指定网络适配器的配置信息。
        /// </summary>
        /// <param name="adapter">指定的网络适配器。</param>
        /// <returns>网络适配器配置信息数组。</returns>
        public static string[] NetworkAdaptersInfo(NetworkAdapter adapter)
        {
            List<string> report = new List<string>();
            if (adapter != null)
            {
                report.Add("==========START==========");
                report.Add($"适配器名称：{adapter.Name}");
                report.Add($"网络连接名称：{adapter.Caption}");
                report.Add($"描述：{adapter.Description}");
                report.Add($"制造商：{adapter.Name}");
                report.Add($"服务名称：{adapter.ServiceName}");
                AddIfNotNullOrEmpty(report, "网络连接状态", GetNetConnectionStatusText(adapter.NetConnectionStatus));
                AddIfNotNullOrEmpty(report, "物理适配器", BoolToYesNo(adapter.IsPhysicalAdapter));
                AddIfNotNullOrEmpty(report, "适配器启用", BoolToYesNo(adapter.NetEnabled));
                AddIfNotNullOrEmpty(report, "IP地址启用", BoolToYesNo(adapter.IPEnabled));
                AddIfNotNullOrEmpty(report, "IP地址", adapter.IP);
                AddIfNotNullOrEmpty(report, "DHCP启用", BoolToYesNo(adapter.DHCPEnabled));
                AddIfNotNullOrEmpty(report, "DHCP服务器IP", adapter.DHCPServer);
                AddIfNotNullOrEmpty(report, "自动获取DNS", BoolToYesNo(adapter.IsDnsAutomatic));
                AddIfNotNullOrEmpty(report, "DNS服务器", adapter.DNS);
                AddIfNotNullOrEmpty(report, "默认网关", adapter.Gateway);
                AddIfNotNullOrEmpty(report, "MAC地址", adapter.MACAddress);
                AddIfNotNullOrEmpty(report, "GUID", adapter.GUID);
                report.Add("==========END==========");
            }
            return report.ToArray();
        }

        /// <summary>
        /// 将布尔值转换为"是"或"否"的字符串。
        /// </summary>
        /// <param name="value">布尔值。</param>
        /// <returns>"是"、"否"或null。</returns>
        static string BoolToYesNo(bool? value)
        {
            if (value != null)
            {
                if (value == true)
                {
                    return "是";
                }
                else
                {
                    return "否";
                }
            }
            return null;
        }

        /// <summary>
        /// 获取网络连接状态的文本描述。
        /// </summary>
        /// <param name="status">连接状态值。</param>
        /// <returns>连接状态的文本描述。</returns>
        static string GetNetConnectionStatusText(ushort? status)
        {
            if (status != null)
            {
                switch (status)
                {
                    case 0:
                        return "连接断开";
                    case 1:
                        return "连接中";
                    case 2:
                        return "已连接";
                    case 3:
                        return "断开连接中";
                    case 4:
                        return "硬件不存在";
                    case 5:
                        return "硬件禁用";
                    case 6:
                        return "硬件故障";
                    case 7:
                        return "媒体断开";
                    case 8:
                        return "验证中";
                    case 9:
                        return "验证成功";
                    case 10:
                        return "验证失败";
                    case 11:
                        return "无效地址";
                    case 12:
                        return "需要证书";
                    default:
                        return "其他";
                }
            }
            return null;
        }

        /// <summary>
        /// 启用当前网络适配器的IPv4协议。
        /// </summary>
        /// <returns>异步任务。</returns>
        public async Task EnableIPv4()
        {
            await EnableIPv4(this.caption);
        }

        /// <summary>
        /// 启用指定网络适配器的IPv4协议。
        /// </summary>
        /// <param name="caption">网络适配器的名称。</param>
        /// <returns>异步任务。</returns>
        public static async Task EnableIPv4(string caption)
        {
            if (caption != null)
            {
                await RunPowerShell($"Enable-NetAdapterBinding -Name '{caption}' -ComponentID ms_tcpip");
            }
        }

        /// <summary>
        /// 启用当前网络适配器的IPv6协议。
        /// </summary>
        /// <returns>异步任务。</returns>
        public async Task EnableIPv6()
        {
            await EnableIPv6(this.caption);
        }

        /// <summary>
        /// 启用指定网络适配器的IPv6协议。
        /// </summary>
        /// <param name="caption">网络适配器的名称。</param>
        /// <returns>异步任务。</returns>
        public static async Task EnableIPv6(string caption)
        {
            if (caption != null)
            {
                await RunPowerShell($"Enable-NetAdapterBinding -Name '{caption}' -ComponentID ms_tcpip6");
            }
        }

        /// <summary>
        /// 禁用当前网络适配器的IPv4协议。
        /// </summary>
        /// <returns>异步任务。</returns>
        public async Task DisableIPv4()
        {
            await DisableIPv4(this.caption);
        }

        /// <summary>
        /// 禁用指定网络适配器的IPv4协议。
        /// </summary>
        /// <param name="caption">网络适配器的名称。</param>
        /// <returns>异步任务。</returns>
        public static async Task DisableIPv4(string caption)
        {
            if (caption != null)
            {
                await RunPowerShell($"Disable-NetAdapterBinding -Name '{caption}' -ComponentID ms_tcpip");
            }
        }

        /// <summary>
        /// 禁用当前网络适配器的IPv6协议。
        /// </summary>
        /// <returns>异步任务。</returns>
        public async Task DisableIPv6()
        {
            await DisableIPv6(this.caption);
        }

        /// <summary>
        /// 禁用指定网络适配器的IPv6协议。
        /// </summary>
        /// <param name="caption">网络适配器的名称。</param>
        /// <returns>异步任务。</returns>
        public static async Task DisableIPv6(string caption)
        {
            if (caption != null)
            {
                await RunPowerShell($"Disable-NetAdapterBinding -Name '{caption}' -ComponentID ms_tcpip6");
            }
        }

        /// <summary>
        /// 设置当前网络适配器的DNS服务器地址，此方法支持同时包含IPv4地址与IPv6地址的数组。当设置为null则重置DNS服务器地址。通过Powershell实现，不应作为首选方案。
        /// </summary>
        /// <param name="dns">DNS服务器地址数组，默认为null。</param>
        /// <returns>异步任务。</returns>
        public async Task SetDNSServer(string[] dns = null)
        {
            await SetDNSServer(this.caption, dns);
        }

        /// <summary>
        /// 设置指定网络适配器的DNS服务器地址，此方法支持同时包含IPv4地址与IPv6地址的数组。当设置为null则重置DNS服务器地址。通过Powershell实现，不应作为首选方案。
        /// </summary>
        /// <param name="caption">网络适配器的名称。</param>
        /// <param name="dns">DNS服务器地址数组，默认为null。</param>
        /// <returns>异步任务。</returns>
        public static async Task SetDNSServer(string caption, string[] dns = null)
        {
            if (!string.IsNullOrEmpty(caption))
            {
                await RunPowerShell($"Set-DnsClientServerAddress -InterfaceAlias \"{caption}\" -ResetServerAddresses");
                if (dns?.Length > 0)
                {
                    string input = FormatStringArray(dns);
                    await RunPowerShell($"Set-DnsClientServerAddress -InterfaceAlias \"{caption}\" -ServerAddresses {input}");
                }
            }
        }

        /// <summary>
        /// 刷新当前网络适配器的信息。
        /// </summary>
        public void Fresh()
        {
            Fresh(this);
        }

        /// <summary>
        /// 刷新指定网络适配器的信息。
        /// </summary>
        /// <param name="adapter">指定的网络适配器。</param>
        /// <returns>刷新后的网络适配器。</returns>
        public static NetworkAdapter Fresh(NetworkAdapter adapter)
        {
            ObjectQuery oquery = new ObjectQuery("SELECT * FROM Win32_NetworkAdapter WHERE InterfaceIndex=" + adapter.interfaceIndex);
            ManagementObjectSearcher searcher = new ManagementObjectSearcher(oquery);
            ManagementObjectCollection queryCollection = searcher.Get();
            foreach (ManagementObject m in queryCollection)
            {
                adapter.interfaceIndex = (uint)m["InterfaceIndex"];
                adapter.serviceName = (string)m["ServiceName"];
                adapter.gUID = (string)m["GUID"];
                adapter.caption = (string)m["NetConnectionID"];
                adapter.description = (string)m["Description"];
                adapter.name = (string)m["Name"];
                adapter.mACAddress = (string)m["MACAddress"];
                adapter.manufacturer = (string)m["Manufacturer"];
                adapter.netEnabled = (bool?)m["NetEnabled"];
                adapter.isPhysicalAdapter = (bool)m["PhysicalAdapter"];
                adapter.netConnectionStatus = (ushort?)m["NetConnectionStatus"];
                if (!string.IsNullOrEmpty(adapter.gUID))
                {
                    string path = "HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters\\Interfaces\\" + (string)m["GUID"];
                    string ns = (string)Registry.GetValue(path, "NameServer", null);
                    adapter.isDnsAutomatic = string.IsNullOrEmpty(ns);
                }
                else
                {
                    adapter.isDnsAutomatic = null;
                }
            }

            oquery = new ObjectQuery("SELECT * FROM Win32_NetworkAdapterConfiguration WHERE InterfaceIndex=" + adapter.interfaceIndex);
            searcher = new ManagementObjectSearcher(oquery);
            queryCollection = searcher.Get();
            foreach (ManagementObject m in queryCollection)
            {
                adapter.dNS = (string[])m["DNSServerSearchOrder"];
                adapter.gateway = (string[])m["DefaultIPGateway"];
                adapter.ip = (string[])m["IPAddress"];
                adapter.mask = (string[])m["IPSubnet"];
                adapter.iPEnabled = (bool)m["IPEnabled"];
                adapter.dHCPEnabled = (bool?)m["DHCPEnabled"];
                adapter.dHCPServer = (string)m["DHCPServer"];
            }
            return adapter;
        }

        /// <summary>
        /// 执行指定的PowerShell命令。
        /// </summary>
        /// <param name="command">PowerShell命令。</param>
        /// <returns>异步任务。</returns>
        static async Task RunPowerShell(string command)
        {
            try
            {
                await Task.Run(() =>
                {
                    using (var powerShell = PowerShell.Create())
                    {
                        powerShell.AddScript(command);
                        var result = powerShell.Invoke();
                        if (powerShell.HadErrors)
                        {
                            var errorMessages = powerShell.Streams.Error.Select(e => e.ToString()).ToList();
                            throw new InvalidOperationException($"PowerShell执行失败：{string.Join(Environment.NewLine, errorMessages)}");
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// 格式化字符串数组为PowerShell所需的字符串格式。
        /// </summary>
        /// <param name="inputArray">输入的字符串数组。</param>
        /// <returns>格式化后的字符串。</returns>
        static string FormatStringArray(string[] inputArray)
        {
            if (inputArray == null) return null;
            return "(" + string.Join(",", Array.ConvertAll(inputArray, s => "\"" + s + "\"")) + ")";
        }

        /// <summary>
        /// 如果值不为空或null，添加到报告列表中。
        /// </summary>
        /// <typeparam name="T">值的类型。</typeparam>
        /// <param name="report">报告列表。</param>
        /// <param name="label">标签。</param>
        /// <param name="value">值。</param>
        static void AddIfNotNullOrEmpty<T>(List<string> report, string label, T value)
        {
            if (value is string str && !string.IsNullOrEmpty(str))
            {
                report.Add($"{label}：{str}");
            }
            else if (value is IEnumerable<string> enumerable && enumerable.Any())
            {
                report.Add($"{label}：");
                foreach (var item in enumerable)
                {
                    report.Add($"  {item}");
                }
            }
            else if (value != null)
            {
                report.Add($"{label}：{value}");
            }
        }

        /// <summary>
        /// 返回网络适配器的硬件名称。
        /// </summary>
        /// <returns>网络适配器的名称。</returns>
        public override string ToString()
        {
            return name;
        }
    }

    /// <summary>
    /// 自定义异常类，用于处理网络适配器设置时发生的错误。
    /// 提供一个错误码和描述错误源的详细信息，能够帮助诊断和调试与配置网络适配器相关的问题。
    /// </summary>
    public class NetworkAdapterSetException : Exception
    {
        private uint code;
        /// <summary>
        /// 获取错误码。
        /// </summary>
        public uint Code { get { return code; } }
        /// <summary>
        /// 获取或设置异常来源的详细描述信息。
        /// </summary>
        new public string Source { get; protected set; }
        /// <summary>
        /// 使用指定的错误码初始化网络适配器设置异常。
        /// 根据不同的错误码，生成相应的错误描述。
        /// </summary>
        /// <param name="c">错误码。</param>
        public NetworkAdapterSetException(uint c) : base("设置时发生错误！错误码：" + c.ToString())
        {
            code = c;
            switch (code)
            {
                case 1:
                    Source = "成功完成，需要重新启动。";
                    break;
                case 64:
                    Source = "此平台不支持方法。";
                    break;
                case 65:
                    Source = "未知错误。";
                    break;
                case 66:
                    Source = "子网掩码无效。";
                    break;
                case 67:
                    Source = "处理返回的实例时出错。";
                    break;
                case 68:
                    Source = "输入参数无效。";
                    break;
                case 69:
                    Source = "指定的网关超过 5 个。";
                    break;
                case 70:
                    Source = "IP 地址无效。";
                    break;
                case 71:
                    Source = "网关 IP 地址无效。";
                    break;
                case 72:
                    Source = "访问注册表以获取请求的信息时出错。";
                    break;
                case 73:
                    Source = "无效的域名。";
                    break;
                case 74:
                    Source = "主机名无效。";
                    break;
                case 75:
                    Source = "未定义主 WINS 服务器或辅助 WINS 服务器。";
                    break;
                case 76:
                    Source = "文件无效。";
                    break;
                case 77:
                    Source = "系统路径无效。";
                    break;
                case 78:
                    Source = "文件复制失败。";
                    break;
                case 79:
                    Source = "无效的安全参数。";
                    break;
                case 80:
                    Source = "无法配置 TCP/IP 服务。";
                    break;
                case 81:
                    Source = "无法配置 DHCP 服务。 有关详细信息，请参见“备注”部分。";
                    break;
                case 82:
                    Source = "无法续订 DHCP 租约。";
                    break;
                case 83:
                    Source = "无法释放 DHCP 租约。";
                    break;
                case 84:
                    Source = "适配器上未启用 IP。";
                    break;
                case 85:
                    Source = "适配器上未启用 IPX。";
                    break;
                case 86:
                    Source = "帧或网络编号边界错误。";
                    break;
                case 87:
                    Source = "无效的帧类型。";
                    break;
                case 88:
                    Source = "网络号码无效。";
                    break;
                case 89:
                    Source = "重复的网络号码。";
                    break;
                case 90:
                    Source = "参数超出边界。";
                    break;
                case 91:
                    Source = "访问被拒绝。（需要管理员权限？）";
                    break;
                case 92:
                    Source = "内存不足。";
                    break;
                case 93:
                    Source = "已存在。";
                    break;
                case 94:
                    Source = "找不到路径、文件或对象。";
                    break;
                case 95:
                    Source = "无法通知服务。";
                    break;
                case 96:
                    Source = "无法通知 DNS 服务。";
                    break;
                case 97:
                    Source = "接口不可配置。";
                    break;
                case 98:
                    Source = "并非所有 DHCP 租约都可以释放或续订。";
                    break;
                case 100:
                    Source = "适配器上未启用 DHCP。";
                    break;
                case 2147786788:
                    Source = "未启用写入锁定。 有关详细信息，请参阅 INetCfgLock：：AcquireWriteLock。";
                    break;
                default:
                    Source = "未知错误。";
                    break;
            }
        }
    }

    public class DNS
    {
        [DllImport("dnsapi.dll", EntryPoint = "DnsFlushResolverCache")]
        private static extern UInt32 DnsFlushResolverCache();

        public static void FlushDNS()
        {
            WriteLog("进入FlushDNS。", LogLevel.Debug);

            UInt32 result = DnsFlushResolverCache();

            WriteLog("完成FlushDNS。", LogLevel.Debug);
        }

        public static string GetIpAddressFromDomain(string domainName)
        {
            WriteLog("进入GetIpAddressFromDomain。", LogLevel.Debug);

            try
            {
                // 获取域名解析到的所有IP地址
                IPAddress[] ipAddresses = Dns.GetHostAddresses(domainName);

                // 如果解析结果不为空，返回第一个IP地址
                if (ipAddresses.Length > 0)
                {
                    WriteLog("完成GetIpAddressFromDomain。", LogLevel.Debug);

                    return ipAddresses[0].ToString();
                }
                else
                {
                    WriteLog("完成GetIpAddressFromDomain，未找到对应的IP地址。", LogLevel.Debug);

                    return "";
                }
            }
            catch (Exception ex)
            {
                WriteLog($"遇到异常：{ex}。", LogLevel.Error, ex);

                // 捕获异常并返回错误信息
                return $"错误: {ex.Message}";
            }
        }
    }

    public class SendPing
    {
        // 检测是否可以 Ping 通的方法
        public static bool IsReachable(string host)
        {
            WriteLog($"进入IsReachable。", LogLevel.Debug);

            bool IsReachable = false;
            Ping pingSender = new Ping();
            try
            {
                PingReply reply = pingSender.Send(host);
                if (reply.Status == IPStatus.Success)
                {
                    IsReachable = true;
                }
            }
            catch (Exception ex)
            {
                WriteLog($"遇到异常。", LogLevel.Error, ex);
            }

            WriteLog($"完成IsReachable。", LogLevel.Debug);

            return IsReachable;
        }

        public static string FindFastetsIP(string[] IP)
        {
            WriteLog($"进入FindFastetsIP。", LogLevel.Debug);

            Dictionary<string, long> PingResults = new Dictionary<string, long>();
            PingReply reply;
            Ping pingSender = new Ping();
            foreach (string ip in IP)
            {
                try
                {
                    reply = pingSender.Send(ip);
                    if (reply.Status == IPStatus.Success)
                    {
                        PingResults.Add(ip, reply.RoundtripTime);

                        WriteLog($"{ip}测试完成，时间：{reply.RoundtripTime}ms。", LogLevel.Info);
                    }
                }
                catch (Exception ex)
                {
                    WriteLog($"遇到异常。", LogLevel.Error, ex);
                }
            }
            if (PingResults.Count == 0)
            {
                return null;
            }

            var fastestIP = PingResults.OrderBy(kv => kv.Value).FirstOrDefault();

            WriteLog($"完成FindFastetsIP。", LogLevel.Debug);

            return fastestIP.Key;
        }
    }

    public class Github
    {
        // 确保 api.github.com 可以正常访问的方法
        // 解决由于 api.github.com 访问异常引起的有关问题（https://github.com/racpast/Pixiv-Nginx-GUI/issues/2）
        public static void EnsureGithubAPI()
        {
            WriteLog($"进入EnsureGithubAPI。", LogLevel.Debug);

            // api.github.com DNS A记录 IPv4 列表
            string[] APIIPAddress = new string[]
            {
                "20.205.243.168",
                "140.82.113.5",
                "140.82.116.6",
                "4.237.22.34"
            };

            string IPAddress = SendPing.FindFastetsIP(APIIPAddress);

            if (IPAddress == null)
            {
                string[] NewAPIRecord = new string[]
                {
                    "#\tapi.github.com Start",
                    $"{IPAddress}\tapi.github.com",
                    "#\tapi.github.com End"
                };
                FileHelper.FileHelper.WriteLinesToFileTop(NewAPIRecord, SystemHosts);
            }

            WriteLog($"完成EnsureGithubAPI。", LogLevel.Debug);
        }

        // Github 文件下载加速代理列表
        public static List<string> proxies = new List<string>{
            "gh.tryxd.cn",
            "cccccccccccccccccccccccccccccccccccccccccccccccccccc.cc",
            "gh.222322.xyz",
            "ghproxy.cc",
            "gh.catmak.name",
            "gh.nxnow.top",
            "ghproxy.cn",
            "ql.133.info",
            "cf.ghproxy.cc",
            "ghproxy.imciel.com",
            "g.blfrp.cn",
            "gh-proxy.ygxz.in",
            "ghp.keleyaa.com",
            "gh.pylas.xyz",
            "githubapi.jjchizha.com",
            "ghp.arslantu.xyz",
            "githubapi.jjchizha.com",
            "ghp.arslantu.xyz",
            "git.40609891.xyz",
            "firewall.lxstd.org",
            "gh.monlor.com",
            "slink.ltd",
            "github.geekery.cn",
            "gh.jasonzeng.dev",
            "github.tmby.shop",
            "gh.sixyin.com",
            "liqiu.love",
            "git.886.be",
            "github.xxlab.tech",
            "github.ednovas.xyz",
            "gh.xx9527.cn",
            "gh-proxy.linioi.com",
            "gitproxy.mrhjx.cn",
            "github.wuzhij.com",
            "git.speed-ssr.tech"
            };

        // 寻找最优代理的方法
        public static async Task<string> FindFastestProxy(List<string> proxies, string targetUrl)
        {
            WriteLog($"进入FindFastestProxy。", LogLevel.Debug);

            // 逐个测试代理延迟
            var proxyTasks = proxies.Select(async proxy =>
            {
                var proxyUri = new Uri($"https://{proxy}");
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) })
                {
                    var stopwatch = new Stopwatch();
                    stopwatch.Start();
                    try
                    {
                        var response = await client.GetAsync(proxyUri + targetUrl, HttpCompletionOption.ResponseHeadersRead);
                        response.EnsureSuccessStatusCode();

                        WriteLog(proxy + " —— " + response.Content.Headers, LogLevel.Debug);

                        return (proxy, stopwatch.ElapsedMilliseconds, null);
                    }
                    catch (Exception ex)
                    {
                        WriteLog(proxyUri + targetUrl + " —— " + ex, LogLevel.Debug);

                        return (proxy, stopwatch.ElapsedMilliseconds, ex);
                    }
                    finally
                    {
                        stopwatch.Stop();
                    }
                }
            }).ToList();

            // 等待测试全部完成
            var proxyResults = await Task.WhenAll(proxyTasks);

            // 输出到控制台
            foreach (var (proxy, ElapsedMilliseconds, ex) in proxyResults)
            {
                if (ex is TaskCanceledException)
                {
                    WriteLog(proxy + " —— 超时", LogLevel.Debug);
                }
                else if (ex != null)
                {
                    WriteLog(proxy + " —— 错误", LogLevel.Debug);
                }
                else
                {
                    WriteLog(proxy + " —— " + ElapsedMilliseconds + "ms", LogLevel.Debug);
                }
            }

            // 排除有错误的结果并排序
            var fastestProxy = proxyResults
                .Where(result => result.ex == null)
                .OrderBy(result => result.ElapsedMilliseconds)
                .First();

            WriteLog($"完成FindFastestProxy。", LogLevel.Debug);

            // 返回延迟最低的加速代理地址
            return fastestProxy.proxy;
        }
    }

    public class HTTPHelper
    {
        // 定义一个静态的HttpClient实例，用于HTTP请求
        private static readonly HttpClient _httpClient = new HttpClient
        {
            // 设置超时时间为10秒
            Timeout = TimeSpan.FromSeconds(10)
        };

        // 异步获取URL内容的方法
        public static async Task<string> GetAsync(string url)
        {
            WriteLog("进入GetAsync。", LogLevel.Debug);

            try
            {
                // 设置用户代理
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");

                // 发起HTTP GET请求
                HttpResponseMessage response = await _httpClient.GetAsync(url);
                // 确保请求成功
                response.EnsureSuccessStatusCode();

                string Output = await response.Content.ReadAsStringAsync();

                WriteLog($"完成GetAsync。", LogLevel.Debug);

                // 返回响应内容
                return Output;
            }
            catch (Exception ex)
            {
                WriteLog($"发送请求时遇到异常。", LogLevel.Error, ex);

                // 抛出异常
                throw ex;
            }
        }
    }

    public class IPValidator
    {
        public static bool IsValidIPv4(string ipAddress)
        {
            // 使用正则表达式验证 IPv4 地址的格式
            string pattern = @"^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$";

            if (!Regex.IsMatch(ipAddress, pattern))
            {
                return false;
            }

            return true;
        }

        public static bool IsValidIPv6(string ipAddress)
        {
            // 使用正则表达式验证 IPv6 地址的格式
            string pattern = @"^(([0-9a-fA-F]{1,4}:){7}[0-9a-fA-F]{1,4}|" +
                             @"(([0-9a-fA-F]{1,4}:){1,7}:)|" +
                             @"(([0-9a-fA-F]{1,4}:){1,6}:[0-9a-fA-F]{1,4})|" +
                             @"(([0-9a-fA-F]{1,4}:){1,5}(:[0-9a-fA-F]{1,4}){1,2})|" +
                             @"(([0-9a-fA-F]{1,4}:){1,4}(:[0-9a-fA-F]{1,4}){1,3})|" +
                             @"(([0-9a-fA-F]{1,4}:){1,3}(:[0-9a-fA-F]{1,4}){1,4})|" +
                             @"(([0-9a-fA-F]{1,4}:){1,2}(:[0-9a-fA-F]{1,4}){1,5})|" +
                             @"([0-9a-fA-F]{1,4}:((:[0-9a-fA-F]{1,4}){1,6}))|" +
                             @"(:((:[0-9a-fA-F]{1,4}){1,7}|:))|" +
                             @"(::(ffff(:0{1,4}){0,1}:){0,1}" +
                             @"(([0-9]{1,3}\.){3}[0-9]{1,3}))|" +
                             @"(([0-9a-fA-F]{1,4}:){1,4}:" +
                             @"([0-9]{1,3}\.){3}[0-9]{1,3}))$";

            if (!Regex.IsMatch(ipAddress, pattern))
            {
                return false;
            }

            return true;
        }
    }
}