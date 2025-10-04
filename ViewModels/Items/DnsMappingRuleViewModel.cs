using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using MaterialDesignThemes.Wpf;
using SNIBypassGUI.Common;
using SNIBypassGUI.Common.Network;
using SNIBypassGUI.Enums;
using SNIBypassGUI.Models;

namespace SNIBypassGUI.ViewModels.Items
{
    public class DnsMappingRuleViewModel : NotifyPropertyChangedBase, IDisposable
    {
        #region State & Core Properties
        private readonly Func<Guid?, ResolverConfig> _resolverLookup;

        public DnsMappingRule Model { get; }
        public DnsMappingGroupViewModel Parent { get; }
        #endregion

        #region UI Properties
        public bool RequiresIPv6
        {
            get
            {
                // 如果规则不涉及 IP，或者没有任何来源，直接返回 false
                if (Model.RuleAction != DnsMappingRuleAction.IP || Model.TargetSources == null || !Model.TargetSources.Any())
                    return false;

                // 只要有任何一个来源满足条件，就返回 true
                return Model.TargetSources.Any(source =>
                {
                    if (source.SourceType == IpAddressSourceType.Static)
                        foreach (var ip in source.Addresses)
                            if (NetworkUtils.RequiresPublicIPv6(ip))
                                return true;

                    if (source.SourceType != IpAddressSourceType.Dynamic) return false;

                    if (!source.ResolverId.HasValue)
                    {
                        return source.FallbackIpAddresses != null && source.FallbackIpAddresses.Any() &&
                               source.FallbackIpAddresses.All(f => NetworkUtils.RequiresPublicIPv6(f.Address));
                    }

                    var resolver = _resolverLookup(source.ResolverId.Value);

                    if (resolver?.RequiresIPv6 == true) return true;

                    if (source.IpAddressType == IpAddressType.IPv6Only)
                    {
                        if (source.FallbackIpAddresses == null || !source.FallbackIpAddresses.Any())
                            return true;

                        // 如果启用了回落地址自动更新，那只有锁定的地址才算数
                        if (source.EnableFallbackAutoUpdate)
                        {
                            var lockedFallbacks = source.FallbackIpAddresses.Where(f => f.IsLocked);
                            // 如果没有任何锁定的回落地址，或者所有锁定的回落地址都是 IPv6，
                            // 那就没有 IPv4 的备胎，所以也视为需要 IPv6。
                            return !lockedFallbacks.Any() || lockedFallbacks.All(f => NetworkUtils.RequiresPublicIPv6(f.Address));
                        }
                    }

                    // 如果以上所有条件都不满足，则此来源不需要 IPv6
                    return false;
                });
            }
        }

        public PackIconKind ListIconKind => Model.RuleAction switch
        {
            DnsMappingRuleAction.IP => PackIconKind.ArrowDecisionOutline,
            DnsMappingRuleAction.Forward => PackIconKind.ShareOutline,
            DnsMappingRuleAction.Block => PackIconKind.ShareOffOutline,
            _ => PackIconKind.HelpCircle,
        };

        public string DisplayText => (Model.DomainPatterns?.Any() == true)
            ? string.Join("、", Model.DomainPatterns)
            : "未指定";
        #endregion

        #region Constructor
        public DnsMappingRuleViewModel(DnsMappingRule model, DnsMappingGroupViewModel parent, Func<Guid?, ResolverConfig> resolverLookup)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            Parent = parent ?? throw new ArgumentNullException(nameof(parent));
            _resolverLookup = resolverLookup ?? throw new ArgumentNullException(nameof(resolverLookup));

            // 订阅 Model 的属性变化
            Model.PropertyChanged += OnModelPropertyChanged;

            if (Model.DomainPatterns != null)
                Model.DomainPatterns.CollectionChanged += OnModelDomainPatternsCollectionChanged;

            if (Model.TargetSources != null)
            {
                Model.TargetSources.CollectionChanged += OnTargetSourcesCollectionChanged;
                foreach (var source in Model.TargetSources)
                    source.PropertyChanged += OnTargetSourcePropertyChanged;
            }
        }

        #endregion

        #region Public Methods
        public void RefreshIPv6Status() =>
            OnPropertyChanged(nameof(RequiresIPv6));
        #endregion

        #region Event Handlers & Private Helpers
        private void OnModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(DnsMappingRule.RuleAction):
                    OnPropertyChanged(nameof(ListIconKind));
                    OnPropertyChanged(nameof(RequiresIPv6));
                    break;

                case nameof(DnsMappingRule.DomainPatterns):
                    OnPropertyChanged(nameof(DisplayText));
                    break;

                case nameof(DnsMappingRule.TargetSources):
                    OnPropertyChanged(nameof(RequiresIPv6));
                    break;
            }
        }

        private void OnModelDomainPatternsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) 
            => OnPropertyChanged(nameof(DisplayText));

        private void OnTargetSourcesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
                foreach (TargetIpSource item in e.OldItems) item.PropertyChanged -= OnTargetSourcePropertyChanged;
            if (e.NewItems != null)
                foreach (TargetIpSource item in e.NewItems) item.PropertyChanged += OnTargetSourcePropertyChanged;

            OnPropertyChanged(nameof(RequiresIPv6));
        }

        private void OnTargetSourcePropertyChanged(object sender, PropertyChangedEventArgs e) 
            =>OnPropertyChanged(nameof(RequiresIPv6));
        #endregion

        #region IDisposable Implementation
        public void Dispose()
        {
            Model.PropertyChanged -= OnModelPropertyChanged;

            if (Model.DomainPatterns != null)
                Model.DomainPatterns.CollectionChanged -= OnModelDomainPatternsCollectionChanged;

            if (Model.TargetSources != null)
            {
                Model.TargetSources.CollectionChanged -= OnTargetSourcesCollectionChanged;
                foreach (var source in Model.TargetSources)
                    source.PropertyChanged -= OnTargetSourcePropertyChanged;
            }
        }
        #endregion
    }
}