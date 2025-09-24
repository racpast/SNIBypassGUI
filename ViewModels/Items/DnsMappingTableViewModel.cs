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
        public DnsMappingTable Model { get; }

        public ObservableCollection<DnsMappingGroupViewModel> MappingGroups { get; } = [];

        private readonly Func<Guid?, ResolverConfig> _resolverLookup;
        #endregion

        #region UI Properties
        public bool IsBuiltIn => Model.IsBuiltIn;

        public string ListTypeDescription => Model.IsBuiltIn ? "(内置)" : "(用户)";

        public PackIconKind ListIconKind => Model.IsBuiltIn ? PackIconKind.ArchiveLockOutline : PackIconKind.ListBoxOutline;

        public string TableName { get => Model.TableName; set => Model.TableName = value; }
        #endregion

        #region Constructor
        public DnsMappingTableViewModel(DnsMappingTable model, Func<Guid?, ResolverConfig> resolverLookup)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            _resolverLookup = resolverLookup ?? throw new ArgumentNullException(nameof(resolverLookup));

            Model.PropertyChanged += OnModelPropertyChanged;
            Model.MappingGroups.CollectionChanged += OnModelGroupsCollectionChanged;

            foreach (var groupModel in Model.MappingGroups)
                AddGroupViewModel(groupModel);
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
            }
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
                    foreach (var vm in MappingGroups) vm.Dispose();
                    MappingGroups.Clear();
                    break;
            }
        }

        private void AddGroupViewModel(DnsMappingGroup groupModel, int index = -1)
        {
            var groupVM = new DnsMappingGroupViewModel(groupModel, _resolverLookup);
            if (index >= 0) MappingGroups.Insert(index, groupVM);
            else MappingGroups.Add(groupVM);
        }

        private void RemoveGroupViewModel(DnsMappingGroup groupModel)
        {
            var groupVM = MappingGroups.FirstOrDefault(vm => vm.Model == groupModel);
            if (groupVM != null)
            {
                groupVM.Dispose();
                MappingGroups.Remove(groupVM);
            }
        }
        #endregion

        #region IDisposable Implementation
        public void Dispose()
        {
            Model.PropertyChanged -= OnModelPropertyChanged;
            Model.MappingGroups.CollectionChanged -= OnModelGroupsCollectionChanged;

            foreach (var groupVM in MappingGroups) groupVM.Dispose();
            MappingGroups.Clear();
        }
        #endregion
    }
}