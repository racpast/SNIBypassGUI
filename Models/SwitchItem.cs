using System.Windows.Media.Imaging;

namespace SNIBypassGUI.Models
{
    public class SwitchItem
    {
        public BitmapImage FaviconImage { get; set; }
        public string SwitchTitle { get; set; }
        public string[] LinksText { get; set; }
        public string ToggleButtonName { get; set; }
        public string SectionName { get; set; }
    }
}
