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
        private readonly Func<Guid?, bool> _requiresIpv6Lookup;

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

                    if (_requiresIpv6Lookup(source.ResolverId)) return true;

                    if (source.IpAddressType == IpAddressType.IPv6Only)
                    {
                        if (source.FallbackIpAddresses == null || !source.FallbackIpAddresses.Any())
                            return true;

                        // 如果启用了回落地址自动更新，那只有锁定的地址才算数
                        if (source.EnableFallbackAutoUpdate)
                        {
                            var lockedFallbacks = source.FallbackIpAddresses.Where(f => f.IsLocked);
                            // 如果没有任何锁定的回落地址，或者所有锁定的回落地址都是 IPv6，
                            // 那就没有 IPv4 的备胎，所以也视为需要 IPv6
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
        public DnsMappingRuleViewModel(DnsMappingRule model, DnsMappingGroupViewModel parent, Func<Guid?, bool> requiresIpv6Lookup)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            Parent = parent ?? throw new ArgumentNullException(nameof(parent));
            _requiresIpv6Lookup = requiresIpv6Lookup ?? throw new ArgumentNullException(nameof(requiresIpv6Lookup));

            Model.PropertyChanged += OnModelPropertyChanged;

            if (Model.DomainPatterns != null)
                Model.DomainPatterns.CollectionChanged += OnModelDomainPatternsCollectionChanged;

            if (Model.TargetSources != null)
            {
                Model.TargetSources.CollectionChanged += OnTargetSourcesCollectionChanged;
                foreach (var source in Model.TargetSources)
                    ListenToSource(source);
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
            OnPropertyChanged(e.PropertyName);

            switch (e.PropertyName)
            {
                case nameof(DnsMappingRule.RuleAction):
                    OnPropertyChanged(nameof(ListIconKind), nameof(RequiresIPv6));
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
                foreach (TargetIpSource item in e.OldItems) StopListeningToSource(item); // 使用辅助方法取消订阅
            if (e.NewItems != null)
                foreach (TargetIpSource item in e.NewItems) ListenToSource(item); // 使用辅助方法进行订阅

            OnPropertyChanged(nameof(RequiresIPv6));
        }

        private void OnFallbackAddressesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
                foreach (FallbackAddress item in e.OldItems) item.PropertyChanged -= OnSourceDependenciesChanged;
            if (e.NewItems != null)
                foreach (FallbackAddress item in e.NewItems) item.PropertyChanged += OnSourceDependenciesChanged;

            OnPropertyChanged(nameof(RequiresIPv6));
        }

        private void OnSourceDependenciesChanged(object sender, EventArgs e)
            => OnPropertyChanged(nameof(RequiresIPv6));

        private void ListenToSource(TargetIpSource source)
        {
            source.PropertyChanged += OnSourceDependenciesChanged;
            source.Addresses.CollectionChanged += OnSourceDependenciesChanged;
            source.FallbackIpAddresses.CollectionChanged += OnFallbackAddressesCollectionChanged; // 订阅回落地址列表

            foreach (var fallback in source.FallbackIpAddresses)
                fallback.PropertyChanged += OnSourceDependenciesChanged;
        }

        private void StopListeningToSource(TargetIpSource source)
        {
            source.PropertyChanged -= OnSourceDependenciesChanged;
            source.Addresses.CollectionChanged -= OnSourceDependenciesChanged;
            source.FallbackIpAddresses.CollectionChanged -= OnFallbackAddressesCollectionChanged;

            foreach (var fallback in source.FallbackIpAddresses)
                fallback.PropertyChanged -= OnSourceDependenciesChanged;
        }
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
                    StopListeningToSource(source);
            }
        }
        #endregion
    }
}