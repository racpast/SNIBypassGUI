using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MaterialDesignThemes.Wpf;
using Newtonsoft.Json.Linq;
using SNIBypassGUI.Enums;
using SNIBypassGUI.Utils.Extensions;
using SNIBypassGUI.Utils.Results;

namespace SNIBypassGUI.Models
{
    /// <summary>
    /// 表示一条供域名映射组使用的映射规则。
    /// </summary>
    public class DnsMappingRule : NotifyPropertyChangedBase
    {
        #region Fields
        private string _domainPattern;
        private DnsMappingRuleAction _ruleAction;
        private IpAddressSourceType _targetIpType;
        private string _targetIp;
        private string _queryDomain;
        private Guid? _resolverId;
        private IpAddressType _ipAddressType;
        private ObservableCollection<string> _fallbackIpAddresses;
        private DnsMappingGroup _parent;
        #endregion

        #region Properties
        /// <summary>
        /// 此规则匹配的域名模式。
        /// </summary>
        public string DomainPattern
        {
            get => _domainPattern;
            set
            {
                if (SetProperty(ref _domainPattern, value))
                    OnPropertyChanged(nameof(PrimaryDisplayText));
            }
        }

        /// <summary>
        /// 此规则要执行的操作类型。
        /// </summary>
        public DnsMappingRuleAction RuleAction
        {
            get => _ruleAction;
            set
            {
                if (SetProperty(ref _ruleAction, value))
                    OnPropertyChanged(nameof(ListIconKind));
            }
        }

        /// <summary>
        /// 此规则的目标地址模式。
        /// </summary>
        public IpAddressSourceType TargetIpType
        {
            get => _targetIpType;
            set
            {
                if (SetProperty(ref _targetIpType, value))
                {
                    OnPropertyChanged(nameof(ListIconKind));
                    OnPropertyChanged(nameof(SecondaryDisplayText));
                }
            }
        }

        /// <summary>
        /// 此规则匹配的域名或模式指向的值。
        /// </summary>
        public string TargetIp
        {
            get => _targetIp;
            set
            {
                if (SetProperty(ref _targetIp, value))
                    OnPropertyChanged(nameof(SecondaryDisplayText));
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
                    OnPropertyChanged(nameof(SecondaryDisplayText));
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
        /// 此规则的 IP 查询类型。
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
        /// 此规则的图标类型，供 UI 使用。
        /// </summary>
        public PackIconKind ListIconKind
        {
            get
            {
                return RuleAction switch
                {
                    DnsMappingRuleAction.IP => TargetIpType == IpAddressSourceType.Static ? PackIconKind.ArrowDecisionOutline : PackIconKind.ArrowDecisionAutoOutline,
                    DnsMappingRuleAction.Forward => PackIconKind.ShareOutline,
                    DnsMappingRuleAction.Block => PackIconKind.ShareOffOutline,
                    _ => PackIconKind.HelpCircle,
                };
            }
        }

        /// <summary>
        /// 此规则在列表中的主要展示文本，供 UI 使用。
        /// </summary>
        public string PrimaryDisplayText { get => DomainPattern.OrDefault("未指定"); }

        /// <summary>
        /// 此规则在列表中的次要展示文本，供 UI 使用。
        /// </summary>
        public string SecondaryDisplayText
        {
            get =>
                TargetIpType switch
                {
                    IpAddressSourceType.Static => $"{TargetIp.OrDefault("未指定")}",
                    IpAddressSourceType.Dynamic => $"{QueryDomain.OrDefault("未指定")}",
                    _ => null
                };
        }

        /// <summary>
        /// 此规则所属的映射组。
        /// </summary>
        public DnsMappingGroup Parent
        {
            get => _parent;
            internal set => SetProperty(ref _parent, value);
        }
        #endregion

        #region Methods
        /// <summary>
        /// 创建当前 <see cref="DnsMappingRule"/> 实例的完整副本。
        /// </summary>
        /// <returns>当前对象的一个完整副本。</returns>
        public DnsMappingRule Clone()
        {
            var clone = new DnsMappingRule
            {
                RuleAction = RuleAction,
                TargetIpType = TargetIpType,
                TargetIp = TargetIp,
                QueryDomain = QueryDomain,
                IpAddressType = IpAddressType,
                ResolverId = ResolverId,
                DomainPattern = DomainPattern,
                FallbackIpAddresses = [.. FallbackIpAddresses ?? []]
            };

            return clone;
        }

        /// <summary>
        /// 使用指定的 <see cref="DnsMappingRule"/> 实例的属性值更新当前实例的内容。
        /// </summary>
        public void UpdateFrom(DnsMappingRule source)
        {
            if (source == null) return;
            RuleAction = source.RuleAction;
            TargetIpType = source.TargetIpType;
            QueryDomain = source.QueryDomain;
            DomainPattern = source.DomainPattern;
            TargetIp = source.TargetIp;
            ResolverId = source.ResolverId;
            IpAddressType = source.IpAddressType;
            FallbackIpAddresses = [.. source.FallbackIpAddresses];
        }

        /// <summary>
        /// 将当前 <see cref="DnsMappingRule"/> 实例转换为 JSON 对象。
        /// </summary>
        public JObject ToJObject()
        {
            var jObject = new JObject
            {
                ["domainPattern"] = DomainPattern.OrDefault(),
                ["ruleAction"] = RuleAction.ToString()
            };

            if (RuleAction == DnsMappingRuleAction.IP)
            {
                jObject["targetIpType"] = TargetIpType.ToString();
                switch (TargetIpType)
                {
                    case IpAddressSourceType.Static:
                        jObject["targetIp"] = TargetIp.OrDefault();
                        break;
                    case IpAddressSourceType.Dynamic:
                        jObject["queryDomain"] = QueryDomain.OrDefault();
                        jObject["resolverId"] = ResolverId.ToString();
                        jObject["ipAddressType"] = IpAddressType.ToString();
                        jObject["fallbackIpAddresses"] = new JArray(FallbackIpAddresses.OrEmpty());
                        break;
                    default:
                        break;
                }
            }

            return jObject;
        }

        /// <summary>
        /// 从 JSON 对象创建一个新的 <see cref="DnsMappingRule"/> 实例。
        /// </summary>
        public static ParseResult<DnsMappingRule> FromJObject(JObject jObject)
        {
            if (jObject == null)
                return ParseResult<DnsMappingRule>.Failure("JSON 对象为空。");

            if (!jObject.TryGetString("domainPattern", out string domainPattern) ||
                !jObject.TryGetEnum("ruleAction", out DnsMappingRuleAction ruleAction))
                return ParseResult<DnsMappingRule>.Failure("一个或多个通用字段缺失或类型错误。");

            var rule = new DnsMappingRule
            {
                DomainPattern = domainPattern,
                RuleAction = ruleAction
            };

            if (ruleAction == DnsMappingRuleAction.IP)
            {
                if (!jObject.TryGetEnum("targetIpType", out IpAddressSourceType targetIpType))
                    return ParseResult<DnsMappingRule>.Failure("返回地址动作所需的字段缺失或类型错误。");
                rule.TargetIpType = targetIpType;

                switch (targetIpType)
                {
                    case IpAddressSourceType.Static:
                        if (!jObject.TryGetString("targetIp", out string targetIp))
                            return ParseResult<DnsMappingRule>.Failure("直接指定模式所需的字段缺失或类型错误。");
                        rule.TargetIp = targetIp;
                        break;

                    case IpAddressSourceType.Dynamic:
                        if (!jObject.TryGetString("queryDomain", out string queryDomain) ||
                            !jObject.TryGetNullableGuid("resolverId", out Guid? resolverId) ||
                            !jObject.TryGetEnum("ipAddressType", out IpAddressType ipAddressType) ||
                            !jObject.TryGetArray("fallbackIpAddresses", out IReadOnlyList<string> fallbackIpAddresses))
                            return ParseResult<DnsMappingRule>.Failure("自动解析模式所需的字段缺失或类型错误。");
                        rule.QueryDomain = queryDomain;
                        rule.ResolverId = resolverId;
                        rule.FallbackIpAddresses = [.. fallbackIpAddresses];
                        rule.IpAddressType = ipAddressType;
                        break;
                }
            }

            return ParseResult<DnsMappingRule>.Success(rule);
        }
        #endregion
    }
}