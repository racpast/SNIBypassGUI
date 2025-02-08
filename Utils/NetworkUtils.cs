using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using static SNIBypassGUI.Utils.LogManager;

namespace SNIBypassGUI.Utils
{
    public class NetworkUtils
    {
        [DllImport("dnsapi.dll", EntryPoint = "DnsFlushResolverCache")]
        static extern UInt32 DnsFlushResolverCache();

        /// <summary>
        /// 刷新 DNS 缓存
        /// </summary>
        public static UInt32 FlushDNSCache() => DnsFlushResolverCache();

        /// <summary>
        /// 从域名获取 IP 地址
        /// </summary>
        /// <param name="domainName">指定的域名</param>
        public static string GetIpAddressFromDomain(string domainName)
        {
            try
            {
                // 获取域名解析到的所有 IP 地址
                IPAddress[] ipAddresses = Dns.GetHostAddresses(domainName);

                // 如果解析结果不为空，返回第一个 IP 地址
                return ipAddresses.Length > 0 ? ipAddresses[0].ToString() : string.Empty;
            }
            catch (Exception ex)
            {
                WriteLog($"解析域名 {domainName} 时遇到异常。", LogLevel.Error, ex);

                // 解析失败，返回空字符串
                return string.Empty;
            }
        }

        /// <summary>
        /// 检查主机是否可达
        /// </summary>
        /// <param name="host">指定的主机</param>
        public static bool IsReachable(string host)
        {
            var pingSender = new Ping();
            try
            {
                PingReply reply = pingSender.Send(host);
                return reply.Status == IPStatus.Success;
            }
            catch (Exception ex)
            {
                WriteLog($"Ping 主机 {host} 时遇到异常。", LogLevel.Error, ex);
                return false;
            }
        }

        /// <summary>
        /// 查找最快的 IP 地址
        /// </summary>
        /// <param name="IP">包含 IP 地址的字符串数组</param>
        public static string FindFastestIP(string[] IP)
        {
            var PingResults = new Dictionary<string, long>();
            var pingSender = new Ping();
            PingReply reply;
            foreach (string ip in IP)
            {
                try
                {
                    reply = pingSender.Send(ip);
                    if (reply.Status == IPStatus.Success) PingResults.Add(ip, reply.RoundtripTime);
                }
                catch (Exception ex)
                {
                    WriteLog($"Ping 主机 {ip} 时遇到异常。", LogLevel.Error, ex);
                    continue;
                }
            }
            return PingResults.Count == 0 ? string.Empty : PingResults.OrderBy(kv => kv.Value).FirstOrDefault().Key;
        }

        /// <summary>
        /// 检查 IP 地址是否有效
        /// </summary>
        /// <param name="ipAddress">IP 地址</param>
        public static bool IsValidIPAddress(string ipAddress) => IsValidIPv4(ipAddress) || IsValidIPv6(ipAddress);

        /// <summary>
        /// 检查 IPv4 地址是否有效
        /// </summary>
        /// <param name="ipAddress">IP 地址</param>
        public static bool IsValidIPv4(string ipAddress) => IPAddress.TryParse(ipAddress, out IPAddress address) && address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;

        /// <summary>
        /// 检查 IPv6 地址是否有效
        /// </summary>
        /// <param name="ipAddress">IP 地址</param>
        public static bool IsValidIPv6(string ipAddress) => IPAddress.TryParse(ipAddress, out IPAddress address) && address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6;

        /// <summary>
        /// 检查端口是否被占用
        /// </summary>
        /// <param name="port">指定的端口</param>
        /// <param name="checkUdp">是否检查 UDP 端口</param>
        /// <returns>指示指定的端口是否被占用</returns>
        public static bool IsPortInUse(int port, bool checkUdp = false)
        {
            try
            {
                IPGlobalProperties ipProperties = IPGlobalProperties.GetIPGlobalProperties();
                IPEndPoint[] tcpEndPoints = ipProperties.GetActiveTcpListeners();
                if (tcpEndPoints.Any(endPoint => endPoint.Port == port)) return true;
                if (checkUdp)
                {
                    IPEndPoint[] udpEndPoints = ipProperties.GetActiveUdpListeners();
                    return udpEndPoints.Any(endPoint => endPoint.Port == port);
                }
                return false;
            }
            catch (Exception ex)
            {
                WriteLog($"检查端口 {port} 时遇到异常。", LogLevel.Error, ex);
                return false;
            }
        }

        private static readonly HttpClient _httpClient = new();

        /// <summary>
        /// 异步发送 GET 请求
        /// </summary>
        /// <param name="url">指定链接</param>
        /// <param name="timeOut">超时时间，以秒为单位</param>
        /// <param name="userAgent">用户代理</param>
        public static async Task<string> GetAsync(string url, double timeOut = 10, string userAgent = "Mozilla/5.0")
        {
            try
            {
                _httpClient.Timeout = TimeSpan.FromSeconds(timeOut);
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
                HttpResponseMessage response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                string Output = await response.Content.ReadAsStringAsync();
                return Output;
            }
            catch (Exception ex)
            {
                WriteLog($"发送请求时遇到异常。", LogLevel.Error, ex);
                throw;
            }
        }
    }
}
