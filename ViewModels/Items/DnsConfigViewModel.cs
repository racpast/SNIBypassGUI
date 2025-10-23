using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using MaterialDesignThemes.Wpf;
using SNIBypassGUI.Common;
using SNIBypassGUI.Models;

namespace SNIBypassGUI.ViewModels.Items
{
    public class DnsConfigViewModel : NotifyPropertyChangedBase, IDisposable
    {
        #region State & Core Properties
        public DnsConfig Model { get; }

        public ObservableCollection<DnsServerViewModel> DnsServers { get; } = [];
        private ObservableCollection<DnsServer> _subscribedDnsServers;
        #endregion

        #region UI Properties
        public string ConfigName
        {
            get => Model.ConfigName;
            set => Model.ConfigName = value;
        }

        public bool IsBuiltIn => Model.IsBuiltIn;

        public string ListTypeDescription => Model.IsBuiltIn ? "(内置)" : "(用户)";

        public PackIconKind ListIconKind => Model.IsBuiltIn ? PackIconKind.ArchiveLockOutline : PackIconKind.ListBoxOutline;

        public bool RequiresIPv6 => DnsServers.Any(vm => vm.Model.RequiresIPv6);
        #endregion

        #region Public Methods
        public void RefreshIPv6Status() =>
            OnPropertyChanged(nameof(RequiresIPv6));
        #endregion

        #region Constructor
        public DnsConfigViewModel(DnsConfig model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));

            Model.PropertyChanged += OnModelPropertyChanged;

            HandleDnsServersChanged();
        }
        #endregion

        #region Event Handlers & Private Helpers
        private void OnModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(e.PropertyName);

            switch (e.PropertyName)
            {
                case nameof(DnsConfig.ConfigName):
                    OnPropertyChanged(nameof(ConfigName));
                    break;

                case nameof(DnsConfig.IsBuiltIn):
                    OnPropertyChanged(nameof(ListTypeDescription), nameof(ListIconKind));
                    break;

                case nameof(DnsConfig.DnsServers):
                    HandleDnsServersChanged();
                    break;
            }
        }

        private void HandleDnsServersChanged()
        {
            if (_subscribedDnsServers != null)
                _subscribedDnsServers.CollectionChanged -= OnModelDnsServersCollectionChanged;
            foreach (var vm in DnsServers)
            {
                vm.PropertyChanged -= OnDnsServerViewModelPropertyChanged;
                vm.Dispose();
            }
            DnsServers.Clear();

            _subscribedDnsServers = Model.DnsServers;
            if (_subscribedDnsServers != null)
            {
                foreach (var serverModel in _subscribedDnsServers)
                    AddServerViewModel(serverModel);
                _subscribedDnsServers.CollectionChanged += OnModelDnsServersCollectionChanged;
            }

            OnPropertyChanged(nameof(RequiresIPv6));
        }

        private void OnModelDnsServersCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (e.NewItems != null)
                        foreach (DnsServer serverModel in e.NewItems)
                            AddServerViewModel(serverModel, e.NewStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    if (e.OldItems != null)
                        foreach (DnsServer serverModel in e.OldItems)
                            RemoveServerViewModel(serverModel);
                    break;
                case NotifyCollectionChangedAction.Replace:
                    if (e.OldItems != null)
                        foreach (DnsServer serverModel in e.OldItems)
                            RemoveServerViewModel(serverModel);
                    if (e.NewItems != null)
                        foreach (DnsServer serverModel in e.NewItems)
                            AddServerViewModel(serverModel, e.NewStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Move:
                    DnsServers.Move(e.OldStartingIndex, e.NewStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Reset:
                    HandleDnsServersChanged();
                    return;
            }

            if (e.Action != NotifyCollectionChangedAction.Move)
                OnPropertyChanged(nameof(RequiresIPv6));
        }

        private void OnDnsServerViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DnsServerViewModel.RequiresIPv6))
                OnPropertyChanged(nameof(RequiresIPv6));
        }

        private void AddServerViewModel(DnsServer serverModel, int index = -1)
        {
            var serverVM = new DnsServerViewModel(serverModel);
            serverVM.PropertyChanged += OnDnsServerViewModelPropertyChanged;
            if (index >= 0 && index <= DnsServers.Count)
                DnsServers.Insert(index, serverVM);
            else
                DnsServers.Add(serverVM);
        }

        private void RemoveServerViewModel(DnsServer serverModel)
        {
            var serverVM = DnsServers.FirstOrDefault(vm => vm.Model == serverModel);
            if (serverVM != null)
            {
                serverVM.PropertyChanged -= OnDnsServerViewModelPropertyChanged;
                serverVM.Dispose();
                DnsServers.Remove(serverVM);
            }
        }
        #endregion

        #region IDisposable Implementation
        public void Dispose()
        {
            Model.PropertyChanged -= OnModelPropertyChanged;
            if (_subscribedDnsServers != null)
                _subscribedDnsServers.CollectionChanged -= OnModelDnsServersCollectionChanged;

            foreach (var vm in DnsServers)
            {
                vm.PropertyChanged -= OnDnsServerViewModelPropertyChanged;
                vm.Dispose();
            }
            DnsServers.Clear();
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
