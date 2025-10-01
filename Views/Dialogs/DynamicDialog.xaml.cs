using System.Windows.Controls;

namespace SNIBypassGUI.Views.Dialogs
{
    public partial class DynamicDialog : UserControl
    {
        /// <summary>
        /// 一个可复用的、通过 ViewModel 数据驱动动态生成字段和按钮的对话框视图。
        /// </summary>
        /// <remarks>
        /// <para><b>【数据上下文契约（DataContext Contract）】</b></para>
        /// <para>• 类型：<b>DialogViewModel</b>（位于 SNIBypassGUI.ViewModels.Dialogs 命名空间）</para>
        /// <para>• 属性：</para>
        /// <para>  • <b>Title (string)</b>：对话框标题。</para>
        /// <para>  • <b>Fields (IEnumerable&lt;IDialogField&gt;)</b>：对话框显示字段集合。</para>
        /// <para>  • <b>Buttons (IEnumerable&lt;DialogButtonViewModel&gt;)</b>：底部操作按钮集合。</para>
        ///
        /// <para><b>【支持的字段模型（Fields 集合）】</b></para>
        /// <para>• <b>MessageField</b>：静态文本说明，支持透明度设置。</para>
        /// <para>• <b>InputField</b>：带标签和提示的输入框，可用于收集输入。</para>
        /// <para>• <b>ItemsListField</b>：用于展示一个嵌套项列表（如 BulletedListItem）。</para>
        ///
        /// <para><b>【支持的按钮模型（Buttons 集合）】</b></para>
        /// <para>• <b>DialogButtonViewModel</b>：定义按钮的文本、结果值、默认或取消行为。</para>
        ///
        /// <para><b>【使用方式】</b></para>
        /// <para>• 使用 MaterialDesignThemes 的 DialogHost 显示此对话框。</para>
        /// <para>• 按钮通过 CommandParameter 传出 Result，可用 await DialogHost.Show 进行结果捕获。</para>
        ///
        /// <para>Copyright (c) 2025 Racpast. All rights reserved.</para>
        /// </remarks>
        public DynamicDialog()
        {
            InitializeComponent();
        }
    }
}
