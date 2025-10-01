using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SNIBypassGUI.Controls;
using SNIBypassGUI.Interfaces;
using SNIBypassGUI.ViewModels.Dialogs;
using SNIBypassGUI.ViewModels.Dialogs.Fields;
using SNIBypassGUI.ViewModels.Dialogs.Items;
using SNIBypassGUI.Views.Dialogs;

namespace SNIBypassGUI.Services
{
    public class DialogService(string dialogHostName) : IDialogService
    {
        public async Task ShowInfoAsync(string title, string message)
        {
            await ShowDialogAsync(new DialogViewModel(
                title,
                [new MessageField { Text = message }],
                [new DialogButtonViewModel { Content = "确定", IsDefault = true, IsCancel = true }]
            ));
        }

        public async Task ShowInfoAsync(string title, IEnumerable<BulletedListItem> items, string header = null)
        {
            var fields = new List<IDialogField>();
            if (!string.IsNullOrEmpty(header))
                fields.Add(new MessageField { Text = header });
            fields.Add(new ItemsListField(items));

            await ShowDialogAsync(new DialogViewModel(
                title,
                fields,
                [new DialogButtonViewModel { Content = "确定", IsDefault = true, IsCancel = true }]
            ));
        }

        public async Task<bool> ShowConfirmationAsync(string title, string message, string confirmButtonText = "确定")
        {
            var (buttonResult, _) = await ShowDialogAsync(new DialogViewModel(
                title,
                [new MessageField { Text = message }],
                [
                    new DialogButtonViewModel { Content = confirmButtonText, Result = true, IsDefault = true },
                    new DialogButtonViewModel { Content = "取消", Result = false, IsCancel = true }
                ]
            ));
            return buttonResult is true;
        }

        public async Task<string> ShowTextInputAsync(string title, string label, string defaultValue = "")
        {
            const string inputKey = "text_input";
            var (buttonResult, fieldResults) = await ShowDialogAsync(new DialogViewModel(
                title,
                [new InputField(inputKey) { Label = label, Value = defaultValue }],
                [
                    new DialogButtonViewModel { Content = "确定", Result = "OK", IsDefault = true },
                    new DialogButtonViewModel { Content = "取消", Result = "CANCEL", IsCancel = true }
                ]
            ));

            return buttonResult as string == "OK" && fieldResults.TryGetValue(inputKey, out var value)
                ? value as string ?? string.Empty
                : null;
        }

        public async Task<SaveChangesResult> ShowSaveChangesDialogAsync(string title, string message)
        {
            var (buttonResult, _) = await ShowDialogAsync(new DialogViewModel(
                title,
                [new MessageField { Text = message }],
                [
                    new DialogButtonViewModel { Content = "保存", Result = SaveChangesResult.Save, IsDefault = true },
                    new DialogButtonViewModel { Content = "不保存", Result = SaveChangesResult.Discard },
                    new DialogButtonViewModel { Content = "取消", Result = SaveChangesResult.Cancel, IsCancel = true }
                ]
            ));
            return buttonResult is SaveChangesResult saveResult ? saveResult : SaveChangesResult.Cancel;
        }

        public async Task<ExportChoice> ShowExportConfirmationAsync(string configName)
        {
            var fields = new[] { new MessageField { Text = $"配置 “{configName}” 有未保存的更改，请问希望如何导出？" } };
            var buttons = new[]
            {
                new DialogButtonViewModel { Content = "保存并导出", Result = ExportChoice.SaveAndExport, IsDefault = true },
                new DialogButtonViewModel { Content = "导出原始版本", Result = ExportChoice.ExportWithoutSaving }, // 文本更清晰
                new DialogButtonViewModel { Content = "取消", Result = ExportChoice.Cancel, IsCancel = true }
            };
            var vm = new DialogViewModel("确认导出", fields, buttons);
            var (buttonResult, _) = await ShowDialogAsync(vm);
            return buttonResult is ExportChoice choice ? choice : ExportChoice.Cancel;
        }

        public async Task<(object buttonResult, Dictionary<string, object> fieldResults)> ShowDialogAsync(DialogViewModel viewModel)
        {
            var view = new DynamicDialog { DataContext = viewModel };
            object buttonResult = await DialogHost.Show(view, dialogHostName);
            var fieldResults = viewModel.Fields
                                        .OfType<IResultField>()
                                        .ToDictionary(f => f.GetResult().Key, f => f.GetResult().Value);
            return (buttonResult, fieldResults);
        }
    }
}
