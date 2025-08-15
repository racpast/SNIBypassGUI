using System.Windows;
using System.Windows.Media;

namespace SNIBypassGUI.Controls.Assist
{
    public static class ChipAssist
    {
        public static readonly DependencyProperty DeleteButtonFillBrushProperty =
            DependencyProperty.RegisterAttached(
                "DeleteButtonFillBrush",
                typeof(Brush),
                typeof(ChipAssist),
                new PropertyMetadata(Brushes.Transparent));

        public static void SetDeleteButtonFillBrush(DependencyObject element, Brush value) =>
            element.SetValue(DeleteButtonFillBrushProperty, value);

        public static Brush GetDeleteButtonFillBrush(DependencyObject element) =>
            (Brush)element.GetValue(DeleteButtonFillBrushProperty);

        public static readonly DependencyProperty DeleteButtonMouseOverBorderBrushProperty =
            DependencyProperty.RegisterAttached(
                "DeleteButtonMouseOverBorderBrush",
                typeof(Brush),
                typeof(ChipAssist),
                new PropertyMetadata(Brushes.Transparent));

        public static void SetDeleteButtonMouseOverBorderBrush(DependencyObject element, Brush value) =>
            element.SetValue(DeleteButtonMouseOverBorderBrushProperty, value);

        public static Brush GetDeleteButtonMouseOverBorderBrush(DependencyObject element) =>
            (Brush)element.GetValue(DeleteButtonMouseOverBorderBrushProperty);
    }
}
