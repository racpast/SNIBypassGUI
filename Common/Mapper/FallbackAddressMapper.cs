using Newtonsoft.Json.Linq;
using SNIBypassGUI.Common.Extensions;
using SNIBypassGUI.Common.Results;
using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Models;

namespace SNIBypassGUI.Common.Mapper
{
    public class FallbackAddressMapper : IMapper<FallbackAddress>
    {
        /// <summary>
        /// 将 <see cref="FallbackAddress"/> 类型的 <paramref name="fallbackAddress"/> 实例转换为 JSON 对象。
        /// </summary>
        public JObject ToJObject(FallbackAddress fallbackAddress)
        {
            var jObject = new JObject
            {
                ["address"] = fallbackAddress.Address.ToString().OrDefault(),
                ["isLocked"] = fallbackAddress.IsLocked
            };

            return jObject;
        }

        /// <summary>
        /// 从 JSON 对象创建一个新的 <see cref="FallbackAddress"/> 实例。
        /// </summary>
        public ParseResult<FallbackAddress> FromJObject(JObject jObject)
        {
            if (jObject == null)
                return ParseResult<FallbackAddress>.Failure("JSON 对象为空。");

            if (!jObject.TryGetString("address", out string address) ||
                !jObject.TryGetBool("isLocked", out bool isLocked))
                return ParseResult<FallbackAddress>.Failure("一个或多个通用字段缺失或类型错误。");

            var fallbackAddress = new FallbackAddress
            {
                Address = address,
                IsLocked = isLocked
            };

            return ParseResult<FallbackAddress>.Success(fallbackAddress);
        }
    }
}
