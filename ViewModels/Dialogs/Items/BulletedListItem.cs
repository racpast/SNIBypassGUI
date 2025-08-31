using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using SNIBypassGUI.Enums;

namespace SNIBypassGUI.ViewModels.Dialogs.Items
{
    public class BulletedListItem
    {
        public PackIconKind? IconKind { get; set; }
        public DisplayType Display => IconKind.HasValue ? DisplayType.Icon : DisplayType.Bullet;
        public Brush IconBrush { get; set; }
        public string Text { get; set; }
        public List<BulletedListItem> Children { get; } = [];
        public bool HasChildren => Children.Any();
    }
}
