using System.Collections.Generic;
using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Common;

namespace SNIBypassGUI.ViewModels.Dialogs
{
    public class DialogViewModel(string title, IEnumerable<IDialogField> fields, IEnumerable<DialogButtonViewModel> buttons) : NotifyPropertyChangedBase
    {
        public string Title { get; } = title;

        public IEnumerable<IDialogField> Fields { get; } = fields;

        public IEnumerable<DialogButtonViewModel> Buttons { get; } = buttons;
    }
}