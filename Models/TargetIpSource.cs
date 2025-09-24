using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    /// 表示一个供映射规则使用的目标地址来源。
    /// </summary>
    public class TargetIpSource : NotifyPropertyChangedBase
    {
        #region Fields
        private IpAddressSourceType _sourceType;
        private string _address;
        private string _queryDomain;
        private Guid? _resolverId;
        private IpAddressType _ipAddressType;
        private bool _enableFallbackAutoUpdate;
        private ObservableCollection<FallbackAddress> _fallbackIpAddresses;
        #endregion

        #region Properties
        /// <summary>
        /// 此地址来源的类型。
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
        /// 此地址来源的地址。
        /// </summary>
        public string Address
        {
            get => _address;
            set
            {
                if (SetProperty(ref _address, value))
                    OnPropertyChanged(nameof(DisplayText));
            }
        }

        /// <summary>
        /// 使用解析器解析的域名。
        /// </summary>
        public string QueryDomain
        {
            get => _queryDomain;
            set
            {
                if (SetProperty(ref _queryDomain, value))
                    OnPropertyChanged(nameof(DisplayText));
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
                if (SetProperty(ref _fallbackIpAddresses, value))
                {
                    // 先解绑旧的事件
                    if (_fallbackIpAddresses != null)
                    {
                        foreach (var old in _fallbackIpAddresses)
                            old.PropertyChanged -= FallbackAddress_PropertyChanged;

                        _fallbackIpAddresses.CollectionChanged -= FallbackAddresses_CollectionChanged;
                    }

                    if (_fallbackIpAddresses != null)
                    {
                        _fallbackIpAddresses.CollectionChanged += FallbackAddresses_CollectionChanged;
                        foreach (var f in _fallbackIpAddresses)
                            f.PropertyChanged += FallbackAddress_PropertyChanged;
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
                    IpAddressSourceType.Static => $"{Address.OrDefault("未指定")}",
                    IpAddressSourceType.Dynamic => $"{QueryDomain.OrDefault("未指定")}",
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
        private void FallbackAddresses_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
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
                Address = Address,
                QueryDomain = QueryDomain,
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
            Address = source.Address;
            QueryDomain = source.QueryDomain;
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
                jObject["address"] = Address.OrDefault();
            else
            {
                jObject["queryDomain"] = QueryDomain.OrDefault();
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
                if (!jObject.TryGetString("address", out string address))
                    return ParseResult<TargetIpSource>.Failure("静态类型所需的字段缺失或类型错误。");

                source.Address = address;
            }
            else
            {
                if (!jObject.TryGetString("queryDomain", out string queryDomain) ||
                    !jObject.TryGetNullableGuid("resolverId", out Guid? resolverId) ||
                    !jObject.TryGetEnum("ipAddressType", out IpAddressType ipAddressType) ||
                    !jObject.TryGetBool("enableFallbackAutoUpdate", out bool enableFallbackAutoUpdate) ||
                    !jObject.TryGetArray("fallbackIpAddresses", out IReadOnlyList<JObject> fallbackIpAddressObjects))
                    return ParseResult<TargetIpSource>.Failure("自动解析模式所需的字段缺失或类型错误。");
                ObservableCollection<FallbackAddress> fallbackIpAddresses = [];
                foreach (var item in fallbackIpAddressObjects.OfType<JObject>())
                {
                    var parsed = FallbackAddress.FromJObject(item);
                    if (!parsed.IsSuccess)
                        return ParseResult<TargetIpSource>.Failure($"解析 fallbackIpAddresses 时出错：{parsed.ErrorMessage}");
                    fallbackIpAddresses.Add(parsed.Value);
                }
                source.QueryDomain = queryDomain;
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