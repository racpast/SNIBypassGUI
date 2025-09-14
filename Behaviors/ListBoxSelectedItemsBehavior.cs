using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace SNIBypassGUI.Behaviors
{
    public static class ListBoxSelectedItemsBehavior
    {
        public static readonly DependencyProperty SynchronizedSelectedItemsProperty =
            DependencyProperty.RegisterAttached(
                "SynchronizedSelectedItems",
                typeof(IList),
                typeof(ListBoxSelectedItemsBehavior),
                new PropertyMetadata(null, OnSynchronizedSelectedItemsChanged));

        public static void SetSynchronizedSelectedItems(DependencyObject element, IList value) => element.SetValue(SynchronizedSelectedItemsProperty, value);

        public static IList GetSynchronizedSelectedItems(DependencyObject element)
        {
            return (IList)element.GetValue(SynchronizedSelectedItemsProperty);
        }

        private static void OnSynchronizedSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ListBox listBox)
            {
                listBox.SelectionChanged -= ListBox_SelectionChanged;

                if (e.NewValue is IList newList)
                {
                    // 根据 ViewModel 的集合设置选中项
                    listBox.SelectedItems.Clear();
                    foreach (var item in newList) listBox.SelectedItems.Add(item);

                    // 注册 SelectionChanged 事件
                    listBox.SelectionChanged += ListBox_SelectionChanged;
                }
            }
        }

        private static void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListBox listBox)
            {
                var synchronizedList = GetSynchronizedSelectedItems(listBox);
                if (synchronizedList == null) return;

                foreach (var item in e.RemovedItems)
                {
                    if (synchronizedList.Contains(item))
                        synchronizedList.Remove(item);
                }

                foreach (var item in e.AddedItems)
                {
                    if (!synchronizedList.Contains(item))
                        synchronizedList.Add(item);
                }
            }
        }
    }
}
