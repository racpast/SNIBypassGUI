using System.Collections;
using SNIBypassGUI.Interfaces;

namespace SNIBypassGUI.ViewModels.Dialogs.Fields
{
    public class ItemsListField(IEnumerable itemsSource) : IDialogField
    {
        public IEnumerable ItemsSource { get; } = itemsSource;
    }
}
