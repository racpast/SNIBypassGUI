using Newtonsoft.Json.Linq;
using SNIBypassGUI.Common.Extensions;
using SNIBypassGUI.Common.Results;
using SNIBypassGUI.Models;
using SNIBypassGUI.Interfaces;

namespace SNIBypassGUI.Common.Mapper
{
    public class HttpHeaderItemMapper : IMapper<HttpHeaderItem>
    {
        /// <summary>
        /// 将 <see cref="HttpHeaderItem"/> 类型的 <paramref name="httpHeaderItem"/> 实例转换为 JSON 对象。
        /// </summary>
        public JObject ToJObject(HttpHeaderItem httpHeaderItem)
        {
            var jObject = new JObject
            {
                ["name"] = httpHeaderItem.Name.OrDefault(),
                ["value"] = httpHeaderItem.Value.OrDefault()
            };
            return jObject;
        }

        /// <summary>
        /// 从 JSON 对象创建一个新的 <see cref="HttpHeaderItem"/> 实例。
        /// </summary>
        public ParseResult<HttpHeaderItem> FromJObject(JObject jObject)
        {
            if (jObject == null)
                return ParseResult<HttpHeaderItem>.Failure("JSON 对象为空。");

            if (!jObject.TryGetString("name", out var name) ||
                !jObject.TryGetString("value", out var value))
                return ParseResult<HttpHeaderItem>.Failure("一个或多个通用字段缺失或类型错误。");

            var item = new HttpHeaderItem
            {
                Name = name,
                Value = value
            };

            return ParseResult<HttpHeaderItem>.Success(item);
        }

    }
}
