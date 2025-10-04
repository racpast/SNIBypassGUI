using Newtonsoft.Json.Linq;
using SNIBypassGUI.Common.Results;

namespace SNIBypassGUI.Interfaces
{
    public interface IMapper<T>
    {
        /// <summary>
        /// 将 <typeparamref name="T"/> 实例转换为 JObject。
        /// </summary>
        JObject ToJObject(T model);

        /// <summary>
        /// 从 JObject 创建 <typeparamref name="T"/> 实例。
        /// </summary>
        ParseResult<T> FromJObject(JObject jObject);
    }

}
