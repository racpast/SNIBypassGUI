using SNIBypassGUI.Enums;
using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Models;

namespace SNIBypassGUI.Factories
{
    public class UpstreamSourceFactory : IFactory<UpstreamSource>
    {
        /// <summary>
        /// 新建上游来源。
        /// </summary>
        public UpstreamSource CreateDefault()
        {
            return new UpstreamSource
            {
                SourceType = IpAddressSourceType.Static,
                Address = string.Empty,
                Port = "443",
                FallbackIpAddresses = []
            };
        }
    }
}
