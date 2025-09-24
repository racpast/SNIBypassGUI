using Newtonsoft.Json.Linq;
using SNIBypassGUI.Common;
using SNIBypassGUI.Common.Extensions;
using SNIBypassGUI.Common.Results;

namespace SNIBypassGUI.Models
{
    /// <summary>
    /// 表示一个 HTTP 头部条目。
    /// </summary>
    public class HttpHeaderItem : NotifyPropertyChangedBase
    {
        #region Fields
        private string _name;
        private string _value;
        #endregion

        #region Properties
        /// <summary>
        /// 获取或设置 HTTP 头部的名称。
        /// </summary>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        /// <summary>
        /// 获取或设置 HTTP 头部的值。
        /// </summary>
        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }
        #endregion

        #region Methods
        /// <summary>
        /// 创建当前 <see cref="HttpHeaderItem"/> 实例的完整副本。
        /// </summary>
        /// <returns>当前对象的一个完整副本。</returns>
        public HttpHeaderItem Clone()
        {
            return new HttpHeaderItem
            {
                Name = Name,
                Value = Value
            };
        }

        /// <summary>
        /// 将当前 <see cref="HttpHeaderItem"/> 实例转换为 JSON 对象。
        /// </summary>
        public JObject ToJObject()
        {
            var jObject = new JObject
            {
                ["name"] = Name.OrDefault(),
                ["value"] = Value.OrDefault()
            };
            return jObject;
        }

        /// <summary>
        /// 从 JSON 对象创建一个新的 <see cref="HttpHeaderItem"/> 实例。
        /// </summary>
        public static ParseResult<HttpHeaderItem> FromJObject(JObject jObject)
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
        #endregion
    }
}
