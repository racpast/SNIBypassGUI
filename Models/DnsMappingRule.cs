using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using MaterialDesignThemes.Wpf;
using Newtonsoft.Json.Linq;
using SNIBypassGUI.Enums;
using SNIBypassGUI.Utils.Extensions;
using SNIBypassGUI.Utils.Network;
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
        private ObservableCollection<TargetIpSource> _targetSources;
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
                    OnPropertyChanged(nameof(DisplayText));
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
                {
                    OnPropertyChanged(nameof(ListIconKind));
                    OnPropertyChanged(nameof(RequiresIPv6));
                }
            }
        }

        /// <summary>
        /// 此规则的目标地址来源列表。
        /// </summary>
        public ObservableCollection<TargetIpSource> TargetSources
        {
            get => _targetSources;
            set
            {
                if (SetProperty(ref _targetSources, value))
                {
                    OnPropertyChanged(nameof(RequiresIPv6));

                    if (_targetSources != null)
                    {
                        _targetSources.CollectionChanged += (s, e) =>
                        {
                            if (e.OldItems != null)
                            {
                                foreach (TargetIpSource oldItem in e.OldItems)
                                    oldItem.PropertyChanged -= TargetSource_PropertyChanged;
                            }
                            if (e.NewItems != null)
                            {
                                foreach (TargetIpSource newItem in e.NewItems)
                                    newItem.PropertyChanged += TargetSource_PropertyChanged;
                            }

                            OnPropertyChanged(nameof(RequiresIPv6));
                        };

                        foreach (var item in _targetSources)
                            item.PropertyChanged += TargetSource_PropertyChanged;
                    }
                }
            }
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
                    DnsMappingRuleAction.IP => PackIconKind.ArrowDecisionOutline,
                    DnsMappingRuleAction.Forward => PackIconKind.ShareOutline,
                    DnsMappingRuleAction.Block => PackIconKind.ShareOffOutline,
                    _ => PackIconKind.HelpCircle,
                };
            }
        }

        /// <summary>
        /// 此规则在列表中的主要展示文本，供 UI 使用。
        /// </summary>
        public string DisplayText { get => DomainPattern.OrDefault("未指定"); }

        /// <summary>
        /// 此规则是否需要 IPv6 支持。
        /// </summary>
        public bool RequiresIPv6
        {
            get
            {
                if (RuleAction != DnsMappingRuleAction.IP)
                    return false;

                return TargetSources?.All(source =>
                {
                    if (source.SourceType == IpAddressSourceType.Static)
                        return NetworkUtils.RequiresPublicIPv6(source.Address);
                    else if (source.SourceType == IpAddressSourceType.Dynamic)
                    {
                        if (source.ResolverId == null)
                        {
                            if (source.FallbackIpAddresses?.All(fallback =>
                                NetworkUtils.RequiresPublicIPv6(fallback.Address)) == true)
                                return true;
                        }
                        else if (source.IpAddressType == IpAddressType.IPv6Only)
                        {
                            var relevantFallbacks = source.EnableFallbackAutoUpdate
                                ? source.FallbackIpAddresses?.Where(f => f.IsLocked)
                                : source.FallbackIpAddresses;

                            // 如果没有任何锁定回落或所有回落都是公网 IPv6
                            if (relevantFallbacks == null || !relevantFallbacks.Any() ||
                                relevantFallbacks.All(f => NetworkUtils.RequiresPublicIPv6(f.Address)))
                                return true;
                        }
                    }
                    return false;
                }) == true;
            }
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
        private void TargetSource_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(TargetIpSource.Address)
                or nameof(TargetIpSource.SourceType)
                or nameof(TargetIpSource.IpAddressType)
                or nameof(TargetIpSource.ResolverId)
                or nameof(TargetIpSource.FallbackIpAddresses))
                OnPropertyChanged(nameof(RequiresIPv6));
        }

        /// <summary>
        /// 创建当前 <see cref="DnsMappingRule"/> 实例的完整副本。
        /// </summary>
        /// <returns>当前对象的一个完整副本。</returns>
        public DnsMappingRule Clone()
        {
            var clone = new DnsMappingRule
            {
                RuleAction = RuleAction,
                DomainPattern = DomainPattern,
                TargetSources = [.. TargetSources.OrEmpty().Select(s => s.Clone())]
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
            DomainPattern = source.DomainPattern;
            TargetSources = [.. source.TargetSources.OrEmpty().Select(s => s.Clone())];
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
                jObject["targetSources"] = new JArray(TargetSources?.Select(s => s.ToJObject()).OrEmpty());

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
                if (!jObject.TryGetArray("targetSources", out IReadOnlyList<JObject> targetSourceObjects))
                    return ParseResult<DnsMappingRule>.Failure("返回地址动作所需的字段缺失或类型错误。");
                ObservableCollection<TargetIpSource> targetSources = [];
                foreach (var item in targetSourceObjects.OfType<JObject>())
                {
                    var parsed = TargetIpSource.FromJObject(item);
                    if (!parsed.IsSuccess)
                        return ParseResult<DnsMappingRule>.Failure($"解析 targetSources 时出错：{parsed.ErrorMessage}");
                    targetSources.Add(parsed.Value);
                }
                rule.TargetSources = targetSources;
            }

            return ParseResult<DnsMappingRule>.Success(rule);
        }
        #endregion
    }
}