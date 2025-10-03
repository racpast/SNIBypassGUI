using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SNIBypassGUI.Controls.Assist
{
    public static class DataGridAssist
    {
        // Style? Pfft, forget about it. Setting Focusable="False" is like telling Ross and Rachel 
        // to "just stay apart"—does it work? Nope. The moment the DialogHost closes, they reunite, 
        // black focus border and all. Time to bring out this big, tough attached property 
        // to play the chaperone.
        public static readonly DependencyProperty DisableCellFocusProperty =
            DependencyProperty.RegisterAttached(
                "DisableCellFocus",
                typeof(bool),
                typeof(DataGridAssist),
                new PropertyMetadata(false, OnDisableCellFocusChanged));

        public static bool GetDisableCellFocus(DependencyObject obj) =>
            (bool)obj.GetValue(DisableCellFocusProperty);

        public static void SetDisableCellFocus(DependencyObject obj, bool value) =>
            obj.SetValue(DisableCellFocusProperty, value);

        private static void OnDisableCellFocusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DataGrid grid)
                return;

            if ((bool)e.NewValue)
            {
                grid.SelectionChanged += OnSelectionChanged;
                grid.PreviewMouseDown += OnPreviewMouseDown;
                grid.PreviewGotKeyboardFocus += OnPreviewGotKeyboardFocus;
                grid.IsTabStop = false;
            }
            else
            {
                grid.SelectionChanged -= OnSelectionChanged;
                grid.PreviewMouseDown -= OnPreviewMouseDown;
                grid.PreviewGotKeyboardFocus -= OnPreviewGotKeyboardFocus;
                grid.IsTabStop = true;
            }
        }

        private static void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is DataGrid grid)
            {
                grid.SelectedCells.Clear();
                grid.SelectedItem = null;
            }
        }

        private static void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGrid grid)
            {
                // 防止鼠标点击导致选中行
                if (grid.SelectionUnit != DataGridSelectionUnit.FullRow)
                    grid.SelectedCells.Clear();
                grid.SelectedItem = null;
                Keyboard.ClearFocus();
            }
        }

        private static void OnPreviewGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is not DataGrid grid) return;

            // 检查焦点元素是否是 DataGrid 内的子元素
            if (e.NewFocus is FrameworkElement fe && IsChildOf(fe, grid))
            {
                // 如果是 DataGrid 内的元素，则阻止焦点
                e.Handled = true;
                Keyboard.ClearFocus();
            }
        }

        private static bool IsChildOf(FrameworkElement child, FrameworkElement parent)
        {
            var current = child;
            while (current != null)
            {
                if (current == parent) return true;
                current = VisualTreeHelper.GetParent(current) as FrameworkElement;
            }
            return false;
        }
    }
}