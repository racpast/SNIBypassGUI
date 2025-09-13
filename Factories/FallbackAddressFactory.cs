using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Models;

namespace SNIBypassGUI.Factories
{
    public class FallbackAddressFactory : IFactory<FallbackAddress>
    {
        /// <summary>
        /// 新建回落地址。
        /// </summary>
        public FallbackAddress CreateDefault()
        {
            return new FallbackAddress
            {
                Address = string.Empty,
                IsLocked = false
            };
        }
    }
}
