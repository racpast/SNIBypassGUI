using SNIBypassGUI.Common;

namespace SNIBypassGUI.Models
{
    /// <summary>
    /// 表示一个 HTTP 头部条目。
    /// </summary>
    public class HttpHeader : NotifyPropertyChangedBase
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
        /// 创建当前 <see cref="HttpHeader"/> 实例的完整副本。
        /// </summary>
        /// <returns>当前对象的一个完整副本。</returns>
        public HttpHeader Clone()
        {
            return new HttpHeader
            {
                Name = Name,
                Value = Value
            };
        }
        #endregion
    }
}
