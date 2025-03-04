using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using static SNIBypassGUI.Utils.LogManager;
using static SNIBypassGUI.Consts.GitHubConsts;

namespace SNIBypassGUI.Utils
{
    public static class GitHubUtils
    {
        /*
        /// <summary>
        /// 确保 api.github.com 可以正常访问
        /// </summary>
        public static bool EnsureGitHubAPI()
        {
            string IPAddress = FindFastestIP(GitHubAPIServerIPs);
            if (!string.IsNullOrEmpty(IPAddress))
            {
                string[] NewAPIRecord =
                [
                    "#\tapi.github.com Start",
                    $"{IPAddress}\tapi.github.com",
                    "#\tapi.github.com End"
                ];
                PrependToFile(SystemHosts, NewAPIRecord);
                return true;
            }
            return false;
        }
        */

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
