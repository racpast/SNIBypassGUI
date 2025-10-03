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

        // Because WPF refuses to wrap Chip content automatically,
        // we gotta pull some tricks to get TextTrimming working.
        // Thankfully, an attached property saves the day.
        public static readonly DependencyProperty TextTrimmingProperty =
        DependencyProperty.RegisterAttached(
            "TextTrimming",
            typeof(TextTrimming),
            typeof(ChipAssist),
            new PropertyMetadata(TextTrimming.CharacterEllipsis));

        public static TextTrimming GetTextTrimming(DependencyObject obj) =>
            (TextTrimming)obj.GetValue(TextTrimmingProperty);

        public static void SetTextTrimming(DependencyObject obj, TextTrimming value) =>
            obj.SetValue(TextTrimmingProperty, value);
    }
}