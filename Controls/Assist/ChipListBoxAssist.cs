using System.Windows;
using System.Windows.Media;

namespace SNIBypassGUI.Controls.Assist
{
    public static class ChipListBoxAssist
    {
        public static readonly DependencyProperty UnselectedChipBorderBrushProperty =
            DependencyProperty.RegisterAttached(
                "UnselectedChipBorderBrush",
                typeof(Brush),
                typeof(ChipListBoxAssist),
                new PropertyMetadata(Brushes.Transparent));

        public static void SetUnselectedChipBorderBrush(DependencyObject element, Brush value) =>
            element.SetValue(UnselectedChipBorderBrushProperty, value);

        public static Brush GetUnselectedChipBorderBrush(DependencyObject element) =>
            (Brush)element.GetValue(UnselectedChipBorderBrushProperty);


        public static readonly DependencyProperty UnselectedChipBorderThicknessProperty =
            DependencyProperty.RegisterAttached(
                "UnselectedChipBorderThickness",
                typeof(Thickness),
                typeof(ChipListBoxAssist),
                new PropertyMetadata(new Thickness(1)));

        public static void SetUnselectedChipBorderThickness(DependencyObject element, Thickness value) =>
            element.SetValue(UnselectedChipBorderThicknessProperty, value);

        public static Thickness GetUnselectedChipBorderThickness(DependencyObject element) =>
            (Thickness)element.GetValue(UnselectedChipBorderThicknessProperty);


        public static readonly DependencyProperty UnselectedChipForegroundProperty =
            DependencyProperty.RegisterAttached(
                "UnselectedChipForeground",
                typeof(Brush),
                typeof(ChipListBoxAssist),
                new PropertyMetadata(Brushes.Gray));

        public static void SetUnselectedChipForeground(DependencyObject element, Brush value) =>
            element.SetValue(UnselectedChipForegroundProperty, value);

        public static Brush GetUnselectedChipForeground(DependencyObject element) =>
            (Brush)element.GetValue(UnselectedChipForegroundProperty);


        public static readonly DependencyProperty UnselectedChipOpacityProperty =
            DependencyProperty.RegisterAttached(
                "UnselectedChipOpacity",
                typeof(double),
                typeof(ChipListBoxAssist),
                new PropertyMetadata(0.65));

        public static void SetUnselectedChipOpacity(DependencyObject element, double value) =>
            element.SetValue(UnselectedChipOpacityProperty, value);

        public static double GetUnselectedChipOpacity(DependencyObject element) =>
            (double)element.GetValue(UnselectedChipOpacityProperty);
    }
}
