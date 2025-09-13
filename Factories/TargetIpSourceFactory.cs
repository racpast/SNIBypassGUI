using SNIBypassGUI.Enums;
using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Models;

namespace SNIBypassGUI.Factories
{
    public class TargetIpSourceFactory : IFactory<TargetIpSource>
    {
        /// <summary>
        /// 新建目标来源。
        /// </summary>
        public TargetIpSource CreateDefault()
        {
            return new TargetIpSource
            {
                Address = string.Empty,
                SourceType = IpAddressSourceType.Static,
                IpAddressType = IpAddressType.IPv4Only,
                QueryDomain = string.Empty,
                ResolverId = null,
                EnableFallbackAutoUpdate = true,
                FallbackIpAddresses = []
            };
        }
    }
}
