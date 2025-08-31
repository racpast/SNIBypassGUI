using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using MaterialDesignThemes.Wpf;
using Newtonsoft.Json.Linq;
using SNIBypassGUI.Enums;
using SNIBypassGUI.Utils.Extensions;
using SNIBypassGUI.Utils.Results;

namespace SNIBypassGUI.Models
{
    /// <summary>
    /// 表示一个供上游组使用的上游来源。
    /// </summary>
    public class UpstreamSource : NotifyPropertyChangedBase
    {
        #region Fields
        private IpAddressSourceType _sourceType;
        private string _address;
        private string _queryDomain;
        private Guid? _resolverId;
        private IpAddressType _ipAddressType;
        private ObservableCollection<string> _fallbackIpAddresses;
        private string _port;
        private string _weight;
        private string _maxFails;
        private string _failTimeout;
        private string _maxConns;
        private UpstreamServerStatus _status;
        #endregion

        #region Properties
        /// <summary>
        /// 此上游来源的类型。
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
        /// 此上游来源的地址。
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
        public ObservableCollection<string> FallbackIpAddresses
        {
            get => _fallbackIpAddresses;
            set => SetProperty(ref _fallbackIpAddresses, value);
        }

        /// <summary>
        /// 此上游来源的端口。
        /// </summary>
        public string Port { get => _port; set => SetProperty(ref _port, value); }

        /// <summary>
        /// 此上游来源的权重。
        /// </summary>
        public string Weight { get => _weight; set => SetProperty(ref _weight, value); }

        /// <summary>
        /// 此上游来源的最大失败次数。
        /// </summary>
        public string MaxFails { get => _maxFails; set => SetProperty(ref _maxFails, value); }

        /// <summary>
        /// 此上游来源的失败超时时间。
        /// </summary>
        public string FailTimeout { get => _failTimeout; set => SetProperty(ref _failTimeout, value); }

        /// <summary>
        /// 此上游来源的最大连接数。
        /// </summary>
        public string MaxConns { get => _maxConns; set => SetProperty(ref _maxConns, value); }

        /// <summary>
        /// 此上游来源的状态。
        /// </summary>
        public UpstreamServerStatus Status
        {
            get => _status;
            set
            {
                if (SetProperty(ref _status, value))
                    OnPropertyChanged(nameof(ListTypeDescription));
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
        /// 此来源的类型描述，供 UI 使用。
        /// </summary>
        public string ListTypeDescription
        {
            get =>
                Status switch
                {
                    UpstreamServerStatus.Active => "(活动)",
                    UpstreamServerStatus.Backup => "(备份)",
                    UpstreamServerStatus.Down => "(停用)",
                    _ => null
                };
        }
        #endregion

        #region Methods
        /// <summary>
        /// 创建当前 <see cref="UpstreamSource"/> 实例的完整副本。
        /// </summary>
        /// <returns>当前对象的一个完整副本。</returns>
        public UpstreamSource Clone()
        {
            var clone = new UpstreamSource
            {
                SourceType = SourceType,
                Address = Address,
                QueryDomain = QueryDomain,
                ResolverId = ResolverId,
                IpAddressType = IpAddressType,
                Port = Port,
                Weight = Weight,
                MaxFails = MaxFails,
                FailTimeout = FailTimeout,
                MaxConns = MaxConns,
                Status = Status,
                FallbackIpAddresses = [.. FallbackIpAddresses.OrEmpty()]
            };

            return clone;
        }

        /// <summary>
        /// 使用指定的 <see cref="UpstreamSource"/> 实例的属性值更新当前实例的内容。
        /// </summary>
        public void UpdateFrom(UpstreamSource source)
        {
            if (source == null) return;
            SourceType = source.SourceType;
            Address = source.Address;
            QueryDomain = source.QueryDomain;
            ResolverId = source.ResolverId;
            IpAddressType = source.IpAddressType;
            FallbackIpAddresses = [.. source.FallbackIpAddresses.OrEmpty()];
            Port = source.Port;
            Weight = source.Weight;
            MaxFails = source.MaxFails;
            FailTimeout = source.FailTimeout;
            MaxConns = source.MaxConns;
            Status = source.Status;
        }

        /// <summary>
        /// 将当前 <see cref="UpstreamSource"/> 实例转换为 JSON 对象。
        /// </summary>
        public JObject ToJObject()
        {
            var jObject = new JObject
            {
                ["sourceType"] = SourceType.ToString(),
                ["port"] = Port.OrDefault(),
                ["weight"] = Weight.OrDefault(),
                ["maxFails"] = MaxFails.OrDefault(),
                ["failTimeout"] = FailTimeout.OrDefault(),
                ["maxConns"] = MaxConns.OrDefault(),
                ["status"] = Status.ToString()
            };

            if (SourceType == IpAddressSourceType.Static)
                jObject["address"] = Address.OrDefault();
            else
            {
                jObject["queryDomain"] = QueryDomain.OrDefault();
                jObject["resolverId"] = ResolverId.ToString();
                jObject["ipAddressType"] = IpAddressType.ToString();
                jObject["fallbackIpAddresses"] = new JArray(FallbackIpAddresses.OrEmpty());
            }

            return jObject;
        }

        /// <summary>
        /// 从 JSON 对象创建一个新的 <see cref="UpstreamSource"/> 实例。
        /// </summary>
        public static ParseResult<UpstreamSource> FromJObject(JObject jObject)
        {
            if (jObject == null)
                return ParseResult<UpstreamSource>.Failure("JSON 对象为空。");

            if (!jObject.TryGetEnum("sourceType", out IpAddressSourceType sourceType) ||
                !jObject.TryGetString("port", out string port) ||
                !jObject.TryGetString("weight", out string weight) ||
                !jObject.TryGetString("maxFails", out string maxFails) ||
                !jObject.TryGetString("failTimeout", out string failTimeout) ||
                !jObject.TryGetString("maxConns", out string maxConns) ||
                !jObject.TryGetEnum("status", out UpstreamServerStatus status))
                return ParseResult<UpstreamSource>.Failure("一个或多个通用字段缺失或类型错误。");

            var source = new UpstreamSource
            {
                SourceType = sourceType,
                Port = port,
                Weight = weight,
                MaxFails = maxFails,
                FailTimeout = failTimeout,
                MaxConns = maxConns,
                Status = status
            };

            if (sourceType == IpAddressSourceType.Static)
            {
                if (!jObject.TryGetString("address", out string address))
                    return ParseResult<UpstreamSource>.Failure("静态类型所需的字段缺失或类型错误。");

                source.Address = address;
            }
            else
            {
                if (!jObject.TryGetString("queryDomain", out string queryDomain) ||
                    !jObject.TryGetNullableGuid("resolverId", out Guid? resolverId) ||
                    !jObject.TryGetEnum("ipAddressType", out IpAddressType ipAddressType) ||
                    !jObject.TryGetArray("fallbackIpAddresses", out IReadOnlyList<string> fallbackIpAddresses))
                    return ParseResult<UpstreamSource>.Failure("动态类型所需的字段缺失或类型错误。");

                source.QueryDomain = queryDomain;
                source.ResolverId = resolverId;
                source.IpAddressType = ipAddressType;
                source.FallbackIpAddresses = [.. fallbackIpAddresses];
            }

            return ParseResult<UpstreamSource>.Success(source);
        }
        #endregion
    }
}
