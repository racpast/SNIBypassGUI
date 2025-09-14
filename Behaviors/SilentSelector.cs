using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using SNIBypassGUI.Commands;
using static SNIBypassGUI.Utils.LogManager;

namespace SNIBypassGUI.Behaviors
{
    /// <summary>
    /// Provides a selection helper that prevents event loops when synchronizing selection between view and view model.
    /// </summary>
    /// <typeparam name="T">The type of the selectable item.</typeparam>
    public sealed class SilentSelector<T> : INotifyPropertyChanged where T : class
    {
        /// <summary>
        /// Indicates whether the selection change is triggered programmatically.
        /// </summary>
        private bool _isProgrammaticChange;

        /// <summary>
        /// Stores the current selected item.
        /// </summary>
        private T _selectedItem;

        /// <summary>
        /// Represents the asynchronous callback invoked when the selection changes due to user interaction.
        /// </summary>
        private readonly Func<T, T, Task> _onUserSelectionChanged;

        /// <summary>
        /// Gets or sets the current selected item.
        /// </summary>
        public T SelectedItem
        {
            get => _selectedItem;
            set => SetProperty(ref _selectedItem, value);
        }

        /// <summary>
        /// Gets the command to handle the <c>SelectionChanged</c> event from the UI.
        /// </summary>
        public ICommand SelectionChangedCommand { get; }

        /// <inheritdoc/>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="SilentSelector{T}"/> class.
        /// </summary>
        /// <param name="onUserSelectionChanged">
        /// A callback that is invoked when the user changes the selection.
        /// The parameters are <c>(newItem, oldItem)</c>.
        /// </param>
        public SilentSelector(Func<T, T, Task> onUserSelectionChanged)
        {
            _onUserSelectionChanged = onUserSelectionChanged ??
                throw new ArgumentNullException(nameof(onUserSelectionChanged));

            SelectionChangedCommand = new AsyncCommand<SelectionChangedEventArgs>(
                execute: HandleUserSelectionChangedAsync,
                canExecute: _ => true
            );
        }

        /// <summary>
        /// Sets the selected item programmatically without triggering the user selection callback.
        /// </summary>
        /// <param name="item">The item to set as selected.</param>
        public void SetItemSilently(T item)
        {
            try
            {
                _isProgrammaticChange = true;
                SelectedItem = item;
            }
            finally
            {
                _isProgrammaticChange = false;
            }
        }

        /// <summary>
        /// Handles the <c>SelectionChanged</c> event raised by the UI.
        /// </summary>
        /// <param name="e">The selection changed event arguments.</param>
        private async Task HandleUserSelectionChangedAsync(SelectionChangedEventArgs e)
        {
            if (_isProgrammaticChange) return;

            var newItem = e?.AddedItems?.OfType<T>().FirstOrDefault();
            var oldItem = e?.RemovedItems?.OfType<T>().FirstOrDefault();

            if (newItem != oldItem || (newItem == null && oldItem != null))
            {
                try
                {
                    await _onUserSelectionChanged(newItem, oldItem);
                }
                catch (Exception ex)
                {
                    WriteLog("发生异常。", LogLevel.Warning, ex);
                }
            }
        }

        /// <summary>
        /// Updates the specified property value and raises <see cref="PropertyChanged"/> if it changes.
        /// </summary>
        /// <typeparam name="TProp">The type of the property.</typeparam>
        /// <param name="field">The backing field.</param>
        /// <param name="value">The new value to set.</param>
        /// <param name="propertyName">The name of the property.</param>
        /// <returns><c>true</c> if the property value changed; otherwise <c>false</c>.</returns>
        private bool SetProperty<TProp>(ref TProp field, TProp value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<TProp>.Default.Equals(field, value)) return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// Raises the <see cref="PropertyChanged"/> event for the specified property.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
