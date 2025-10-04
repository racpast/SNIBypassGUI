using System.Windows;
using System.Windows.Controls;
using SNIBypassGUI.ViewModels.Items;

namespace SNIBypassGUI.Selectors
{
    public class DnsMappingTreeItemStyleSelector : StyleSelector
    {
        public Style GroupStyle { get; set; }
        public Style RuleStyle { get; set; }

        public override Style SelectStyle(object item, DependencyObject container)
        {
            if (item is DnsMappingGroupViewModel) return GroupStyle;
            if (item is DnsMappingRuleViewModel) return RuleStyle;
            return base.SelectStyle(item, container);
        }
    }
}
