using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using static SNIBypassGUI.Consts.GitHubConsts;
using static SNIBypassGUI.Consts.PathConsts;
using static SNIBypassGUI.Utils.FileUtils;
using static SNIBypassGUI.Utils.LogManager;
using static SNIBypassGUI.Utils.NetworkUtils;

namespace SNIBypassGUI.Utils
{
    public static class GitHubUtils
    {
        /// <summary>
        /// GitHub API 优选
        /// </summary>
        public static async Task OptimizeGitHubAPIRouting()
        {
            RemoveSection(SystemHosts, "api.github.com");
            IPAddress ip = FindFastestIP([.. await ResolveAAsync("api.github.com")]);
            if (ip != null)
            {
                string[] NewAPIRecord =
                [
                "#\tapi.github.com Start",
                $"{ip}       api.github.com",
                "#\tapi.github.com End",
                ];
                PrependToFile(SystemHosts, NewAPIRecord);
                FlushDNSCache();
            }
            else WriteLog("GitHub API 优选失败，没有找到最优 IP。", LogLevel.Warning);
        }

        /// <summary>
        /// 恢复原始 GitHub API DNS
        /// </summary>
        public static void RestoreOriginalGitHubAPIDNS() => RemoveSection(SystemHosts, "api.github.com");

        /// <summary>
        /// 寻找最优代理
        /// </summary>
        /// <param name="targetUrl">目标 URL</param>
        public static async Task<string> FindFastestProxy(string targetUrl)
        {
            var proxyTasks = ghproxyMirrors.Select(async proxy =>
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
            var proxyResults = await Task.WhenAll(proxyTasks);
            foreach (var (proxy, ElapsedMilliseconds, ex) in proxyResults)
            {
                if (ex is TaskCanceledException) WriteLog(proxy + " —— 超时", LogLevel.Debug);
                else if (ex != null) WriteLog(proxy + " —— 错误", LogLevel.Debug);
                else WriteLog(proxy + " —— " + ElapsedMilliseconds + "ms", LogLevel.Debug);
            }
            var fastestProxy = proxyResults
                .Where(result => result.ex == null)
                .OrderBy(result => result.ElapsedMilliseconds)
                .First();
            return fastestProxy.proxy;
        }
    }
}
