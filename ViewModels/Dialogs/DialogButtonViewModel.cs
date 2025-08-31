using SNIBypassGUI.Models;

namespace SNIBypassGUI.ViewModels.Dialogs
{
    public class DialogButtonViewModel : NotifyPropertyChangedBase
    {
        public string Content { get; set; }
        public object Result { get; set; }
        public bool IsDefault { get; set; }
        public bool IsCancel { get; set; }
    }
}
