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
    public class DnsMappingTableViewModel : NotifyPropertyChangedBase, IDisposable
    {
        #region State & Properties
        private readonly Func<Guid?, bool> _requiresIpv6Lookup;

        public DnsMappingTable Model { get; }
        public ObservableCollection<DnsMappingGroupViewModel> MappingGroups { get; } = [];
        private ObservableCollection<DnsMappingGroup> _subscribedMappingGroups;
        #endregion

        #region UI Properties
        public bool IsBuiltIn => Model.IsBuiltIn;

        public string ListTypeDescription => Model.IsBuiltIn ? "(内置)" : "(用户)";

        public PackIconKind ListIconKind => Model.IsBuiltIn ? PackIconKind.ArchiveLockOutline : PackIconKind.ListBoxOutline;

        public bool RequiresIPv6 => MappingGroups.Any(vm => vm.RequiresIPv6 && vm.IsEnabled);

        public string TableName { get => Model.TableName; set => Model.TableName = value; }
        #endregion

        #region Constructor
        public DnsMappingTableViewModel(DnsMappingTable model, Func<Guid?, bool> requiresIpv6Lookup)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            _requiresIpv6Lookup = requiresIpv6Lookup ?? throw new ArgumentNullException(nameof(requiresIpv6Lookup));

            Model.PropertyChanged += OnModelPropertyChanged;

            HandleMappingGroupsChanged();
        }
        #endregion

        #region Public Methods
        public void RefreshAllRuleIPv6Status()
        {
            foreach (var groupVM in MappingGroups)
                groupVM.RefreshAllRuleIPv6Status();
        }
        #endregion

        #region Private Helpers & Event Handlers
        private void OnModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(e.PropertyName);

            switch (e.PropertyName)
            {
                case nameof(DnsMappingTable.TableName):
                    OnPropertyChanged(nameof(TableName));
                    break;

                case nameof(DnsMappingTable.IsBuiltIn):
                    OnPropertyChanged(nameof(IsBuiltIn));
                    OnPropertyChanged(nameof(ListTypeDescription));
                    OnPropertyChanged(nameof(ListIconKind));
                    break;

                case nameof(DnsMappingTable.MappingGroups):
                    HandleMappingGroupsChanged();
                    break;
            }
        }

        private void HandleMappingGroupsChanged()
        {
            if (_subscribedMappingGroups != null)
                _subscribedMappingGroups.CollectionChanged -= OnModelGroupsCollectionChanged;

            foreach (var vm in MappingGroups)
            {
                vm.PropertyChanged -= OnGroupViewModelPropertyChanged;
                vm.Dispose();
            }
            MappingGroups.Clear();

            _subscribedMappingGroups = Model.MappingGroups;
            if (_subscribedMappingGroups != null)
            {
                foreach (var groupModel in _subscribedMappingGroups)
                    AddGroupViewModel(groupModel);
                _subscribedMappingGroups.CollectionChanged += OnModelGroupsCollectionChanged;
            }

            OnPropertyChanged(nameof(RequiresIPv6));
        }

        private void OnModelGroupsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (DnsMappingGroup groupModel in e.NewItems) AddGroupViewModel(groupModel, e.NewStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    foreach (DnsMappingGroup groupModel in e.OldItems) RemoveGroupViewModel(groupModel);
                    break;
                case NotifyCollectionChangedAction.Replace:
                    foreach (DnsMappingGroup groupModel in e.OldItems) RemoveGroupViewModel(groupModel);
                    foreach (DnsMappingGroup groupModel in e.NewItems) AddGroupViewModel(groupModel, e.NewStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Move:
                    MappingGroups.Move(e.OldStartingIndex, e.NewStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Reset:
                    foreach (var vm in MappingGroups)
                    {
                        vm.PropertyChanged -= OnGroupViewModelPropertyChanged;
                        vm.Dispose();
                    }
                    MappingGroups.Clear();
                    break;
            }

            if (e.Action != NotifyCollectionChangedAction.Move)
                OnPropertyChanged(nameof(RequiresIPv6));
        }

        private void OnGroupViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DnsMappingGroupViewModel.RequiresIPv6) ||
                e.PropertyName == nameof(DnsMappingGroupViewModel.IsEnabled))
            {
                OnPropertyChanged(nameof(RequiresIPv6));
            }
        }

        private void AddGroupViewModel(DnsMappingGroup groupModel, int index = -1)
        {
            var groupVM = new DnsMappingGroupViewModel(groupModel, _requiresIpv6Lookup);
            groupVM.PropertyChanged += OnGroupViewModelPropertyChanged;
            if (index >= 0) MappingGroups.Insert(index, groupVM);
            else MappingGroups.Add(groupVM);
        }

        private void RemoveGroupViewModel(DnsMappingGroup groupModel)
        {
            var groupVM = MappingGroups.FirstOrDefault(vm => vm.Model == groupModel);
            if (groupVM != null)
            {
                groupVM.PropertyChanged -= OnGroupViewModelPropertyChanged;
                groupVM.Dispose();
                MappingGroups.Remove(groupVM);
            }
        }
        #endregion

        #region IDisposable Implementation
        public void Dispose()
        {
            Model.PropertyChanged -= OnModelPropertyChanged;
            if (_subscribedMappingGroups != null)
                _subscribedMappingGroups.CollectionChanged -= OnModelGroupsCollectionChanged;

            foreach (var groupVM in MappingGroups)
            {
                groupVM.PropertyChanged -= OnGroupViewModelPropertyChanged;
                groupVM.Dispose();
            }
            MappingGroups.Clear();
        }
        #endregion
    }
}
