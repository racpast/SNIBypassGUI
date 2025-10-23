using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using MaterialDesignThemes.Wpf;
using SNIBypassGUI.Common;
using SNIBypassGUI.Common.Extensions;
using SNIBypassGUI.Common.Network;
using SNIBypassGUI.Enums;
using SNIBypassGUI.Models;

namespace SNIBypassGUI.ViewModels.Items
{
    public class DnsServerViewModel : NotifyPropertyChangedBase, IDisposable
    {
        #region State & Core Properties
        public DnsServer Model { get; }

        public ObservableCollection<DnsServerViewModel> DnsServers { get; } = [];
        #endregion

        #region UI Properties
        public string PrimaryDisplayText
        {
            get => $"{(NetworkUtils.IsValidIPv6(Model.ServerAddress?.Trim()) ?
                    $"[{Model.ServerAddress?.Trim()}]" :
                    (Model.ServerAddress?.Trim()).OrDefault("未指定"))}:{(Model.ServerPort?.Trim()).OrDefault("未指定")}";
        }

        public string SecondaryDisplayText
        {
            get => Model.ProtocolType switch
            {
                DnsServerProtocol.DoH => $"{(Model.DohHostname?.Trim()).OrDefault("未指定")}/{Model.DohQueryPath?.Trim()}",
                DnsServerProtocol.SOCKS5 => $"{(NetworkUtils.IsValidIPv6(Model.Socks5ProxyAddress?.Trim()) ?
                    $"[{Model.Socks5ProxyAddress?.Trim()}]" :
                    (Model.Socks5ProxyAddress?.Trim()).OrDefault("未指定"))}:{(Model.Socks5ProxyPort?.Trim()).OrDefault("未指定")}",
                _ => null
            };
        }

        public string LimitQueryTypesDisplayText =>
            Model.LimitQueryTypes.Any()
                ? $"{string.Join(" ", Model.LimitQueryTypes.OrderBy(type => type))}"
                : null;

        public PackIconKind ListIconKind
        {
            get => Model.ProtocolType switch
            {
                DnsServerProtocol.UDP => PackIconKind.Server,
                DnsServerProtocol.TCP => PackIconKind.Server,
                DnsServerProtocol.SOCKS5 => PackIconKind.ServerNetwork,
                DnsServerProtocol.DoH => PackIconKind.ServerSecurity,
                _ => PackIconKind.HelpCircleOutline
            };
        }

        public string DomainMatchingRulesDisplayText => Model.DomainMatchingRules.Any()
            ? $"{string.Join("、", Model.DomainMatchingRules.Select(rule => rule.ToString()))}" : null;

        public string ProtocolType => Model.ProtocolType.ToString();

        public bool RequiresIPv6 => Model.RequiresIPv6;
        #endregion

        #region Constructor
        public DnsServerViewModel(DnsServer model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            Model.PropertyChanged += OnModelPropertyChanged;

            if (Model.LimitQueryTypes != null)
                Model.LimitQueryTypes.CollectionChanged += OnModelLimitQueryTypesCollectionChanged;

            if (Model.DomainMatchingRules != null)
            {
                Model.DomainMatchingRules.CollectionChanged += OnDomainMatchingRulesCollectionChanged;
                foreach (var item in Model.DomainMatchingRules)
                    item.PropertyChanged += OnDomainMatchingRulesPropertyChanged;
            }
        }
        #endregion

        #region Event Handlers & Private Helpers
        private void OnModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(e.PropertyName);

            switch (e.PropertyName)
            {
                case nameof(DnsServer.ProtocolType):
                    OnPropertyChanged(nameof(ListIconKind), nameof(SecondaryDisplayText));
                    break;

                case nameof(DnsServer.ServerAddress):
                    OnPropertyChanged(nameof(PrimaryDisplayText));
                    break;

                case nameof(DnsServer.ServerPort):
                    OnPropertyChanged(nameof(PrimaryDisplayText));
                    break;

                case nameof(DnsServer.DohHostname):
                case nameof(DnsServer.DohQueryPath):
                case nameof(DnsServer.Socks5ProxyAddress):
                case nameof(DnsServer.Socks5ProxyPort):
                    OnPropertyChanged(nameof(SecondaryDisplayText));
                    break;

                case nameof(DnsServer.LimitQueryTypes):
                    OnPropertyChanged(nameof(LimitQueryTypesDisplayText));
                    break;

                case nameof(DnsServer.DomainMatchingRules):
                    OnPropertyChanged(nameof(DomainMatchingRulesDisplayText));
                    break;

                case nameof(DnsServer.RequiresIPv6):
                    OnPropertyChanged(nameof(RequiresIPv6));
                    break;
            }
        }

        private void OnModelLimitQueryTypesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
            => OnPropertyChanged(nameof(LimitQueryTypesDisplayText));

        private void OnDomainMatchingRulesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
                foreach (AffinityRule item in e.OldItems) item.PropertyChanged -= OnDomainMatchingRulesPropertyChanged;
            if (e.NewItems != null)
                foreach (AffinityRule item in e.NewItems) item.PropertyChanged += OnDomainMatchingRulesPropertyChanged;

            OnPropertyChanged(nameof(DomainMatchingRulesDisplayText));
        }

        private void OnDomainMatchingRulesPropertyChanged(object sender, PropertyChangedEventArgs e)
            => OnPropertyChanged(nameof(DomainMatchingRulesDisplayText));
        #endregion

        #region IDisposable Implementation
        public void Dispose()
        {
            Model.PropertyChanged -= OnModelPropertyChanged;

            if (Model.LimitQueryTypes != null)
                Model.LimitQueryTypes.CollectionChanged -= OnModelLimitQueryTypesCollectionChanged;

            if (Model.DomainMatchingRules != null)
            {
                Model.DomainMatchingRules.CollectionChanged -= OnDomainMatchingRulesCollectionChanged;
                foreach (var item in Model.DomainMatchingRules)
                    item.PropertyChanged -= OnDomainMatchingRulesPropertyChanged;
            }

            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
