using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Linq;
using MaterialDesignThemes.Wpf;
using Newtonsoft.Json.Linq;
using SNIBypassGUI.Enums;
using SNIBypassGUI.Common;
using SNIBypassGUI.Common.Extensions;
using SNIBypassGUI.Common.Results;

namespace SNIBypassGUI.Models
{
    /// <summary>
    /// 表示一个供映射规则使用的目标来源。
    /// </summary>
    public class TargetIpSource : NotifyPropertyChangedBase
    {
        #region Fields
        private IpAddressSourceType _sourceType;
        private ObservableCollection<string> _addresses;
        private ObservableCollection<string> _queryDomains;
        private Guid? _resolverId;
        private IpAddressType _ipAddressType;
        private bool _enableFallbackAutoUpdate;
        private ObservableCollection<FallbackAddress> _fallbackIpAddresses;
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
                {
                    OnPropertyChanged(nameof(ListIconKind));
                    OnPropertyChanged(nameof(DisplayText));
                }
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
        private void Addresses_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) 
        {
            OnPropertyChanged(nameof(Addresses));
            OnPropertyChanged(nameof(DisplayText));
        }

        private void QueryDomains_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(QueryDomains));
            OnPropertyChanged(nameof(DisplayText));
        }

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

        /// <summary>
        /// 将当前 <see cref="TargetIpSource"/> 实例转换为 JSON 对象。
        /// </summary>
        public JObject ToJObject()
        {
            var jObject = new JObject { ["sourceType"] = SourceType.ToString() };

            if (SourceType == IpAddressSourceType.Static)
                jObject["addresses"] = new JArray(Addresses.OrEmpty());
            else
            {
                jObject["queryDomains"] = new JArray(QueryDomains.OrEmpty());
                jObject["resolverId"] = ResolverId.ToString();
                jObject["ipAddressType"] = IpAddressType.ToString();
                jObject["enableFallbackAutoUpdate"] = EnableFallbackAutoUpdate;
                jObject["fallbackIpAddresses"] = new JArray(FallbackIpAddresses?.Select(f => f.ToJObject()).OrEmpty());
            }

            return jObject;
        }

        /// <summary>
        /// 从 JSON 对象创建一个新的 <see cref="TargetIpSource"/> 实例。
        /// </summary>
        public static ParseResult<TargetIpSource> FromJObject(JObject jObject)
        {
            if (jObject == null)
                return ParseResult<TargetIpSource>.Failure("JSON 对象为空。");

            if (!jObject.TryGetEnum("sourceType", out IpAddressSourceType sourceType))
                return ParseResult<TargetIpSource>.Failure("一个或多个通用字段缺失或类型错误。");

            var source = new TargetIpSource { SourceType = sourceType };

            if (sourceType == IpAddressSourceType.Static)
            {
                if (!jObject.TryGetArray("addresses", out IReadOnlyList<string> addresses))
                    return ParseResult<TargetIpSource>.Failure("直接指定模式所需的字段缺失或类型错误。");

                source.Addresses = [.. addresses];
            }
            else
            {
                if (!jObject.TryGetArray("queryDomains", out IReadOnlyList<string> queryDomains) ||
                    !jObject.TryGetNullableGuid("resolverId", out Guid? resolverId) ||
                    !jObject.TryGetEnum("ipAddressType", out IpAddressType ipAddressType) ||
                    !jObject.TryGetBool("enableFallbackAutoUpdate", out bool enableFallbackAutoUpdate) ||
                    !jObject.TryGetArray("fallbackIpAddresses", out IReadOnlyList<JObject> fallbackIpAddressObjects))
                    return ParseResult<TargetIpSource>.Failure("解析获取模式所需的字段缺失或类型错误。");
                ObservableCollection<FallbackAddress> fallbackIpAddresses = [];
                foreach (var item in fallbackIpAddressObjects.OfType<JObject>())
                {
                    var parsed = FallbackAddress.FromJObject(item);
                    if (!parsed.IsSuccess)
                        return ParseResult<TargetIpSource>.Failure($"解析 fallbackIpAddresses 时出错：{parsed.ErrorMessage}");
                    fallbackIpAddresses.Add(parsed.Value);
                }
                source.QueryDomains = [.. queryDomains];
                source.ResolverId = resolverId;
                source.IpAddressType = ipAddressType;
                source.FallbackIpAddresses = fallbackIpAddresses;
                source.EnableFallbackAutoUpdate = enableFallbackAutoUpdate;
            }

            return ParseResult<TargetIpSource>.Success(source);
        }
        #endregion
    }
}