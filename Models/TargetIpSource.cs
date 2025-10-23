using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Linq;
using MaterialDesignThemes.Wpf;
using SNIBypassGUI.Common;
using SNIBypassGUI.Common.Extensions;
using SNIBypassGUI.Enums;

namespace SNIBypassGUI.Models
{
    /// <summary>
    /// 表示一个供映射规则使用的目标来源。
    /// </summary>
#warning 应将 UI 属性移至 ViewModel 层以遵循 MVVM 原则。
    public class TargetIpSource : NotifyPropertyChangedBase
    {
        #region Fields
        private IpAddressSourceType _sourceType;
        private ObservableCollection<string> _addresses = [];
        private ObservableCollection<string> _queryDomains = [];
        private Guid? _resolverId;
        private IpAddressType _ipAddressType;
        private bool _enableFallbackAutoUpdate;
        private ObservableCollection<FallbackAddress> _fallbackIpAddresses = [];
        #endregion

        #region Properties
        /// <summary>
        /// 此来源的类型。
        /// </summary>
        public IpAddressSourceType SourceType
        {
            get => _sourceType;
            set
            {
                if (SetProperty(ref _sourceType, value))
                    OnPropertyChanged(nameof(ListIconKind), nameof(DisplayText));
            }
        }

        /// <summary>
        /// 此来源直接指定的地址列表。
        /// </summary>
        public ObservableCollection<string> Addresses
        {
            get => _addresses;
            set
            {
                if (_addresses != value)
                {
                    var oldCollection = _addresses;

                    if (SetProperty(ref _addresses, value))
                    {
                        if (oldCollection != null)
                            oldCollection.CollectionChanged -= Addresses_CollectionChanged;

                        if (_addresses != null)
                            _addresses.CollectionChanged += Addresses_CollectionChanged;

                        Addresses_CollectionChanged(this, new(NotifyCollectionChangedAction.Reset));
                    }
                }
            }
        }

        /// <summary>
        /// 使用解析器解析的域名列表。
        /// </summary>
        public ObservableCollection<string> QueryDomains
        {
            get => _queryDomains;
            set
            {
                if (_queryDomains != value)
                {
                    var oldCollection = _queryDomains;

                    if (SetProperty(ref _queryDomains, value))
                    {
                        if (oldCollection != null)
                            oldCollection.CollectionChanged -= QueryDomains_CollectionChanged;

                        if (_queryDomains != null)
                            _queryDomains.CollectionChanged += QueryDomains_CollectionChanged;

                        QueryDomains_CollectionChanged(this, new(NotifyCollectionChangedAction.Reset));
                    }
                }
            }

        }

        /// <summary>
        /// 用于解析指定域名的解析器 ID。
        /// </summary>
        public Guid? ResolverId
        {
            get => _resolverId;
            set => SetProperty(ref _resolverId, value);
        }

        /// <summary>
        /// 此来源的 IP 查询类型。
        /// </summary>
        public IpAddressType IpAddressType
        {
            get => _ipAddressType;
            set => SetProperty(ref _ipAddressType, value);
        }

        /// <summary>
        /// 解析域名失败后使用的回退 IP 地址列表。
        /// </summary>
        public ObservableCollection<FallbackAddress> FallbackIpAddresses
        {
            get => _fallbackIpAddresses;
            set
            {
                if (_fallbackIpAddresses != value)
                {
                    // 缓存旧值
                    var oldCollection = _fallbackIpAddresses;

                    if (SetProperty(ref _fallbackIpAddresses, value))
                    {
                        // 解绑旧事件
                        if (oldCollection != null)
                        {
                            foreach (var old in oldCollection)
                                old.PropertyChanged -= FallbackAddress_PropertyChanged;

                            oldCollection.CollectionChanged -= FallbackAddresses_CollectionChanged;
                        }

                        // 绑定新事件
                        if (_fallbackIpAddresses != null)
                        {
                            _fallbackIpAddresses.CollectionChanged += FallbackAddresses_CollectionChanged;
                            foreach (var f in _fallbackIpAddresses)
                                f.PropertyChanged += FallbackAddress_PropertyChanged;
                        }

                        FallbackAddresses_CollectionChanged(this, new(NotifyCollectionChangedAction.Reset));
                    }
                }
            }
        }

        /// <summary>
        /// 此来源的图标类型，供 UI 使用。
        /// </summary>
        public PackIconKind ListIconKind
        {
            get => SourceType switch
            {
                IpAddressSourceType.Static => PackIconKind.PlaylistEdit,
                IpAddressSourceType.Dynamic => PackIconKind.CloudSearchOutline,
                _ => PackIconKind.HelpCircleOutline
            };
        }

        /// <summary>
        /// 此来源在列表中的展示文本，供 UI 使用。
        /// </summary>
        public string DisplayText
        {
            get =>
                SourceType switch
                {
                    IpAddressSourceType.Static => $"{(Addresses?.Any() == true ? string.Join("、", Addresses) : "未指定")}",
                    IpAddressSourceType.Dynamic => $"{(QueryDomains?.Any() == true ? string.Join("、", QueryDomains) : "未指定")}",
                    _ => null
                };
        }

        /// <summary>
        /// 此来源的回退 IP 地址列表是否自动更新。
        /// </summary>
        public bool EnableFallbackAutoUpdate
        {
            get => _enableFallbackAutoUpdate;
            set => SetProperty(ref _enableFallbackAutoUpdate, value);
        }
        #endregion

        #region Methods
        private void Addresses_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) =>
            OnPropertyChanged(nameof(Addresses), nameof(DisplayText));

        private void QueryDomains_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) =>
            OnPropertyChanged(nameof(QueryDomains), nameof(DisplayText));

        private void FallbackAddresses_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (FallbackAddress oldItem in e.OldItems)
                    oldItem.PropertyChanged -= FallbackAddress_PropertyChanged;
            }
            if (e.NewItems != null)
            {
                foreach (FallbackAddress newItem in e.NewItems)
                    newItem.PropertyChanged += FallbackAddress_PropertyChanged;
            }
            OnPropertyChanged(nameof(FallbackIpAddresses));
        }

        private void FallbackAddress_PropertyChanged(object sender, PropertyChangedEventArgs e) =>
            OnPropertyChanged(nameof(FallbackIpAddresses));

        /// <summary>
        /// 创建当前 <see cref="TargetIpSource"/> 实例的完整副本。
        /// </summary>
        /// <returns>当前对象的一个完整副本。</returns>
        public TargetIpSource Clone()
        {
            var clone = new TargetIpSource
            {
                SourceType = SourceType,
                Addresses = [.. Addresses.OrEmpty()],
                QueryDomains = [.. QueryDomains.OrEmpty()],
                ResolverId = ResolverId,
                IpAddressType = IpAddressType,
                EnableFallbackAutoUpdate = EnableFallbackAutoUpdate,
                FallbackIpAddresses = [.. FallbackIpAddresses.OrEmpty().Select(f => f.Clone())]
            };

            return clone;
        }

        /// <summary>
        /// 使用指定的 <see cref="TargetIpSource"/> 实例的属性值更新当前实例的内容。
        /// </summary>
        public void UpdateFrom(TargetIpSource source)
        {
            if (source == null) return;
            SourceType = source.SourceType;
            Addresses = [.. source.Addresses.OrEmpty()];
            QueryDomains = [.. source.QueryDomains.OrEmpty()];
            ResolverId = source.ResolverId;
            IpAddressType = source.IpAddressType;
            EnableFallbackAutoUpdate = source.EnableFallbackAutoUpdate;
            FallbackIpAddresses = [.. source.FallbackIpAddresses.OrEmpty().Select(f => f.Clone())];
        }
        #endregion
    }
}
