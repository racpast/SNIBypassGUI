using System;
using Newtonsoft.Json.Linq;
using SNIBypassGUI.Common.Extensions;
using SNIBypassGUI.Common.Results;
using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Models;

namespace SNIBypassGUI.Common.Mapper
{
    public class HttpHeaderMapper : IMapper<HttpHeader>
    {
        /// <summary>
        /// 将 <see cref="HttpHeader"/> 类型的 <paramref name="httpHeader"/> 实例转换为 JSON 对象。
        /// </summary>
        public JObject ToJObject(HttpHeader httpHeader)
        {
            var jObject = new JObject
            {
                ["name"] = httpHeader.Name.OrDefault(),
                ["value"] = httpHeader.Value.OrDefault()
            };
            return jObject;
        }

        /// <summary>
        /// 从 JSON 对象创建一个新的 <see cref="HttpHeader"/> 实例。
        /// </summary>
        public ParseResult<HttpHeader> FromJObject(JObject jObject)
        {
            if (jObject == null)
                return ParseResult<HttpHeader>.Failure("JSON 对象为空。");

            try
            {
                if (!jObject.TryGetString("name", out var name) ||
                    !jObject.TryGetString("value", out var value))
                    return ParseResult<HttpHeader>.Failure("一个或多个通用字段缺失或类型错误。");

                var item = new HttpHeader
                {
                    Name = name,
                    Value = value
                };

                return ParseResult<HttpHeader>.Success(item);
            }
            catch (Exception ex)
            {
                return ParseResult<HttpHeader>.Failure($"解析 HttpHeader 时遇到异常：{ex.Message}");
            }
        }
    }
}
