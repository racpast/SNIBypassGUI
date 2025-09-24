using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Media.Imaging;
using SNIBypassGUI.Common;
using SNIBypassGUI.Common.IO;
using SNIBypassGUI.Models;

namespace SNIBypassGUI.ViewModels.Items
{
    public class DnsMappingGroupViewModel : NotifyPropertyChangedBase, IDisposable
    {
        #region State & Core Properties
        private bool _isExpanded;

        public DnsMappingGroup Model { get; }

        public ObservableCollection<DnsMappingRuleViewModel> MappingRules { get; } = [];

        private readonly Func<Guid?, ResolverConfig> _resolverLookup;
        #endregion

        #region UI Properties
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        public BitmapImage GroupIcon => FileUtils.Base64ToBitmapImage(Model.GroupIconBase64);

        public bool HasGroupIcon => !string.IsNullOrEmpty(Model.GroupIconBase64);

        public string DisplayText => $"{Model.GroupName} ({Model.MappingRules?.Count ?? 0})";

        public bool RequiresIPv6 => MappingRules.Any(vm => vm.RequiresIPv6);

        public bool IsEnabled { get => Model.IsEnabled; set => Model.IsEnabled = value; }

        public string GroupName { get => Model.GroupName; set => Model.GroupName = value; }
        #endregion

        #region Constructor
        public DnsMappingGroupViewModel(DnsMappingGroup model, Func<Guid?, ResolverConfig> resolverLookup)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            _resolverLookup = resolverLookup ?? throw new ArgumentNullException(nameof(resolverLookup));

            Model.PropertyChanged += OnModelPropertyChanged;
            Model.MappingRules.CollectionChanged += OnModelRulesCollectionChanged;

            foreach (var ruleModel in Model.MappingRules)
                AddRuleViewModel(ruleModel);
        }
        #endregion

        #region Public Methods
        public void RefreshAllRuleIPv6Status()
        {
            foreach (var ruleVM in MappingRules)
                ruleVM.RefreshIPv6Status();
        }
        #endregion

        #region Event Handlers & Private Helpers
        private void OnModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(DnsMappingGroup.GroupName):
                    OnPropertyChanged(nameof(GroupName));
                    OnPropertyChanged(nameof(DisplayText));
                    break;

                case nameof(DnsMappingGroup.GroupIconBase64):
                    OnPropertyChanged(nameof(GroupIcon));
                    OnPropertyChanged(nameof(HasGroupIcon));
                    break;

                case nameof(DnsMappingGroup.IsEnabled):
                    OnPropertyChanged(nameof(IsEnabled));
                    break;
            }
        }

        private void OnModelRulesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (DnsMappingRule ruleModel in e.NewItems)
                        AddRuleViewModel(ruleModel, e.NewStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    foreach (DnsMappingRule ruleModel in e.OldItems)
                        RemoveRuleViewModel(ruleModel);
                    break;
                case NotifyCollectionChangedAction.Replace:
                    foreach (DnsMappingRule ruleModel in e.OldItems) RemoveRuleViewModel(ruleModel);
                    foreach (DnsMappingRule ruleModel in e.NewItems) AddRuleViewModel(ruleModel, e.NewStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Move:
                    MappingRules.Move(e.OldStartingIndex, e.NewStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Reset:
                    foreach (var vm in MappingRules) vm.Dispose();
                    MappingRules.Clear();
                    break;
            }
            OnPropertyChanged(nameof(DisplayText));
            OnPropertyChanged(nameof(RequiresIPv6));
        }

        private void AddRuleViewModel(DnsMappingRule ruleModel, int index = -1)
        {
            var ruleVM = new DnsMappingRuleViewModel(ruleModel, this, _resolverLookup);
            ruleVM.PropertyChanged += OnRuleViewModelPropertyChanged;
            if (index >= 0) MappingRules.Insert(index, ruleVM);
            else MappingRules.Add(ruleVM);
        }

        private void RemoveRuleViewModel(DnsMappingRule ruleModel)
        {
            var ruleVM = MappingRules.FirstOrDefault(vm => vm.Model == ruleModel);
            if (ruleVM != null)
            {
                ruleVM.PropertyChanged -= OnRuleViewModelPropertyChanged;
                ruleVM.Dispose();
                MappingRules.Remove(ruleVM);
            }
        }

        private void OnRuleViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DnsMappingRuleViewModel.RequiresIPv6))
                OnPropertyChanged(nameof(RequiresIPv6));
        }
        #endregion

        #region IDisposable Implementation
        public void Dispose()
        {
            Model.PropertyChanged -= OnModelPropertyChanged;
            Model.MappingRules.CollectionChanged -= OnModelRulesCollectionChanged;
            foreach (var ruleVM in MappingRules)
            {
                ruleVM.PropertyChanged -= OnRuleViewModelPropertyChanged;
                ruleVM.Dispose();
            }
            MappingRules.Clear();
        }
        #endregion
    }
}