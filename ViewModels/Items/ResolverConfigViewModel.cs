using System;
using System.ComponentModel;
using MaterialDesignThemes.Wpf;
using SNIBypassGUI.Common;
using SNIBypassGUI.Models;

namespace SNIBypassGUI.ViewModels.Items
{
    public class ResolverConfigViewModel : NotifyPropertyChangedBase, IDisposable
    {
        #region State & Core Properties
        public ResolverConfig Model { get; }
        #endregion

        #region UI Properties
        public bool IsBuiltIn => Model.IsBuiltIn;

        public PackIconKind ListIconKind => Model.IsBuiltIn ? PackIconKind.ArchiveLockOutline : PackIconKind.SearchWeb;

        public string ConfigName { get => Model.ConfigName; set => Model.ConfigName = value; }

        public string ListTypeDescription => Model.IsBuiltIn ? "(内置)" : "(用户)";

        public bool RequiresIPv6 => Model.RequiresIPv6;
        #endregion

        #region Constructor
        public ResolverConfigViewModel(ResolverConfig model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            Model.PropertyChanged += OnModelPropertyChanged;
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

            if (e.PropertyName == nameof(Model.IsBuiltIn))
            {
                OnPropertyChanged(nameof(ListIconKind));
                OnPropertyChanged(nameof(ListTypeDescription));
            }
        }
        #endregion

        #region IDisposable Implementation
        public void Dispose() =>
            Model.PropertyChanged -= OnModelPropertyChanged;
        #endregion
    }
}
