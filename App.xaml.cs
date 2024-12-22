using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SNIBypassGUI
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        private void Application_SessionEnding(object sender, SessionEndingCancelEventArgs e)
        {
            // 你们不要直接关机啦，SessionEnding 也有不行的时候 (╥﹏╥)
            var _mainWindow = Current.Windows
            .Cast<Window>()
            .FirstOrDefault(window => window is MainWindow) as MainWindow;
            _mainWindow.ExitBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }
    }
}
