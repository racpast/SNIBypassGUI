using SNIBypassGUI.Interfaces;

namespace SNIBypassGUI.ViewModels.Dialogs.Fields
{
    public class InputField(string key) : IDialogField, IResultField
    {
        public string Key { get; } = key; public string Label { get; set; }
        public string Value { get; set; }
        public string HintText { get; set; }
        public (string Key, object Value) GetResult() => (Key, Value);
    }   
}
