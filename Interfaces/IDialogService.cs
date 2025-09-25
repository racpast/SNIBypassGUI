using System.Collections.Generic;
using System.Threading.Tasks;
using SNIBypassGUI.ViewModels.Dialogs;
using SNIBypassGUI.ViewModels.Dialogs.Items;

namespace SNIBypassGUI.Interfaces
{
    public enum SaveChangesResult
    {
        Save,
        Discard,
        Cancel
    }
    public enum ExportChoice
    {
        SaveAndExport,
        ExportWithoutSaving,
        Cancel
    }
    public interface IDialogService
    {
        /// <summary>
        /// 显示一条简单的信息提示框。
        /// </summary>
        /// <param name="title">对话框的标题。</param>
        /// <param name="message">要显示的文本信息。</param>
        /// <returns>一个表示异步操作的任务。</returns>
        Task ShowInfoAsync(string title, string message);

        /// <summary>
        /// 显示一个列表信息提示框。
        /// </summary>
        /// <param name="title">对话框的标题。</param>
        /// <param name="items">要显示的项目列表。</param>
        /// <param name="header">显示在列表上方的额外标题文本。</param>
        /// <returns>一个表示异步操作的任务。</returns>
        Task ShowInfoAsync(string title, IEnumerable<BulletedListItem> items, string header = null);

        /// <summary>
        /// 显示一个带确认和取消按钮的对话框，用于请求用户确认操作。
        /// </summary>
        /// <param name="title">对话框的标题。</param>
        /// <param name="message">要显示的确认信息。</param>
        /// <param name="confirmButtonText">（可选）“确认”按钮上显示的文本，默认为“确定”。</param>
        /// <returns>一个任务，如果用户点击确认按钮，则其结果为 true；否则为 false。</returns>
        Task<bool> ShowConfirmationAsync(string title, string message, string confirmButtonText = "确定");

        /// <summary>
        /// 显示一个带文本输入框的对话框。
        /// </summary>
        /// <param name="title">对话框的标题。</param>
        /// <param name="label">输入框上方的标签文本。</param>
        /// <param name="defaultValue">（可选）输入框中预先填写的默认值。</param>
        /// <returns>一个任务，如果用户点击确认，则其结果为输入的文本；如果用户点击取消，则为 null。</returns>
        Task<string> ShowTextInputAsync(string title, string label, string defaultValue = "");

        /// <summary>
        /// 显示一个保存、不保存或取消的三选一对话框。
        /// </summary>
        /// <param name="title">对话框的标题。</param>
        /// <param name="message">要显示的提示信息。</param>
        /// <returns>一个任务，其结果为 SaveChangesResult 枚举值，代表用户的选择。</returns>
        Task<SaveChangesResult> ShowSaveChangesDialogAsync(string title, string message);

        /// <summary>
        /// 当有未保存的更改时，显示一个用于导出的特定确认对话框。
        /// </summary>
        Task<ExportChoice> ShowExportConfirmationAsync(string configName);

        /// <summary>
        /// 显示一个自定义对话框。
        /// </summary>
        /// <param name="viewModel">手动配置好的对话框 ViewModel，包括显示的字段和按钮。</param>
        /// <returns>
        /// 一个任务，其结果为一个元组：
        /// <list type="bullet">
        /// <item>
        /// <description><c>buttonResult</c>：用户点击的按钮所绑定的结果对象。</description>
        /// </item>
        /// <item>
        /// <description><c>fieldResults</c>：对话框中各输入字段的结果字典。</description>
        /// </item>
        /// </list>
        /// </returns>
        Task<(object buttonResult, Dictionary<string, object> fieldResults)> ShowDialogAsync(DialogViewModel viewModel);
    }
}
