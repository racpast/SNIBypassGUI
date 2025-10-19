using System.Collections.Generic;
using SNIBypassGUI.Common;
using SNIBypassGUI.Interfaces;

namespace SNIBypassGUI.ViewModels.Dialogs
{
    /// <summary>
    /// Represents the data model for a <see cref="DynamicDialog"/>.
    /// It encapsulates all the necessary information to dynamically construct a dialog,
    /// including its title, content fields, and action buttons.
    /// </summary>
    /// <param name="title">The text to display in the dialog's title bar.</param>
    /// <param name="fields">A collection of <see cref="IDialogField"/> objects that define the main content of the dialog.</param>
    /// <param name="buttons">A collection of <see cref="DialogButtonViewModel"/> objects for the dialog's action buttons.</param>
    public class DialogViewModel(string title, IEnumerable<IDialogField> fields, IEnumerable<DialogButtonViewModel> buttons) : NotifyPropertyChangedBase
    {
        public string Title { get; } = title;

        public IEnumerable<IDialogField> Fields { get; } = fields;

        public IEnumerable<DialogButtonViewModel> Buttons { get; } = buttons;
    }
}