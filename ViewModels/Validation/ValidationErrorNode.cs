using System.Collections.Generic;
using System.Linq;
using MaterialDesignThemes.Wpf;
using SNIBypassGUI.Enums;
using SNIBypassGUI.ViewModels.Dialogs.Items;

namespace SNIBypassGUI.ViewModels.Validation
{
    public class ValidationErrorNode
    {
        #region Fields
        private int _depth = 0;
        #endregion

        #region Properties
        public string Message { get; set; }
        public List<ValidationErrorNode> Children { get; } = [];
        public bool IsGroup => Children.Any();
        public int Depth
        {
            get => _depth;
            set
            {
                _depth = value;
                // 递归设置子节点深度
                foreach (var child in Children)
                    child.Depth = value + 1;
            }
        }
        public DisplayType Display
        {
            get
            {
                if (Depth == 0) return DisplayType.Icon; // 根节点总是显示图标
                return IsGroup ? DisplayType.Icon : DisplayType.Bullet;
            }
        }
        #endregion

        #region Methods
        public void AddChild(ValidationErrorNode child)
        {
            child.Depth = Depth + 1;
            Children.Add(child);
        }

        public void AddChildren(IEnumerable<ValidationErrorNode> children)
        {
            foreach (var child in children) AddChild(child);
        }

        public BulletedListItem ToBulletedListItem()
        {
            var item = new BulletedListItem
            {
                Text = Message,
                IconKind = Display == DisplayType.Icon ?
                    PackIconKind.AlertCircleOutline :
                    null
            };

            foreach (var child in Children)
                item.Children.Add(child.ToBulletedListItem());

            return item;
        }
        #endregion
    }
}
