using DnsClient;
using DnsClient.Protocol;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using SNIBypassGUI.Models;
using static SNIBypassGUI.Consts.AppConsts;
using static SNIBypassGUI.Utils.LogManager;
using static SNIBypassGUI.Utils.WinApiUtils;

namespace SNIBypassGUI.Utils
{
    public class NetworkUtils
    {
        /// <summary>
        /// 刷新 DNS 缓存
        /// </summary>
        public static UInt32 FlushDNSCache() => DnsFlushResolverCache();

        /// <summary>
        /// 通用 DNS 解析方法
        /// </summary>
        /// <param name="domain">域名</param>
        /// <param name="queryType">记录类型</param>
        /// <param name="dnsServer">DNS 服务器 IP（默认系统 DNS）</param>
        /// <param name="timeoutMs">超时时间（毫秒）</param>
        public static async Task<List<DnsRecordResult>> ResolveDnsAsync(string domain, DnsQueryType queryType = DnsQueryType.A, string dnsServer = null, int timeoutMs = 2000)
        {
            var lookupClient = GetLookupClient(dnsServer, timeoutMs);
            var queryTypeMapped = MapQueryType(queryType);

            try
            {
                IDnsQueryResponse response = await lookupClient.QueryAsync(domain, queryTypeMapped);

                if (response.HasError)
                {
                    WriteLog($"DNS 查询时遇到错误：{response.ErrorMessage}（响应码：{response.Header.ResponseCode}）。", LogLevel.Error);
                    return [];
                }

                return [.. response.AllRecords
                    .Select(ParseDnsRecord)
                    .Where(record => record != null)];
            }
            catch (SocketException ex)
            {
                WriteLog($"解析域名时遇到网络错误。", LogLevel.Error, ex);
                return [];
            }
            catch (Exception ex)
            {
                WriteLog($"解析域名时遇到异常。", LogLevel.Error, ex);
                return [];
            }
        }

        // 解析 A 记录（IPv4）
        public static async Task<List<IPAddress>> ResolveAAsync(string domain, string dnsServer = DefaultDNS)
        {
            var records = await ResolveDnsAsync(domain, DnsQueryType.A, dnsServer);
            return [.. records
                .Where(r => r.RecordType == DnsQueryType.A)
                .Select(r => (IPAddress)r.Value)];
        }

        // 解析 AAAA 记录（IPv6）
        public static async Task<List<IPAddress>> ResolveAaaaAsync(string domain, string dnsServer = DefaultDNS)
        {
            var records = await ResolveDnsAsync(domain, DnsQueryType.AAAA, dnsServer);
            return [.. records
                .Where(r => r.RecordType == DnsQueryType.AAAA)
                .Select(r => (IPAddress)r.Value)];
        }

        // 辅助方法：解析 DNS 记录到结构化数据
        private static DnsRecordResult ParseDnsRecord(DnsResourceRecord record)
        {
            return record switch
            {
                ARecord a => new DnsRecordResult
                {
                    RecordType = DnsQueryType.A,
                    Name = a.DomainName,
                    TTL = TimeSpan.FromSeconds(a.TimeToLive),
                    Value = a.Address
                },
                AaaaRecord aaaa => new DnsRecordResult
                {
                    RecordType = DnsQueryType.AAAA,
                    Name = aaaa.DomainName,
                    TTL = TimeSpan.FromSeconds(aaaa.TimeToLive),
                    Value = aaaa.Address
                },
                CNameRecord cname => new DnsRecordResult
                {
                    RecordType = DnsQueryType.CNAME,
                    Name = cname.DomainName,
                    TTL = TimeSpan.FromSeconds(cname.TimeToLive),
                    Value = cname.CanonicalName
                },
                MxRecord mx => new DnsRecordResult
                {
                    RecordType = DnsQueryType.MX,
                    Name = mx.DomainName,
                    TTL = TimeSpan.FromSeconds(mx.TimeToLive),
                    Value = new { Priority = mx.Preference, Host = mx.Exchange }
                },
                TxtRecord txt => new DnsRecordResult
                {
                    RecordType = DnsQueryType.TXT,
                    Name = txt.DomainName,
                    TTL = TimeSpan.FromSeconds(txt.TimeToLive),
                    Value = string.Join(" ", txt.Text)
                },
                NsRecord ns => new DnsRecordResult
                {
                    RecordType = DnsQueryType.NS,
                    Name = ns.DomainName,
                    TTL = TimeSpan.FromSeconds(ns.TimeToLive),
                    Value = ns.NSDName
                },
                SoaRecord soa => new DnsRecordResult
                {
                    RecordType = DnsQueryType.SOA,
                    Name = soa.DomainName,
                    TTL = TimeSpan.FromSeconds(soa.TimeToLive),
                    Value = new
                    {
                        soa.MName,
                        soa.RName,
                        soa.Serial,
                        soa.Refresh,
                        soa.Retry,
                        soa.Expire,
                        soa.Minimum
                    }
                },
                PtrRecord ptr => new DnsRecordResult
                {
                    RecordType = DnsQueryType.PTR,
                    Name = ptr.DomainName,
                    TTL = TimeSpan.FromSeconds(ptr.TimeToLive),
                    Value = ptr.PtrDomainName
                },
                SrvRecord srv => new DnsRecordResult
                {
                    RecordType = DnsQueryType.SRV,
                    Name = srv.DomainName,
                    TTL = TimeSpan.FromSeconds(srv.TimeToLive),
                    Value = new
                    {
                        srv.Priority,
                        srv.Weight,
                        srv.Port,
                        srv.Target
                    }
                },
                CaaRecord caa => new DnsRecordResult
                {
                    RecordType = DnsQueryType.CAA,
                    Name = caa.DomainName,
                    TTL = TimeSpan.FromSeconds(caa.TimeToLive),
                    Value = new
                    {
                        caa.Flags,
                        caa.Tag,
                        caa.Value
                    }
                },
                _ => null // 忽略不支持的类型
            };
        }

        // 创建 LookupClient 实例
        private static LookupClient GetLookupClient(string dnsServer, int timeoutMs)
        {
            if (string.IsNullOrEmpty(dnsServer))
                return new LookupClient(); // 使用系统默认 DNS

            if (!IPAddress.TryParse(dnsServer, out var ip))
                throw new ArgumentException("无效的 DNS 服务器地址");

            var endpoint = new IPEndPoint(ip, 53);
            return new LookupClient(new LookupClientOptions(endpoint)
            {
                Timeout = TimeSpan.FromMilliseconds(timeoutMs),
                UseCache = false // 禁用缓存，强制实时查询
            });
        }

        // 映射查询类型
        private static QueryType MapQueryType(DnsQueryType queryType) => queryType switch
        {
            DnsQueryType.A => QueryType.A,
            DnsQueryType.AAAA => QueryType.AAAA,
            DnsQueryType.CNAME => QueryType.CNAME,
            DnsQueryType.MX => QueryType.MX,
            DnsQueryType.TXT => QueryType.TXT,
            DnsQueryType.NS => QueryType.NS,
            DnsQueryType.SOA => QueryType.SOA,
            DnsQueryType.PTR => QueryType.PTR,
            DnsQueryType.SRV => QueryType.SRV,
            DnsQueryType.CAA => QueryType.CAA,
            DnsQueryType.ANY => QueryType.ANY,
            _ => throw new ArgumentOutOfRangeException(nameof(queryType))
        };

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
        public static IPAddress FindFastestIP(IPAddress[] IP)
        {
            var PingResults = new Dictionary<IPAddress, long>();
            var pingSender = new Ping();
            PingReply reply;
            foreach (var ip in IP)
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
            return PingResults.Count == 0 ? null : PingResults.OrderBy(kv => kv.Value).FirstOrDefault().Key;
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

        /// <summary>
        /// 异步发送 GET 请求
        /// </summary>
        public static async Task<string> GetAsync(string url, double timeOut = 10, string userAgent = "Mozilla/5.0")
        {
            HttpClient _httpClient = new()
            {
                Timeout = TimeSpan.FromSeconds(timeOut)
            };
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                string Output = await response.Content.ReadAsStringAsync();
                return Output;
            }
            catch (Exception ex)
            {
                WriteLog("发送请求时遇到异常。", LogLevel.Error, ex);
                throw;
            }
            finally
            {
                _httpClient.Dispose();
            }
        }

        /// <summary>
        /// 尝试下载文件并返回是否成功
        /// </summary>
        public static async Task<bool> TryDownloadFile(string url, string savePath, Action<double> updateProgress, double timeOut = 60, string userAgent = "Mozilla/5.0")
        {
            HttpClient _httpClient = new()
            {
                Timeout = TimeSpan.FromSeconds(timeOut),
            };
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                Exception ex = new($"文件 {url} 下载失败！响应码：{response.StatusCode}");
                WriteLog("下载文件时遇到异常。", LogLevel.Error, ex);
                throw ex;
            }
            using var stream = await response.Content.ReadAsStreamAsync();
            long totalBytes = response.Content.Headers.ContentLength ?? 0;
            long bytesDownloaded = 0;

            using var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None);
            byte[] buffer = new byte[8192];
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                bytesDownloaded += bytesRead;
                double progress = (double)bytesDownloaded / totalBytes * 100;
                updateProgress(progress);
            }
            return true;
        }

        /// <summary>
        /// 尝试下载文件并显示进度
        /// </summary>
        public static async Task DownloadFileWithProgress(string Url, string savePath, Action<double> updateProgress, double timeOut = 60, string userAgent = "Mozilla/5.0")
        {
            try
            {
                await TryDownloadFile(Url, savePath, updateProgress, timeOut, userAgent);
            }
            catch (Exception ex)
            {
                WriteLog("从服务器下载文件遇到异常。", LogLevel.Error, ex);
                throw;
            }
        }
    }
}
