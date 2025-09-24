using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace SNIBypassGUI.Common.Extensions
{
    /// <summary>
    /// Represents a collection of items that provides additional functionality for bulk operations 
    /// and suppresses collection change notifications during batch updates.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    public class RangeObservableCollection<T> : ObservableCollection<T>
    {
        private bool _suppressNotification;

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            // Suppresses collection change notifications if _suppressNotification is true.
            if (!_suppressNotification) base.OnCollectionChanged(e);
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            // Suppresses property change notifications if _suppressNotification is true.
            if (!_suppressNotification) base.OnPropertyChanged(e);
        }

        /// <summary>
        /// Adds a collection of items to the current collection, triggering a single Reset notification.
        /// </summary>
        /// <param name="items">The collection of items to add.</param>
        /// <exception cref="ArgumentNullException">Thrown when the items collection is null.</exception>
        public void AddRange(IEnumerable<T> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));

            // Suppresses notifications while adding the items to the collection
            _suppressNotification = true;
            foreach (var item in items) Items.Add(item);
            _suppressNotification = false;

            // Triggers a Reset notification to update the UI or other subscribers
            RaiseReset();
        }

        /// <summary>
        /// Replaces the current items with a new collection, triggering a single Reset notification.
        /// </summary>
        /// <param name="items">The collection of items to replace the current collection with.</param>
        /// <exception cref="ArgumentNullException">Thrown when the items collection is null.</exception>
        public void ReplaceAll(IEnumerable<T> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));

            // Suppresses notifications while clearing and adding new items to the collection
            _suppressNotification = true;
            Items.Clear();
            foreach (var item in items) Items.Add(item);
            _suppressNotification = false;

            // Triggers a Reset notification to update the UI or other subscribers
            RaiseReset();
        }

        /// <summary>
        /// Triggers a Reset notification manually to notify any listeners that the collection has been reset.
        /// </summary>
        public void RaiseReset()
        {
            // Notify that the collection count and the indexed items have changed
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));

            // Notify that the collection has been reset
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
