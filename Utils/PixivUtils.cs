using System.Net;
using System.Threading.Tasks;
using static SNIBypassGUI.Consts.PathConsts;
using static SNIBypassGUI.Utils.FileUtils;
using static SNIBypassGUI.Utils.LogManager;
using static SNIBypassGUI.Utils.NetworkUtils;

namespace SNIBypassGUI.Utils
{
    public static class PixivUtils
    {
        /// <summary>
        /// Pixiv IP 优选。
        /// </summary>
        public static async Task OptimizePixivIPRouting()
        {
            await Task.Run(async() =>
            {
                RemoveSection(SystemHosts, "s.pximg.net");
                IPAddress ip = FindFastestIP([.. await ResolveAAsync("s.pximg.net")]);
                if (ip != null)
                {
                    string[] NewAPIRecord =
                    [
                        "#\ts.pximg.net Start",
                        $"{ip}       s.pximg.net",
                        "#\ts.pximg.net End",
                    ];
                    PrependToFile(SystemHosts, NewAPIRecord);
                    FlushDNSCache();
                }
                else WriteLog("Pixiv IP 优选失败，没有找到最优 IP。", LogLevel.Warning);
            });
        }

        /// <summary>
        /// 恢复原始 Pixiv DNS。
        /// </summary>
        public static async Task RestoreOriginalPixivDNS()
        {
            await Task.Run(() =>
            {
                RemoveSection(SystemHosts, "s.pximg.net");
            });
        }
    }
}
