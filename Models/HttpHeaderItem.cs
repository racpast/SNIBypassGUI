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
        #endregion
    }
}
