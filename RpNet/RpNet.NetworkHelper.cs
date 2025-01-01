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
    // --------------------------------------------------------------------------
    //
    //  NetAdp 类
    //  该类用于管理和操作系统中的网络适配器，提供了一系列与网络配置相关的功能。
    //  包括获取网络适配器信息、设置静态 IP 和子网掩码、配置网关与 DNS，以及生成网络适配器的详细报告。
    //  通过该类，用户可以灵活管理网络适配器的各种属性和设置。
    //
    //  功能：
    //  - 获取所有网络适配器的信息，包括名称、描述、类型、运行状态、IP 地址、子网掩码、网关、DNS 等。
    //  - 设置网络适配器的静态 IP、子网掩码、网关和 DNS。
    //  - 刷新指定网络适配器的信息。
    //  - 生成网络适配器的详细报告。
    //  - 提供只读属性访问适配器的各种基本信息。
    //
    //  注意：
    //  - 使用本类的方法时，请确保应用程序具有足够的权限来执行网络配置操作。
    //  - 在更改网络配置时，可能需要管理员权限或重启网络适配器。
    //  - 请确保参数设置的合法性，例如有效的 IP 地址、子网掩码和网关等，以避免配置失败或产生不可预期的行为。
    //
    //  示例：
    //  ```csharp
    //  List<NetAdp> adapters = NetAdp.GetAdapters();
    //  foreach (var adapter in adapters)
    //  {
    //      Console.WriteLine(adapter.Name);
    //      Console.WriteLine(adapter.IP);
    //  }
    //  ```
    //
    // --------------------------------------------------------------------------
    public class NetAdp
    {
        // 私有字段，存储网络适配器的属性
        private string name; // 网络适配器名称
        private string description; // 网络适配器描述
        private string serviceName; // 服务名
        private OperationalStatus status; // 网络适配器的运行状态
        private NetworkInterfaceType type; // 网络适配器的类型
        private string[] dns; // DNS服务器地址
        private Int32? interFace; // 接口索引
        private string gateway; // 默认网关地址
        private string ip; // IP地址
        private string mask; // 子网掩码
        private string guid;// GUID

        // 只读属性，用于访问私有字段
        public string Name { get { return name; } }
        public string Description { get { return description; } }
        public string GUID { get { return guid; } }
        public string ServiceName { get { return serviceName; } }
        public OperationalStatus Status { get { return status; } }
        public NetworkInterfaceType Type { get { return type; } }
        public string[] DNS
        {
            get
            {
                return dns;
            }
            set
            {
                // 使用 WMI 设置 DNS 服务器
                ManagementClass wmi = new ManagementClass("Win32_NetworkAdapterConfiguration");
                ManagementObjectCollection moc = wmi.GetInstances();
                ManagementBaseObject i = null;
                ManagementBaseObject o = null;
                foreach (ManagementObject mo in moc)
                {
                    // 匹配接口索引
                    if ((UInt32)mo["InterfaceIndex"] == Interface)
                    {
                        // 如果 value 为空，重置 DNS 设置
                        if (value == null)
                        {
                            UInt32 t;
                            t = (UInt32)mo.InvokeMethod("SetDNSServerSearchOrder", null);
                            if (t != 0)
                                throw new NetAdpSetException((UInt32)o["returnValue"]);
                            break;
                        }
                        else
                        {
                            // 设置新的 DNS 服务器地址
                            i = mo.GetMethodParameters("SetDNSServerSearchOrder");
                            i["DNSServerSearchOrder"] = value;
                            o = mo.InvokeMethod("SetDNSServerSearchOrder", i, null);
                            if ((UInt32)o["returnValue"] != 0)
                                throw new NetAdpSetException((UInt32)o["returnValue"]);
                            break;
                        }
                    }
                }
            }
        }

        public Int32? Interface { get { return interFace; } }
        public string Gateway
        {
            get
            {
                return gateway;
            }
            set
            {
                // 使用 WMI 设置网关地址
                ManagementClass wmi = new ManagementClass("Win32_NetworkAdapterConfiguration");
                ManagementObjectCollection moc = wmi.GetInstances();
                ManagementBaseObject i = null;
                ManagementBaseObject o = null;
                foreach (ManagementObject mo in moc)
                {
                    if ((UInt32)mo["InterfaceIndex"] == Interface)
                    {
                        // 如果 value 为空，启用 DHCP
                        if (value == null)
                        {
                            i = mo.GetMethodParameters("EnableDHCP");
                            o = mo.InvokeMethod("EnableDHCP", i, null);
                            if ((UInt32)o["returnValue"] != 0)
                                throw new NetAdpSetException((UInt32)o["returnValue"]);
                            break;
                        }
                        else
                        {
                            // 设置新的网关地址
                            i = mo.GetMethodParameters("SetGateways");
                            i["DefaultIPGateway"] = new string[] { value };
                            o = mo.InvokeMethod("SetGateways", i, null);
                            if ((UInt32)o["returnValue"] != 0)
                                throw new NetAdpSetException((UInt32)o["returnValue"]);
                            break;
                        }
                    }
                }
            }
        }

        public string IP { get { return ip; } }
        public string Mask { get { return mask; } }

        // 设置网络配置的方法，包括 IP、子网掩码、网关和 DNS
        void SetNetwork(string[] ip, string[] submask, string[] getway, string[] dns)
        {
            ManagementClass wmi = new ManagementClass("Win32_NetworkAdapterConfiguration");
            ManagementObjectCollection moc = wmi.GetInstances();
            ManagementBaseObject inPar = null;
            ManagementBaseObject outPar = null;
            foreach (ManagementObject mo in moc)
            {
                if (!(bool)mo["IPEnabled"]) // 检查是否启用 IP
                    continue;

                // 设置 IP 地址和子网掩码
                if (ip != null && submask != null)
                {
                    inPar = mo.GetMethodParameters("EnableStatic");
                    inPar["IPAddress"] = ip;
                    inPar["SubnetMask"] = submask;
                    outPar = mo.InvokeMethod("EnableStatic", inPar, null);
                }

                // 设置网关
                if (getway != null)
                {
                    inPar = mo.GetMethodParameters("SetGateways");
                    inPar["DefaultIPGateway"] = getway;
                    outPar = mo.InvokeMethod("SetGateways", inPar, null);
                }

                // 设置 DNS
                if (dns != null)
                {
                    inPar = mo.GetMethodParameters("SetDNSServerSearchOrder");
                    inPar["DNSServerSearchOrder"] = dns;
                    outPar = mo.InvokeMethod("SetDNSServerSearchOrder", inPar, null);
                }
                else if (dns[0] == "back")
                {
                    mo.InvokeMethod("SetDNSServerSearchOrder", null);
                }
            }
        }

        // 获取所有网络适配器的信息
        public static List<NetAdp> GetAdapters()
        {
            List<NetAdp> back = new List<NetAdp>();
            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapter");
            ManagementObjectCollection wmiadps = searcher.Get();

            foreach (NetworkInterface adapter in adapters)
            {
                NetAdp adp = new NetAdp();
                adp.name = adapter.Name;
                adp.description = adapter.Description;
                adp.status = adapter.OperationalStatus;
                adp.type = adapter.NetworkInterfaceType;

                IPInterfaceProperties adapterProperties = adapter.GetIPProperties();
                UnicastIPAddressInformationCollection uniCast = adapterProperties.UnicastAddresses;
                IPv4InterfaceProperties p = adapterProperties.GetIPv4Properties();
                if (uniCast.Count > 1)
                {
                    if (uniCast[1].IPv4Mask != null)
                    {
                        adp.ip = uniCast[1].Address.ToString();
                        adp.mask = uniCast[1].IPv4Mask.ToString();
                    }

                }
                GatewayIPAddressInformationCollection addresses = adapterProperties.GatewayAddresses;
                if (addresses.Count > 0)
                {
                    adp.gateway = addresses[0].Address.ToString();
                }
                IPAddressCollection dnsServers = adapterProperties.DnsAddresses;
                switch (dnsServers.Count)
                {
                    case 0:
                        adp.dns = new string[0];
                        break;
                    case 1:
                        adp.dns = new string[1];
                        adp.dns[0] = dnsServers[0].ToString();
                        break;
                    default:
                        adp.dns = new string[2];
                        adp.dns[0] = dnsServers[0].ToString();
                        adp.dns[1] = dnsServers[1].ToString();
                        break;
                }
                if (p != null)
                {
                    adp.interFace = p.Index;
                }
                foreach (ManagementObject mo in wmiadps)
                {
                    if ((string)mo.GetPropertyValue("NetConnectionID") == adp.name)
                    {
                        adp.guid = (string)mo.GetPropertyValue("GUID");
                        adp.serviceName = (string)mo.GetPropertyValue("ServiceName");
                    }
                }
                back.Add(adp);
            }
            return back;
        }

        // 返回所有适配器的信息报告
        public static string GetAdaptersReport()
        {
            List<NetAdp> adps = GetAdapters();
            string back = "";
            foreach (NetAdp adp in adps)
            {
                back += "==================================================\r\n";
                back += "名称:" + adp.Name + "\r\n";
                back += "描述:" + adp.Description + "\r\n";
                back += "类型:" + adp.Type.ToString() + "\r\n";
                back += "GUID:" + adp.guid + "\r\n";
                back += "状态:" + adp.Status.ToString() + "\r\n";
                back += "网关:" + adp.Gateway + "\r\n";
                back += "是否自动获取DNS：" + adp.IsDnsAutomatic + "\r\n";
                if (adp.DNS != null)
                {
                    foreach (string d in adp.DNS)
                    {
                        back += "DNS:" + d + "\r\n";
                    }
                }
                back += "接口:" + adp.Interface + "\r\n";
                back += "服务名:" + adp.ServiceName + "\r\n";
                back += "IP：" + adp.IP + "\r\n";
                back += "子网掩码：" + adp.Mask + "\r\n";
            }
            return back;
        }

        // 刷新当前适配器的信息
        public void Fresh()
        {
            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapter");
            ManagementObjectCollection wmiadps = searcher.Get();

            foreach (NetworkInterface adapter in adapters)
            {
                if (adapter.Name == name)
                {
                    status = adapter.OperationalStatus;
                    type = adapter.NetworkInterfaceType;

                    IPInterfaceProperties adapterProperties = adapter.GetIPProperties();
                    UnicastIPAddressInformationCollection uniCast = adapterProperties.UnicastAddresses;
                    IPv4InterfaceProperties p = adapterProperties.GetIPv4Properties();
                    if (uniCast.Count > 1)
                    {
                        if (uniCast[1].IPv4Mask != null)
                        {
                            ip = uniCast[1].Address.ToString();
                            mask = uniCast[1].IPv4Mask.ToString();
                        }

                    }
                    GatewayIPAddressInformationCollection addresses = adapterProperties.GatewayAddresses;
                    if (addresses.Count > 0)
                    {
                        gateway = addresses[0].Address.ToString();
                    }

                    IPAddressCollection dnsServers = adapterProperties.DnsAddresses;
                    switch (dnsServers.Count)
                    {
                        case 1:
                            dns = new string[1];
                            dns[0] = dnsServers[0].ToString();
                            break;
                        case 2:
                            dns = new string[2];
                            dns[0] = dnsServers[0].ToString();
                            dns[1] = dnsServers[1].ToString();
                            break;
                    }
                    if (p != null)
                    {
                        interFace = p.Index;
                    }

                    foreach (ManagementObject mo in wmiadps)
                    {
                        if ((string)mo.GetPropertyValue("NetConnectionID") == name)
                        {
                            serviceName = (string)mo.GetPropertyValue("ServiceName");
                        }
                    }

                    break;
                }
            }
        }

        // 设置静态 IP 和子网掩码
        public void SetStatic(string ip, string mask)
        {
            ManagementClass wmi = new ManagementClass("Win32_NetworkAdapterConfiguration");
            ManagementObjectCollection moc = wmi.GetInstances();
            ManagementBaseObject i = null;
            ManagementBaseObject o = null;
            foreach (ManagementObject mo in moc)
            {
                if ((UInt32)mo["InterfaceIndex"] == Interface)
                {
                    if ((ip == null) || (mask == null))
                    {
                        i = mo.GetMethodParameters("EnableDHCP");
                        o = mo.InvokeMethod("EnableDHCP", i, null);
                        if ((UInt32)o["returnValue"] != 0)
                            throw new NetAdpSetException((UInt32)o["returnValue"]);
                        break;
                    }
                    else
                    {
                        i = mo.GetMethodParameters("EnableStatic");
                        i["IPAddress"] = new string[] { ip };
                        i["SubnetMask"] = new string[] { mask };
                        o = mo.InvokeMethod("EnableStatic", i, null);
                        if ((UInt32)o["returnValue"] != 0)
                            throw new NetAdpSetException((UInt32)o["returnValue"]);
                        break;
                    }
                }
            }
        }

        // 禁用网络适配器的 IPv6
        public bool DisableIPv6()
        {
            if (!string.IsNullOrEmpty(name))
            {
                using (var powerShell = PowerShell.Create())
                {
                    powerShell.AddScript($"Disable-NetAdapterBinding -Name '{name}' -ComponentID ms_tcpip6");
                    powerShell.Invoke();
                    if (powerShell.HadErrors)
                    {
                        return false;
                    }
                    return true;
                }
            }
            return false;
        }

        // 启用网络适配器的 IPv6
        public bool EnableIPv6()
        {
            if (!string.IsNullOrEmpty(name))
            {
                using (var powerShell = PowerShell.Create())
                {
                    powerShell.AddScript($"Enable-NetAdapterBinding -Name '{name}' -ComponentID ms_tcpip6");
                    powerShell.Invoke();
                    if (powerShell.HadErrors)
                    {
                        return false;
                    }
                    return true;
                }
            }
            return false;
        }

        // 检查 DNS 是否为自动获取
        public bool IsDnsAutomatic
        {
            get
            {
                if (string.IsNullOrEmpty(GUID))
                    return false;

                string path = "HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters\\Interfaces\\" + GUID;
                string ns = (string)Registry.GetValue(path, "NameServer", null);
                if (String.IsNullOrEmpty(ns))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        // 重写 ToString 方法，返回适配器名称
        public override string ToString()
        {
            return this.name;
        }

    }

    // 自定义异常类，用于表示网络适配器设置错误
    public class NetAdpSetException : Exception
    {
        private UInt32 code; // 错误码

        public UInt32 Code { get { return code; } }
        new public string Source { get; protected set; } // 错误来源

        public NetAdpSetException(UInt32 c) : base("设置时发生错误！错误码：" + c.ToString())
        {
            code = c;
            // 根据错误码设置错误来源描述
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
        public static bool PingHost(string host)
        {
            WriteLog($"进入PingHost。", LogLevel.Debug);

            bool pingable = false;
            Ping pingSender = new Ping();
            try
            {
                PingReply reply = pingSender.Send(host);
                if (reply.Status == IPStatus.Success)
                {
                    pingable = true;
                }
            }
            catch (Exception ex)
            {
                WriteLog($"遇到异常：{ex}。", LogLevel.Error, ex);
            }

            WriteLog($"完成PingHost。", LogLevel.Debug);

            return pingable;
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
            List<string> APIIPAddress = new List<string>
            {
                "20.205.243.168",
                "140.82.113.5",
                "140.82.116.6",
                "4.237.22.34"
            };
            foreach (string IPAddress in APIIPAddress)
            {
                bool isReachable = SendPing.PingHost(IPAddress);

                WriteLog($"{IPAddress}测试完成，Ping结果：{isReachable}。", LogLevel.Info);

                if (isReachable)
                {
                    string[] NewAPIRecord =
                    {
                        "#\tapi.github.com Start",
                        $"{IPAddress}\tapi.github.com",
                        "#\tapi.github.com End"
                    };
                    FileHelper.FileHelper.WriteLinesToFile(NewAPIRecord, SystemHosts);
                    break;
                }
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