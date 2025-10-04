using SNIBypassGUI.Common;

namespace SNIBypassGUI.Models
{
    /// <summary>
    /// 表示一个回落地址。
    /// </summary>
    public class FallbackAddress : NotifyPropertyChangedBase
    {
        #region Fields
        private string _address;
        private bool _isLocked;
        #endregion

        #region Properties
        /// <summary>
        /// 备用地址。
        /// </summary>
        public string Address
        {
            get => _address;
            set => SetProperty(ref _address, value);
        }

        /// <summary>
        /// 此备用地址是否被锁定，锁定后将不会被自动更新。
        /// </summary>
        public bool IsLocked
        {
            get => _isLocked;
            set => SetProperty(ref _isLocked, value);
        }
        #endregion

        #region Methods
        /// <summary>
        /// 创建当前 <see cref="FallbackAddress"/> 实例的完整副本。
        /// </summary>
        /// <returns>当前对象的一个完整副本。</returns>
        public FallbackAddress Clone()
        {
            var clone = new FallbackAddress
            {
                Address = Address,
                IsLocked = IsLocked
            };

            return clone;
        }

        /// <summary>
        /// 使用指定的 <see cref="FallbackAddress"/> 实例的属性值更新当前实例的内容。
        /// </summary>
        public void UpdateFrom(FallbackAddress fallbackAddress)
        {
            if (fallbackAddress == null) return;
            Address = fallbackAddress.Address;
            IsLocked = fallbackAddress.IsLocked;
        }
        #endregion
    }
}
