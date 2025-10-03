using System.Windows.Controls;

namespace SNIBypassGUI.Views.Dialogs
{
    public partial class DynamicDialog : UserControl
    {
        /// <summary>
        /// A reusable, data-driven UserControl for creating dynamic dialogs. 
        /// Its content, including informational fields and action buttons, is generated at runtime based on an associated view model.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This control is designed to work within an MVVM architecture, decoupling the dialog's presentation 
        /// from its underlying data and logic. It uses DataTemplates to render different field types.
        /// </para>
        /// 
        /// <para><b>DataContext Contract</b></para>
        /// <para>This view expects its <c>DataContext</c> to be an instance of <c>DialogViewModel</c> (from the <c>SNIBypassGUI.ViewModels.Dialogs</c> namespace), which defines the entire structure of the dialog.</para>
        /// <list type="bullet">
        ///   <item><term><c>Title</c> (string)</term><description>The text displayed in the header of the dialog.</description></item>
        ///   <item><term><c>Fields</c> (IEnumerable&lt;IDialogField&gt;)</term><description>A collection of field models that constitute the main body of the dialog.</description></item>
        ///   <item><term><c>Buttons</c> (IEnumerable&lt;DialogButtonViewModel&gt;)</term><description>A collection of view models defining the action buttons at the bottom of the dialog.</description></item>
        /// </list>
        /// 
        /// <para><b>Supported Field Models</b> (for the <c>Fields</c> collection)</para>
        /// <list type="bullet">
        ///   <item><term><c>MessageField</c></term><description>Displays a block of static, read-only text.</description></item>
        ///   <item><term><c>InputField</c></term><description>Provides a labeled text box for user input, complete with placeholder hint text.</description></item>
        ///   <item><term><c>ItemsListField</c></term><description>Renders a collection of items, typically used with models like <c>BulletedListItem</c> to show nested lists.</description></item>
        /// </list>
        /// 
        /// <para><b>Supported Button Models</b> (for the <c>Buttons</c> collection)</para>
        /// <list type="bullet">
        ///   <item><term><c>DialogButtonViewModel</c></term><description>Defines a button's appearance and behavior, including its content text, the result value it returns upon being clicked, and whether it acts as the default or cancel button.</description></item>
        /// </list>
        /// 
        /// <para><b>Usage</b></para>
        /// <para>
        /// This control is intended to be hosted within a dialog container, such as the <c>DialogHost</c> from MaterialDesignThemes.
        /// The result of the dialog interaction can be captured asynchronously by awaiting the <c>DialogHost.Show</c> method, which returns the <c>Result</c> object from the clicked <c>DialogButtonViewModel</c>.
        /// </para>
        /// 
        /// <para>Copyright (c) 2025 Racpast. All rights reserved.</para>
        /// </remarks>
        public DynamicDialog()
        {
            InitializeComponent();
        }
    }
}