using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using FluentValidation;
using Microsoft.Win32;
using SNIBypassGUI.Common;
using SNIBypassGUI.Common.Codecs;
using SNIBypassGUI.Common.Commands;
using SNIBypassGUI.Common.IO;
using SNIBypassGUI.Common.Network;
using SNIBypassGUI.Enums;
using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Interop.Pcre;
using SNIBypassGUI.Models;
using SNIBypassGUI.Validators;
using SNIBypassGUI.ViewModels.Helpers;
using SNIBypassGUI.ViewModels.Items;
using SNIBypassGUI.ViewModels.Validation;

namespace SNIBypassGUI.ViewModels
{
    public class DnsMappingTablesViewModel : NotifyPropertyChangedBase, IDisposable
    {
        #region Constants
        private static readonly IReadOnlyList<SyntaxItem> s_pcreMetacharacters =
        [
            new(@"\", @"通用转义字符，用于转义特殊字符或引入特殊序列。"),
            new(@"^", @"断言字符串的开始。"),
            new(@"$", @"断言字符串的结束。"),
            new(@".", @"匹配除换行符以外的任何单个字符。"),
            new(@"[", @"开始一个字符类定义。"),
            new(@"]", @"结束一个字符类定义。"),
            new(@"(", @"开始一个子模式（分组）。"),
            new(@")", @"结束一个子模式（分组）。"),
            new(@"|", @"表示或，分隔多个替代分支。"),
            new(@"?", @"作为量词，表示 0 次或 1 次匹配。 也可用于其他语法构造中，如非捕获组或非贪婪量词。"),
            new(@"*", @"作为量词，表示 0 次或多次匹配。"),
            new(@"+", @"作为量词，表示 1 次或多次匹配。 也可用于构成所有格量词。"),
            new(@"{", @"开始一个精确的量词定义。")
];
        private static readonly IReadOnlyList<SyntaxItem> s_pcreCharacterEscapes =
        [
            new(@"\a", @"警报符（BEL, 十六进制 07）。"),
            new(@"\cx", @"匹配 control-x 控制字符。"),
            new(@"\e", @"Escape 字符（十六进制 1B）。"),
            new(@"\f", @"换页符（formfeed, 十六进制 0C）。"),
            new(@"\n", @"换行符（linefeed, 十六进制 0A）。"),
            new(@"\r", @"回车符（carriage return, 十六进制 0D）。"),
            new(@"\t", @"制表符（tab, 十六进制 09）。"),
            new(@"\xhh", @"匹配十六进制代码为 hh 的字符。"),
            new(@"\x{hhh...}", @"匹配十六进制代码为 hhh... 的字符。"),
            new(@"\ddd", @"匹配八进制代码为 ddd 的字符，或作为一个后向引用。"),
            new(@"\Q...\E", @"将 ... 内的所有字符视为字面量，不解释其特殊含义。")
        ];
        private static readonly IReadOnlyList<SyntaxItem> s_pcreCharacterTypes =
        [
            new(@"\d", @"匹配任何一个十进制数字。"),
            new(@"\D", @"匹配任何一个非十进制数字的字符。"),
            new(@"\h", @"匹配任何一个水平空白字符。"),
            new(@"\H", @"匹配任何一个非水平空白字符的字符。"),
            new(@"\s", @"匹配任何一个空白字符。"),
            new(@"\S", @"匹配任何一个非空白字符的字符。"),
            new(@"\v", @"匹配任何一个垂直空白字符。"),
            new(@"\V", @"匹配任何一个非垂直空白字符的字符。"),
            new(@"\w", @"匹配任何一个“单词”字符（字母、数字或下划线）。"),
            new(@"\W", @"匹配任何一个“非单词”字符。"),
            new(@"\p{Prop}", @"匹配具有指定 Unicode 属性的字符。"),
            new(@"\P{Prop}", @"匹配不具有指定 Unicode 属性的字符。"),
            new(@"\X", @"匹配一个扩展的 Unicode 序列（一个非标记字符后跟零个或多个标记字符）。")
        ];
        private static readonly IReadOnlyList<SyntaxItem> s_pcreAnchorsAndBoundaries =
        [
            new(@"^", @"断言字符串的开始。"),
            new(@"$", @"断言字符串的结束。"),
            new(@"\A", @"断言主题的绝对开始位置。"),
            new(@"\Z", @"断言主题的结束位置，或结束位置之前的换行符。"),
            new(@"\z", @"只断言主题的绝对结束位置。"),
            new(@"\b", @"断言一个单词边界。"),
            new(@"\B", @"断言一个非单词边界。"),
            new(@"\G", @"断言当前匹配的起始位置。")
        ];
        private static readonly IReadOnlyList<SyntaxItem> s_pcreCharacterClasses =
        [
            new(@"[...]", @"匹配方括号中任意一个字符。"),
            new(@"[^...]", @"匹配任意一个不在方括号中的字符。"),
            new(@"[a-z]", @"匹配 'a' 到 'z' 范围内的任意一个字符。"),
            new(@"[:alnum:]", @"匹配字母和数字。"),
            new(@"[:alpha:]", @"匹配字母。"),
            new(@"[:ascii:]", @"匹配 ASCII 字符（0-127）。"),
            new(@"[:blank:]", @"匹配空格或制表符。"),
            new(@"[:cntrl:]", @"匹配控制字符。"),
            new(@"[:digit:]", @"匹配十进制数字（同 \d）。"),
            new(@"[:graph:]", @"匹配可打印字符，不包括空格。"),
            new(@"[:lower:]", @"匹配小写字母。"),
            new(@"[:print:]", @"匹配可打印字符，包括空格。"),
            new(@"[:punct:]", @"匹配可打印字符，不包括字母和数字。"),
            new(@"[:space:]", @"匹配空白字符（包括 VT，不同于 \s）。"),
            new(@"[:upper:]", @"匹配大写字母。"),
            new(@"[:word:]", @"匹配单词字符（同 \w）。"),
            new(@"[:xdigit:]", @"匹配十六进制数字。"),
            new(@"[:^name:]", @"否定 POSIX 类，匹配不属于该类的任何字符。")
        ];
        private static readonly IReadOnlyList<SyntaxItem> s_pcreQuantifiers =
        [
            new(@"*", @"匹配前一个元素 0 次或多次（贪婪模式）。"),
            new(@"+", @"匹配前一个元素 1 次或多次（贪婪模式）。"),
            new(@"?", @"匹配前一个元素 0 次或 1 次（贪婪模式）。"),
            new(@"{n}", @"精确匹配前一个元素 n 次。"),
            new(@"{n,}", @"至少匹配前一个元素 n 次（贪婪模式）。"),
            new(@"{n,m}", @"匹配前一个元素 n 到 m 次（贪婪模式）。"),
            new(@"*?", @"匹配前一个元素 0 次或多次（非贪婪/懒惰模式）。"),
            new(@"+?", @"匹配前一个元素 1 次或多次（非贪婪/懒惰模式）。"),
            new(@"??", @"匹配前一个元素 0 次或 1 次（非贪婪/懒惰模式）。"),
            new(@"{n,}?", @"至少匹配前一个元素 n 次（非贪婪/懒惰模式）。"),
            new(@"{n,m}?", @"匹配前一个元素 n 到 m 次（非贪婪/懒惰模式）。"),
            new(@"*+", @"匹配前一个元素 0 次或多次（所有格/独占模式）。"),
            new(@"++", @"匹配前一个元素 1 次或多次（所有格/独占模式）。"),
            new(@"?+", @"匹配前一个元素 0 次或 1 次（所有格/独占模式）。"),
            new(@"{n,}+", @"至少匹配前一个元素 n 次（所有格/独占模式）。"),
            new(@"{n,m}+", @"匹配前一个元素 n 到 m 次（所有格/独占模式）。")
        ];
        private static readonly IReadOnlyList<SyntaxItem> s_pcreGroupingCapturingAndSubroutines =
        [
            new(@"(...)", @"捕获型分组。匹配的内容被捕获，并可通过后向引用访问。"),
            new(@"(?:...)", @"非捕获型分组。只用于分组，不进行捕获。"),
            new(@"(?>...)", @"原子分组。一旦匹配，分组内的部分将不会被回溯。"),
            new(@"(?<name>...)", @"命名捕获分组 (Perl 语法)。"),
            new(@"(?'name'...)", @"命名捕获分组 (Perl 语法)。"),
            new(@"(?P<name>...)", @"命名捕获分组 (Python 语法)。"),
            new(@"(?|...)", @"分支重置分组。每个分支中的捕获括号编号从相同的数字开始。"),
            new(@"(?R) 或 (?0)", @"递归整个模式。"),
            new(@"(?n)", @"递归或作为子程序调用第 n 个子模式。"),
            new(@"(?&name)", @"递归或作为子程序调用名为 'name' 的子模式 (Perl 语法)。"),
            new(@"(?P>name)", @"递归或作为子程序调用名为 'name' 的子模式 (PCRE 旧语法)。"),
            new(@"\g<n> 或 \g'n'", @"作为子程序调用第 n 个子模式 (Oniguruma 语法)。"),
            new(@"\g<name> 或 \g'name'", @"作为子程序调用名为 'name' 的子模式 (Oniguruma 语法)。")
        ];
        private static readonly IReadOnlyList<SyntaxItem> s_pcreLookarounds =
        [
            new(@"(?=...)", @"肯定顺序环视（正向先行断言）。要求 ... 模式能在当前位置匹配，但不消耗任何字符。"),
            new(@"(?!...)", @"否定顺序环视（负向先行断言）。要求 ... 模式不能在当前位置匹配，但不消耗任何字符。"),
            new(@"(?<=...)", @"肯定逆序环视（正向后行断言）。要求 ... 模式能在当前位置之前匹配，但不消耗任何字符，且 ... 必须是固定长度的。"),
            new(@"(?<!...)", @"否定逆序环视（负向后行断言）。要求 ... 模式不能在当前位置之前匹配，但不消耗任何字符，且 ... 必须是固定长度的。")
        ];
        private static readonly IReadOnlyList<SyntaxItem> s_pcreBackReferences =
        [
            new(@"\n", @"后向引用到第 n 个捕获子模式匹配的内容。"),
            new(@"\g{n} 或 \gn", @"绝对后向引用到第 n 个捕获子模式。"),
            new(@"\g{-n}", @"相对后向引用到倒数第 n 个捕获子模式。"),
            new(@"\g{name}", @"命名后向引用到名为 'name' 的子模式。"),
            new(@"\k<name>", @"命名后向引用 (Perl/.NET 语法)。"),
            new(@"\k'name'", @"命名后向引用 (Perl 语法)。"),
            new(@"\k{name}", @"命名后向引用 (.NET 语法)。"),
            new(@"(?P=name)", @"命名后向引用 (Python 语法)。")
        ];
        private static readonly IReadOnlyList<SyntaxItem> s_pcreConditionalSubpatterns =
        [
            new(@"(?(n)yes|no)", @"如果第 n 个捕获组已匹配，则匹配 'yes' 模式，否则匹配 'no' 模式。"),
            new(@"(?(<name>)yes|no)", @"如果名为 'name' 的捕获组已匹配，则匹配 'yes' 模式，否则匹配 'no' 模式。"),
            new(@"(?(R)yes|no)", @"如果在递归中，则匹配 'yes' 模式，否则匹配 'no' 模式。"),
            new(@"(?(Rn)yes|no)", @"如果最近的递归是进入第 n 个子模式，则条件为真。"),
            new(@"(?(R&name)yes|no)", @"如果最近的递归是进入名为 'name' 的子模式，则条件为真。"),
            new(@"(?(DEFINE)...)", @"定义一个在匹配过程中被跳过的“子程序”组，仅用于被引用。"),
            new(@"(?(assertion)yes|no)", @"如果环视 'assertion' 为真，则匹配 'yes' 模式，否则匹配 'no' 模式。")
        ];
        private static readonly IReadOnlyList<SyntaxItem> s_pcreBacktrackingControlVerbs =
        [
            new(@"(*ACCEPT)", @"立即成功结束整个匹配，跳过模式的其余部分。"),
            new(@"(*FAIL) 或 (*F)", @"强制当前匹配路径失败，并立即进行回溯。"),
            new(@"(*COMMIT)", @"如果模式的其余部分不匹配，则彻底失败，不再尝试其他起始点。"),
            new(@"(*PRUNE)", @"如果模式的其余部分不匹配，则丢弃所有回溯位置，从当前位置失败。"),
            new(@"(*SKIP)", @"类似于 (*PRUNE)，但在非锚定模式下，将下一个匹配起始点移动到 (*SKIP) 的位置。"),
            new(@"(*THEN)", @"如果模式的其余部分不匹配，则跳到下一个替代分支，取消当前分支内的回溯。")
        ];
        private static readonly IReadOnlyList<SyntaxItem> s_pcrePatternOptionsAndDirectives =
        [
            new(@"(?i)", @"设置不区分大小写模式。"),
            new(@"(?m)", @"设置多行模式（^ 和 $ 匹配行的开始和结束）。"),
            new(@"(?s)", @"设置点号匹配所有字符模式（包括换行符）。"),
            new(@"(?x)", @"设置扩展模式（忽略空白和 # 后面的注释）。"),
            new(@"(?J)", @"允许重复的子模式名称。"),
            new(@"(?U)", @"设置非贪婪模式（反转量词的贪婪性）。"),
            new(@"(?-...)", @"取消一个或多个选项，例如 (?-i) 关闭不区分大小写。"),
            new(@"(?i:...)", @"在非捕获型分组内设置选项。"),
            new(@"(*CR)", @"指定换行符为回车 (CR)。"),
            new(@"(*LF)", @"指定换行符为换行 (LF)。"),
            new(@"(*CRLF)", @"指定换行符为回车换行 (CRLF)。"),
            new(@"(*ANYCRLF)", @"指定换行符为 CR, LF, 或 CRLF 中的任意一种。"),
            new(@"(*ANY)", @"指定换行符为所有 Unicode 换行序列。"),
            new(@"(*UTF8)", @"在模式开始处指定 UTF-8 模式。"),
            new(@"(*BSR_ANYCRLF)", @"使 \R 只匹配 CR, LF, 或 CRLF。"),
            new(@"(*BSR_UNICODE)", @"使 \R 匹配任何 Unicode 换行序列。")
        ];
        private static readonly IReadOnlyList<SyntaxItem> s_pcreMiscellaneous =
        [
            new(@"(?#...)", @"内联注释，内容会被忽略。"),
            new(@"#...", @"在扩展模式 (?x) 下，从 # 到行尾是注释。"),
            new(@"\K", @"重置匹配的起点，将之前匹配的字符从最终匹配结果中排除。"),
            new(@"\C", @"匹配单个字节（不建议使用，即使在 UTF-8 模式下）。"),
            new(@"\R", @"匹配一个通用的换行符序列。"),
            new(@"(?C)", @"在模式中设置一个 callout 点。"),
            new(@"(?Cn)", @"设置一个带编号 n 的 callout 点。")
        ];
        #endregion

        #region Dependencies & Instance State
        private readonly IConfigSetService<DnsMappingTable> _tableService;
        private readonly IConfigSetService<Resolver> _resolverService;
        private readonly IDialogService _dialogService;
        private readonly IFactory<DnsMappingGroup> _groupFactory;
        private readonly IFactory<DnsMappingRule> _ruleFactory;
        private readonly IFactory<TargetIpSource> _sourceFactory;
        private readonly IFactory<FallbackAddress> _addressFactory;
        private readonly DnsMappingTableValidator _configValidator;
        private DnsMappingTable _originalTable;
        private DnsMappingTable _editingTableCopy;
        private DnsMappingTableViewModel _editingTableVM;
        private TargetIpSource _selectedSource;
        private object _selectedTreeItem;
        private string _associatedResolverName;
        private bool _isGroupSelected;
        private bool _isRuleSelected;
        private bool _isBusy;
        private bool _canExecuteCopy = true;
        private EditingState _currentState;
        private IReadOnlyList<ValidationErrorNode> _validationErrors;
        private IReadOnlyList<ValidationErrorNode> _validationWarnings;
        private readonly ObservableCollection<DnsMappingTableViewModel> _allTableVMs = [];
        #endregion

        #region Constructor
        public DnsMappingTablesViewModel(
            IConfigSetService<DnsMappingTable> tableService,
            IConfigSetService<Resolver> resolverService,
            IFactory<DnsMappingGroup> groupFactory, 
            IFactory<DnsMappingRule> ruleFactory, 
            IFactory<TargetIpSource> sourceFactory, 
            IFactory<FallbackAddress> addressFactory, 
            IDialogService dialogService)
        {
            _tableService = tableService;
            _resolverService = resolverService;
            _groupFactory = groupFactory;
            _ruleFactory = ruleFactory;
            _sourceFactory = sourceFactory;
            _addressFactory = addressFactory;
            _dialogService = dialogService;
            _configValidator = new DnsMappingTableValidator();

            AllTables = new ReadOnlyObservableCollection<DnsMappingTable>(_tableService.AllConfigs);
            AllTableVMs = new ReadOnlyObservableCollection<DnsMappingTableViewModel>(_allTableVMs);
            TableSelector = new SilentSelector<DnsMappingTableViewModel>(HandleUserSelectionChangedAsync);

            _resolverService.ConfigRemoved += HandleResolverRemoved;
            _resolverService.ConfigRenamed += HandleResolverRenamed;
            _resolverService.ConfigUpdated += HandleResolverUpdated;

            _tableService.AllConfigs.CollectionChanged += OnAllTablesCollectionChanged;

            CopyLinkCodeCommand = new AsyncCommand<DnsMappingTableViewModel>(ExecuteCopyLinkCodeAsync, CanExecuteCopyLinkCode);

            AddNewTableCommand = new AsyncCommand(ExecuteAddNewTableAsync, CanExecuteWhenNotBusy);
            DuplicateTableCommand = new AsyncCommand(ExecuteDuplicateTableAsync, CanExecuteDuplicateTable);
            DeleteTableCommand = new AsyncCommand(ExecuteDeleteTableAsync, CanExecuteOnEditableTable);
            RenameTableCommand = new AsyncCommand(ExecuteRenameTableAsync, CanExecuteOnEditableTable);
            ImportTableCommand = new AsyncCommand(ExecuteImportTableAsync, CanExecuteWhenNotBusy);
            ExportTableCommand = new AsyncCommand(ExecuteExportTableAsync, CanExecuteExport);

            AddNewGroupCommand = new AsyncCommand(ExecuteAddNewGroupAsync, CanExecuteAddNewGroup);
            RemoveSelectedGroupCommand = new RelayCommand(ExecuteRemoveSelectedGroup, CanExecuteOnSelectedGroup);
            RenameSelectedGroupCommand = new AsyncCommand(ExecuteRenameSelectedGroupAsync, CanExecuteOnSelectedGroup);
            MoveSelectedGroupUpCommand = new RelayCommand(ExecuteMoveSelectedGroupUp, CanExecuteMoveSelectedGroupUp);
            MoveSelectedGroupDownCommand = new RelayCommand(ExecuteMoveSelectedGroupDown, CanExecuteMoveSelectedGroupDown);
            AddGroupIconCommand = new AsyncCommand<DnsMappingGroupViewModel>(ExecuteAddGroupIconAsync, CanExecuteAddGroupIcon);
            RemoveGroupIconCommand = new RelayCommand<DnsMappingGroupViewModel>(ExecuteRemoveGroupIcon, CanExecuteRemoveGroupIcon);

            AddNewRuleCommand = new RelayCommand(ExecuteAddNewRule, CanExecuteAddRule);
            RemoveSelectedRuleCommand = new RelayCommand(ExecuteRemoveSelectedRule, CanExecuteOnSelectedRule);
            MoveSelectedRuleUpCommand = new RelayCommand(ExecuteMoveSelectedRuleUp, CanExecuteMoveSelectedRuleUp);
            MoveSelectedRuleDownCommand = new RelayCommand(ExecuteMoveSelectedRuleDown, CanExecuteMoveSelectedRuleDown);

            EditDomainPatternCommand = new AsyncCommand<string>(ExecuteEditDomainPatternAsync, CanExecuteEditDomainPattern);
            AddDomainPatternCommand = new AsyncCommand(ExecuteAddDomainPatternAsync, CanExecuteOnSelectedRule);
            DeleteDomainPatternCommand = new RelayCommand<string>(ExecuteDeleteDomainPattern, CanExecuteOnSelectedRule);

            AddNewSourceCommand = new RelayCommand(ExecuteAddNewSource, CanExecuteAddNewSource);
            RemoveSelectedSourceCommand = new RelayCommand(ExecuteRemoveSelectedSource, CanExecuteOnSelectedSource);
            MoveSelectedSourceUpCommand = new RelayCommand(ExecuteMoveSelectedSourceUp, CanExecuteMoveSelectedSourceUp);
            MoveSelectedSourceDownCommand = new RelayCommand(ExecuteMoveSelectedSourceDown, CanExecuteMoveSelectedSourceDown);

            MoveAddressUpCommand = new RelayCommand<string>(ExecuteMoveAddressUp, CanExecuteMoveAddressUp);
            MoveAddressDownCommand = new RelayCommand<string>(ExecuteMoveAddressDown, CanExecuteMoveAddressDown);
            EditAddressCommand = new AsyncCommand<string>(ExecuteEditAddressAsync, CanExecuteOnSelectedSource);
            DeleteAddressCommand = new RelayCommand<string>(ExecuteDeleteAddress, CanExecuteOnSelectedSource);
            AddAddressCommand = new AsyncCommand(ExecuteAddAddressAsync, CanExecuteOnSelectedSource);
            DeleteAllAddressesCommand = new AsyncCommand(ExecuteDeleteAllAddressesAsync, CanExecuteDeleteAllAddresses);

            PasteResolverLinkCodeCommand = new AsyncCommand(ExecutePasteResolverLinkCodeAsync, CanExecuteOnSelectedRule);
            UnlinkResolverCommand = new RelayCommand(ExecuteUnlinkResolver, CanExecuteOnSelectedRule);

            AddQueryDomainCommand = new AsyncCommand(ExecuteAddQueryDomainAsync, CanExecuteOnSelectedSource);
            DeleteQueryDomainCommand = new RelayCommand<string>(ExecuteDeleteQueryDomain, CanExecuteOnSelectedSource);
            EditQueryDomainCommand = new AsyncCommand<string>(ExecuteEditQueryDomainAsync, CanExecuteEditQueryDomain);

            LockFallbackAddressCommand = new RelayCommand<FallbackAddress>(ExecuteLockFallbackAddress, CanExecuteLockFallbackAddress);
            UnlockFallbackAddressCommand = new RelayCommand<FallbackAddress>(ExecuteUnlockFallbackAddress, CanExecuteUnlockFallbackAddress);
            MoveFallbackAddressUpCommand = new RelayCommand<FallbackAddress>(ExecuteMoveFallbackAddressUp, CanExecuteMoveFallbackAddressUp);
            MoveFallbackAddressDownCommand = new RelayCommand<FallbackAddress>(ExecuteMoveFallbackAddressDown, CanExecuteMoveFallbackAddressDown);
            EditFallbackAddressCommand = new AsyncCommand<FallbackAddress>(ExecuteEditFallbackAddressAsync, CanExecuteOnSelectedSource);
            DeleteFallbackAddressCommand = new RelayCommand<FallbackAddress>(ExecuteDeleteFallbackAddress, CanExecuteOnSelectedSource);
            AddFallbackAddressCommand = new AsyncCommand(ExecuteAddFallbackAddressAsync, CanExecuteOnSelectedSource);
            DeleteAllFallbackAddressesCommand = new AsyncCommand(ExecuteDeleteAllFallbackAddressesAsync, CanExecuteDeleteAllFallbackAddresses);

            SaveChangesCommand = new AsyncCommand(ExecuteSaveChangesAsync, CanExecuteSave);
            DiscardChangesCommand = new RelayCommand(ExecuteDiscardChanges, CanExecuteWhenDirty);

            _tableService.LoadData();

            if (AllTableVMs.Any())
                SwitchToTable(AllTableVMs.First());
        }
        #endregion

        #region Public Properties
        public IReadOnlyList<SyntaxItem> PcreMetacharacters => s_pcreMetacharacters;
        public IReadOnlyList<SyntaxItem> PcreCharacterEscapes => s_pcreCharacterEscapes;
        public IReadOnlyList<SyntaxItem> PcreCharacterTypes => s_pcreCharacterTypes;
        public IReadOnlyList<SyntaxItem> PcreAnchorsAndBoundaries => s_pcreAnchorsAndBoundaries;
        public IReadOnlyList<SyntaxItem> PcreCharacterClasses => s_pcreCharacterClasses;
        public IReadOnlyList<SyntaxItem> PcreQuantifiers => s_pcreQuantifiers;
        public IReadOnlyList<SyntaxItem> PcreGroupingCapturingAndSubroutines => s_pcreGroupingCapturingAndSubroutines;
        public IReadOnlyList<SyntaxItem> PcreLookarounds => s_pcreLookarounds;
        public IReadOnlyList<SyntaxItem> PcreBackReferences => s_pcreBackReferences;
        public IReadOnlyList<SyntaxItem> PcreConditionalSubpatterns => s_pcreConditionalSubpatterns;
        public IReadOnlyList<SyntaxItem> PcreBacktrackingControlVerbs => s_pcreBacktrackingControlVerbs;
        public IReadOnlyList<SyntaxItem> PcrePatternOptionsAndDirectives => s_pcrePatternOptionsAndDirectives;
        public IReadOnlyList<SyntaxItem> PcreMiscellaneous => s_pcreMiscellaneous;

        public ReadOnlyObservableCollection<DnsMappingTable> AllTables { get; }

        public ReadOnlyObservableCollection<DnsMappingTableViewModel> AllTableVMs { get; }

        public SilentSelector<DnsMappingTableViewModel> TableSelector { get; }

        public EditingState CurrentState { get => _currentState; private set => SetProperty(ref _currentState, value); }

        public DnsMappingTableViewModel EditingTableVM
        {
            get => _editingTableVM;
            private set => SetProperty(ref _editingTableVM, value);
        }

        public DnsMappingTable EditingTableCopy
        {
            get => _editingTableCopy;
            private set
            {
                if (_editingTableCopy != null) StopListeningToChanges(_editingTableCopy);

                if (SetProperty(ref _editingTableCopy, value))
                {
                    EditingTableVM?.Dispose(); // 销毁

                    if (_editingTableCopy != null)
                    {
                        EditingTableVM = new DnsMappingTableViewModel(_editingTableCopy, DoesResolverRequireIPv6);
                        StartListeningToChanges(_editingTableCopy);
                    }
                    else EditingTableVM = null;

                    OnPropertyChanged(nameof(EditingTableVM));

                    SelectedTreeItem = null;
                    SelectedSource = null;
                }
            }
        }

        public object SelectedTreeItem
        {
            get => _selectedTreeItem;
            set
            {
                if (SetProperty(ref _selectedTreeItem, value))
                {
                    // 更新选中项的类型状态
                    IsGroupSelected = value is DnsMappingGroupViewModel;
                    IsRuleSelected = value is DnsMappingRuleViewModel;

                    // 当选中的是“规则”时
                    if (value is DnsMappingRuleViewModel ruleVM)
                    {
                        var parentGroupVM = ruleVM.Parent;
                        if (parentGroupVM != null)
                        {
                            parentGroupVM.IsExpanded = true;
                        }

                        SelectedSource = ruleVM.Model.TargetSources?.FirstOrDefault();
                    }
                    else
                    {
                        SelectedSource = null;
                    }

                    UpdateAssociatedResolverName();
                    UpdateCommandStates();
                }
            }
        }

        public TargetIpSource SelectedSource
        {
            get => _selectedSource;
            set
            {
                if (SetProperty(ref _selectedSource, value))
                {
                    UpdateAssociatedResolverName();
                    UpdateCommandStates();
                }
            }
        }

        public bool IsGroupSelected { get => _isGroupSelected; private set => SetProperty(ref _isGroupSelected, value); }

        public bool IsRuleSelected { get => _isRuleSelected; private set => SetProperty(ref _isRuleSelected, value); }

        public string AssociatedResolverName { get => _associatedResolverName; private set => SetProperty(ref _associatedResolverName, value); }

        public IReadOnlyList<ValidationErrorNode> ValidationErrors { get => _validationErrors; private set => SetProperty(ref _validationErrors, value); }

        public bool HasValidationErrors => ValidationErrors?.Any() == true;

        public IReadOnlyList<ValidationErrorNode> ValidationWarnings { get => _validationWarnings; private set => SetProperty(ref _validationWarnings, value); }

        public bool HasValidationWarnings => ValidationWarnings?.Any() == true;
        #endregion

        #region Public Commands
        public ICommand CopyLinkCodeCommand { get; }
        public ICommand AddNewTableCommand { get; }
        public ICommand DuplicateTableCommand { get; }
        public ICommand DeleteTableCommand { get; }
        public ICommand RenameTableCommand { get; }
        public ICommand ImportTableCommand { get; }
        public ICommand ExportTableCommand { get; }
        public ICommand AddNewGroupCommand { get; }
        public ICommand RemoveSelectedGroupCommand { get; }
        public ICommand RenameSelectedGroupCommand { get; }
        public ICommand MoveSelectedGroupUpCommand { get; }
        public ICommand MoveSelectedGroupDownCommand { get; }
        public ICommand AddGroupIconCommand { get; }
        public ICommand RemoveGroupIconCommand { get; }
        public ICommand AddNewRuleCommand { get; }
        public ICommand RemoveSelectedRuleCommand { get; }
        public ICommand MoveSelectedRuleUpCommand { get; }
        public ICommand MoveSelectedRuleDownCommand { get; }
        public ICommand EditDomainPatternCommand { get; }
        public ICommand AddDomainPatternCommand { get; }
        public ICommand DeleteDomainPatternCommand { get; }
        public ICommand AddNewSourceCommand { get; }
        public ICommand RemoveSelectedSourceCommand { get; }
        public ICommand MoveSelectedSourceUpCommand { get; }
        public ICommand MoveSelectedSourceDownCommand { get; }
        public ICommand MoveAddressUpCommand { get; }
        public ICommand MoveAddressDownCommand { get; }
        public ICommand EditAddressCommand { get; }
        public ICommand DeleteAddressCommand { get; }
        public ICommand AddAddressCommand { get; }
        public ICommand DeleteAllAddressesCommand { get; }
        public ICommand EditQueryDomainCommand { get; }
        public ICommand AddQueryDomainCommand { get; }
        public ICommand DeleteQueryDomainCommand { get; }
        public ICommand PasteResolverLinkCodeCommand { get; }
        public ICommand UnlinkResolverCommand { get; }
        public ICommand MoveFallbackAddressUpCommand { get; }
        public ICommand MoveFallbackAddressDownCommand { get; }
        public ICommand LockFallbackAddressCommand { get; }
        public ICommand UnlockFallbackAddressCommand { get; }
        public ICommand DeleteFallbackAddressCommand { get; }
        public ICommand AddFallbackAddressCommand { get; }
        public ICommand EditFallbackAddressCommand { get; }
        public ICommand DeleteAllFallbackAddressesCommand { get; }
        public ICommand SaveChangesCommand { get; }
        public ICommand DiscardChangesCommand { get; }
        #endregion

        #region Lifecycle & State Management
        private async Task HandleUserSelectionChangedAsync(DnsMappingTableViewModel newItem, DnsMappingTableViewModel oldItem)
        {
            _isBusy = true;
            UpdateCommandStates();

            try
            {
                if (_currentState != EditingState.None)
                {
                    var result = await PromptToSaveChangesAndContinueAsync();
                    switch (result)
                    {
                        case SaveChangesResult.Save:
                        case SaveChangesResult.Discard:
                            SwitchToTable(newItem);
                            break;
                        case SaveChangesResult.Cancel:
                            TableSelector.SetItemSilently(oldItem);
                            break;
                    }
                }
                else SwitchToTable(newItem);
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }

        private void SwitchToTable(DnsMappingTableViewModel newTableVM)
        {
            TableSelector.SetItemSilently(newTableVM);
            ResetToSelectedTable();
        }

        private void ResetToSelectedTable()
        {
            _originalTable = TableSelector.SelectedItem?.Model;
            EditingTableCopy = _originalTable?.Clone();
            TransitionToState(EditingState.None);
        }

        private void EnterCreationMode(string name)
        {
            TableSelector.SetItemSilently(null);
            _originalTable = null;
            EditingTableCopy = _tableService.CreateDefault();
            EditingTableCopy.TableName = name;
            TransitionToState(EditingState.Creating);
        }

        private void TransitionToState(EditingState newState)
        {
            CurrentState = newState;
            ValidateEditingCopy();
            UpdateCommandStates();
        }

        private void UpdateCommandStates()
        {
            (CopyLinkCodeCommand as AsyncCommand<DnsMappingTableViewModel>)?.RaiseCanExecuteChanged();

            (AddNewTableCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (DeleteTableCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (RenameTableCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (ImportTableCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (ExportTableCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (DuplicateTableCommand as AsyncCommand)?.RaiseCanExecuteChanged();

            (AddNewGroupCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (RemoveSelectedGroupCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (RenameSelectedGroupCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (MoveSelectedGroupUpCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (MoveSelectedGroupDownCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AddGroupIconCommand as AsyncCommand<DnsMappingGroupViewModel>)?.RaiseCanExecuteChanged();
            (RemoveGroupIconCommand as RelayCommand<DnsMappingGroupViewModel>)?.RaiseCanExecuteChanged();

            (AddNewRuleCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (RemoveSelectedRuleCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (MoveSelectedRuleUpCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (MoveSelectedRuleDownCommand as RelayCommand)?.RaiseCanExecuteChanged();

            (EditDomainPatternCommand as AsyncCommand<string>)?.RaiseCanExecuteChanged();
            (AddDomainPatternCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (DeleteDomainPatternCommand as RelayCommand<string>)?.RaiseCanExecuteChanged();

            (AddNewSourceCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (RemoveSelectedSourceCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (MoveSelectedSourceUpCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (MoveSelectedSourceDownCommand as RelayCommand)?.RaiseCanExecuteChanged();

            (MoveAddressUpCommand as RelayCommand<string>)?.RaiseCanExecuteChanged();
            (MoveAddressDownCommand as RelayCommand<string>)?.RaiseCanExecuteChanged();
            (EditAddressCommand as AsyncCommand<string>)?.RaiseCanExecuteChanged();
            (DeleteAddressCommand as RelayCommand<string>)?.RaiseCanExecuteChanged();
            (AddAddressCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (DeleteAllAddressesCommand as AsyncCommand)?.RaiseCanExecuteChanged();

            (PasteResolverLinkCodeCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (UnlinkResolverCommand as RelayCommand)?.RaiseCanExecuteChanged();

            (AddQueryDomainCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (DeleteQueryDomainCommand as RelayCommand<string>)?.RaiseCanExecuteChanged();
            (EditQueryDomainCommand as AsyncCommand<string>)?.RaiseCanExecuteChanged();

            (MoveFallbackAddressUpCommand as RelayCommand<FallbackAddress>)?.RaiseCanExecuteChanged();
            (MoveFallbackAddressDownCommand as RelayCommand<FallbackAddress>)?.RaiseCanExecuteChanged();
            (DeleteFallbackAddressCommand as RelayCommand<FallbackAddress>)?.RaiseCanExecuteChanged();
            (EditFallbackAddressCommand as AsyncCommand<FallbackAddress>)?.RaiseCanExecuteChanged();
            (LockFallbackAddressCommand as RelayCommand<FallbackAddress>)?.RaiseCanExecuteChanged();
            (UnlockFallbackAddressCommand as RelayCommand<FallbackAddress>)?.RaiseCanExecuteChanged();
            (AddFallbackAddressCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (DeleteAllFallbackAddressesCommand as AsyncCommand)?.RaiseCanExecuteChanged();

            (SaveChangesCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (DiscardChangesCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
        #endregion

        #region Table Management
        #region Add New Table
        private async Task ExecuteAddNewTableAsync()
        {
            _isBusy = true;
            UpdateCommandStates();

            try
            {
                if (_currentState != EditingState.None)
                {
                    var result = await PromptToSaveChangesAndContinueAsync();
                    if (result == SaveChangesResult.Cancel)
                    {
                        _isBusy = false;
                        UpdateCommandStates();
                        return;
                    }
                }

                var newName = await _dialogService.ShowTextInputAsync("新建映射表", "请输入新映射表的名称：", "新映射表");
                if (newName != null)
                {
                    if (!string.IsNullOrWhiteSpace(newName)) EnterCreationMode(newName);
                    else await _dialogService.ShowInfoAsync("创建失败", "映射表名称不能为空！");
                }
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }
        #endregion

        #region Duplicate Table
        private async Task ExecuteDuplicateTableAsync()
        {
            _isBusy = true;
            UpdateCommandStates();

            try
            {
                if (_currentState != EditingState.None)
                {
                    var result = await PromptToSaveChangesAndContinueAsync();
                    if (result == SaveChangesResult.Cancel) return;
                }

                var tableToCloneVM = TableSelector.SelectedItem;
                if (tableToCloneVM == null) return;

                var tableToCloneModel = tableToCloneVM.Model;
                var suggestedName = $"{tableToCloneModel.TableName} - 副本";
                var newName = await _dialogService.ShowTextInputAsync("创建副本", "请输入新映射表的名称：", suggestedName);

                if (newName != null && !string.IsNullOrWhiteSpace(newName))
                {
                    var newTable = tableToCloneModel.Clone();
                    newTable.TableName = newName;
                    newTable.IsBuiltIn = false;
                    newTable.Id = Guid.NewGuid();

                    _tableService.AllConfigs.Add(newTable);
                    await _tableService.SaveChangesAsync(newTable);

                    var newTableVM = AllTableVMs.FirstOrDefault(vm => vm.Model == newTable);
                    if (newTableVM != null) SwitchToTable(newTableVM);
                }
                else if (newName != null)
                    await _dialogService.ShowInfoAsync("创建失败", "映射表名称不能为空！");
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }

        private bool CanExecuteDuplicateTable() => TableSelector.SelectedItem != null && !_isBusy;
        #endregion

        #region Delete Table
        private async Task ExecuteDeleteTableAsync()
        {
            _isBusy = true;
            UpdateCommandStates();

            try
            {
                if (_currentState != EditingState.None)
                {
                    var result = await PromptToSaveChangesAndContinueAsync();
                    if (result == SaveChangesResult.Cancel)
                    {
                        _isBusy = false;
                        UpdateCommandStates();
                        return;
                    }
                }

                var tableToDeleteVM = TableSelector.SelectedItem;
                var tableToDelete = tableToDeleteVM.Model;
                var confirmResult = await _dialogService.ShowConfirmationAsync("确认删除", $"您确定要删除 “{tableToDelete.TableName}” 吗？\n此操作不可恢复！", "删除");

                if (confirmResult)
                {
                    DnsMappingTableViewModel nextSelectionVM = null;
                    if (AllTableVMs.Count > 1)
                    {
                        int currentIndex = AllTableVMs.IndexOf(tableToDeleteVM);
                        nextSelectionVM = currentIndex == AllTableVMs.Count - 1
                            ? AllTableVMs[currentIndex - 1]
                            : AllTableVMs[currentIndex + 1];
                    }
                    _tableService.DeleteConfig(tableToDelete);
                    SwitchToTable(nextSelectionVM);
                }
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }
        #endregion

        #region Rename Table
        private async Task ExecuteRenameTableAsync()
        {
            _isBusy = true;
            UpdateCommandStates();

            try
            {
                if (_currentState != EditingState.None)
                {
                    var result = await PromptToSaveChangesAndContinueAsync();
                    if (result == SaveChangesResult.Cancel) return;
                }

                var newName = await _dialogService.ShowTextInputAsync($"重命名 “{EditingTableCopy.TableName}”", "请输入新的映射表名称：", EditingTableCopy.TableName);
                if (newName != null && !string.IsNullOrWhiteSpace(newName))
                {
                    if (newName != EditingTableCopy.TableName)
                    {
                        EditingTableCopy.TableName = newName;
                        await _tableService.SaveChangesAsync(EditingTableCopy);
                        ResetToSelectedTable();
                    }
                }
                else if (newName != null)
                    await _dialogService.ShowInfoAsync("重命名失败", "映射表名称不能为空！");
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }
        #endregion

        #region Import & Export Table
        private async Task ExecuteImportTableAsync()
        {
            _isBusy = true;
            UpdateCommandStates();

            try
            {
                if (_currentState != EditingState.None)
                {
                    var result = await PromptToSaveChangesAndContinueAsync();
                    if (result == SaveChangesResult.Cancel) return;
                }

                var openFileDialog = new OpenFileDialog
                {
                    Filter = "映射表文件 (*.smt)|*.smt",
                    Title = "选择要导入的映射表文件",
                    RestoreDirectory = true
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    var importedTableModel = _tableService.ImportConfig(openFileDialog.FileName);
                    if (importedTableModel != null)
                    {
                        var importedTableVM = AllTableVMs.FirstOrDefault(vm => vm.Model == importedTableModel);
                        if (importedTableVM != null) SwitchToTable(importedTableVM);
                    }
                    else await _dialogService.ShowInfoAsync("错误", "映射表导入失败。");
                }
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }

        private async Task ExecuteExportTableAsync()
        {
            if (_currentState == EditingState.Creating)
            {
                var confirmResult = await _dialogService.ShowConfirmationAsync("保存并导出", "此映射表尚未保存，必须先保存才能导出。\n是否立即保存并继续导出？", "保存并导出");

                if (!confirmResult) return;

                await ExecuteSaveChangesAsync();
                if (_currentState != EditingState.None) return;
            }
            else if (_currentState == EditingState.Editing)
            {
                var choice = await _dialogService.ShowExportConfirmationAsync(EditingTableCopy.TableName);
                switch (choice)
                {
                    case ExportChoice.SaveAndExport:
                        await ExecuteSaveChangesAsync();
                        if (_currentState != EditingState.None) return;
                        break;
                    case ExportChoice.ExportWithoutSaving:
                        break;
                    default:
                        return;
                }
            }

            var tableToExportVM = TableSelector.SelectedItem;
            if (tableToExportVM == null)
            {
                await _dialogService.ShowInfoAsync("错误", "没有可导出的映射表。");
                return;
            }

            var tableToExport = tableToExportVM.Model;
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "映射表文件 (*.smt)|*.smt",
                Title = "选择映射表导出位置",
                FileName = $"{tableToExport.TableName}.smt",
                RestoreDirectory = true
            };
            if (saveFileDialog.ShowDialog() == true)
                _tableService.ExportConfig(tableToExport, saveFileDialog.FileName);
        }

        private bool CanExecuteExport()
        {
            if (_isBusy) return false;

            // 当正在创建一个新映射表时，允许导出以便触发保存并导出的流程
            if (_currentState == EditingState.Creating) return true;

            if (TableSelector.SelectedItem != null && !TableSelector.SelectedItem.IsBuiltIn) return true;

            // 其他所有情况都禁用
            return false;
        }
        #endregion
        #endregion

        #region Editing Area Operations
        #region Change Listening & Validation
        private void StartListeningToChanges(DnsMappingTable table)
        {
            table.PropertyChanged += OnEditingCopyPropertyChanged;
            if (table.MappingGroups != null)
            {
                table.MappingGroups.CollectionChanged += OnGroupsCollectionChanged;
                foreach (var group in table.MappingGroups) ListenToGroup(group);
            }
        }

        private void StopListeningToChanges(DnsMappingTable table)
        {
            table.PropertyChanged -= OnEditingCopyPropertyChanged;
            if (table.MappingGroups != null)
            {
                table.MappingGroups.CollectionChanged -= OnGroupsCollectionChanged;
                foreach (var group in table.MappingGroups) StopListeningToGroup(group);
            }
        }

        private void ListenToGroup(DnsMappingGroup group)
        {
            group.PropertyChanged += OnEditingCopyPropertyChanged;
            if (group.MappingRules != null)
            {
                group.MappingRules.CollectionChanged += OnRulesCollectionChanged;
                foreach (var rule in group.MappingRules) ListenToRule(rule);
            }
        }

        private void StopListeningToGroup(DnsMappingGroup group)
        {
            group.PropertyChanged -= OnEditingCopyPropertyChanged;
            if (group.MappingRules != null)
            {
                group.MappingRules.CollectionChanged -= OnRulesCollectionChanged;
                foreach (var rule in group.MappingRules) StopListeningToRule(rule);
            }
        }

        private void ListenToRule(DnsMappingRule rule)
        {
            rule.PropertyChanged += OnEditingCopyPropertyChanged;
            if (rule.DomainPatterns != null)
                rule.DomainPatterns.CollectionChanged += OnEditingCopyPropertyChanged;
            if (rule.TargetSources != null)
            {
                rule.TargetSources.CollectionChanged += OnSourcesCollectionChanged;
                foreach (var source in rule.TargetSources) ListenToSource(source);
            }
        }

        private void StopListeningToRule(DnsMappingRule rule)
        {
            rule.PropertyChanged -= OnEditingCopyPropertyChanged;
            if (rule.DomainPatterns != null)
                rule.DomainPatterns.CollectionChanged -= OnEditingCopyPropertyChanged;
            if (rule.TargetSources != null)
            {
                rule.TargetSources.CollectionChanged -= OnSourcesCollectionChanged;
                foreach (var source in rule.TargetSources) StopListeningToSource(source);
            }
        }

        private void ListenToSource(TargetIpSource source)
        {
            source.PropertyChanged += OnEditingCopyPropertyChanged;

            if (source.Addresses != null)
                source.Addresses.CollectionChanged += OnEditingCopyPropertyChanged;

            if (source.FallbackIpAddresses != null)
            {
                source.FallbackIpAddresses.CollectionChanged += OnFallbackAddressesCollectionChanged;
                foreach (var address in source.FallbackIpAddresses) ListenToFallbackAddress(address);
            }
        }

        private void StopListeningToSource(TargetIpSource source)
        {
            source.PropertyChanged -= OnEditingCopyPropertyChanged;

            if (source.Addresses != null)
                source.Addresses.CollectionChanged -= OnEditingCopyPropertyChanged;

            if (source.FallbackIpAddresses != null)
            {
                source.FallbackIpAddresses.CollectionChanged -= OnFallbackAddressesCollectionChanged;
                foreach (var address in source.FallbackIpAddresses) StopListeningToFallbackAddress(address);
            }
        }

        private void ListenToFallbackAddress(FallbackAddress address) =>
            address.PropertyChanged += OnEditingCopyPropertyChanged;

        private void StopListeningToFallbackAddress(FallbackAddress address) =>
            address.PropertyChanged -= OnEditingCopyPropertyChanged;

        private void OnGroupsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null) foreach (DnsMappingGroup item in e.NewItems) ListenToGroup(item);
            if (e.OldItems != null) foreach (DnsMappingGroup item in e.OldItems) StopListeningToGroup(item);
            if (CurrentState == EditingState.None) TransitionToState(EditingState.Editing);
            ValidateEditingCopy();
        }

        private void OnRulesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null) foreach (DnsMappingRule item in e.NewItems) ListenToRule(item);
            if (e.OldItems != null) foreach (DnsMappingRule item in e.OldItems) StopListeningToRule(item);
            if (CurrentState == EditingState.None) TransitionToState(EditingState.Editing);
            ValidateEditingCopy();
        }

        private void OnSourcesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null) foreach (TargetIpSource item in e.NewItems) ListenToSource(item);
            if (e.OldItems != null) foreach (TargetIpSource item in e.OldItems) StopListeningToSource(item);
            if (CurrentState == EditingState.None) TransitionToState(EditingState.Editing);
            OnEditingCopyPropertyChanged(sender, e);
        }

        private void OnFallbackAddressesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null) foreach (FallbackAddress item in e.NewItems) ListenToFallbackAddress(item);
            if (e.OldItems != null) foreach (FallbackAddress item in e.OldItems) StopListeningToFallbackAddress(item);
            if (CurrentState == EditingState.None) TransitionToState(EditingState.Editing);
            ValidateEditingCopy();
        }

        private void OnEditingCopyPropertyChanged(object sender, EventArgs e)
        {
            if (CurrentState == EditingState.None)
                TransitionToState(EditingState.Editing);
            ValidateEditingCopy();
        }

        private void ValidateEditingCopy()
        {
            if (EditingTableCopy == null) ValidationErrors = ValidationWarnings = null;
            else
            {
                var result = _configValidator.Validate(EditingTableCopy);
                if (result.IsValid) ValidationErrors = ValidationWarnings = null;
                else
                {
                    var errors = new List<ValidationErrorNode>();

                    foreach (var error in result.Errors.Where(e => e.Severity == Severity.Error))
                    {
                        if (error.CustomState is ValidationErrorNode structuredError)
                        {
                            structuredError.Depth = 0;
                            errors.Add(structuredError);
                        }
                        else
                        {
                            errors.Add(new ValidationErrorNode
                            {
                                Message = error.ErrorMessage,
                                Depth = 0
                            });
                        }
                    }

                    ValidationErrors = errors;

                    var warnings = new List<ValidationErrorNode>();

                    foreach (var warn in result.Errors.Where(e => e.Severity == Severity.Warning))
                    {
                        if (warn.CustomState is ValidationErrorNode structuredError)
                        {
                            structuredError.Depth = 0;
                            warnings.Add(structuredError);
                        }
                        else
                        {
                            warnings.Add(new ValidationErrorNode
                            {
                                Message = warn.ErrorMessage,
                                Depth = 0
                            });
                        }
                    }

                    ValidationWarnings = warnings;
                }
            }
            OnPropertyChanged(nameof(ValidationWarnings), nameof(HasValidationWarnings),
                nameof(ValidationErrors), nameof(HasValidationErrors));
            UpdateCommandStates();
        }
        #endregion

        #region Save & Discard Changes
        private async Task ExecuteSaveChangesAsync()
        {
            _isBusy = true;
            ValidateEditingCopy();

            if (HasValidationErrors)
            {
                var errorItems = ValidationErrors.Select(e => e.ToBulletedListItem());
                await _dialogService.ShowInfoAsync("保存失败", errorItems, "请更正以下信息：");
                _isBusy = false;
                UpdateCommandStates();
                return;
            }

            try
            {
                if (_currentState == EditingState.Creating)
                {
                    var newTable = EditingTableCopy;
                    _tableService.AllConfigs.Add(newTable);
                    await _tableService.SaveChangesAsync(newTable);
                    var newTableVM = AllTableVMs.FirstOrDefault(vm => vm.Model == newTable);
                    SwitchToTable(newTableVM);
                }
                else if (_currentState == EditingState.Editing)
                {
                    _originalTable.UpdateFrom(EditingTableCopy);
                    await _tableService.SaveChangesAsync(_originalTable);
                    TransitionToState(EditingState.None);
                }
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }

        private void ExecuteDiscardChanges()
        {
            if (_currentState == EditingState.Creating) SwitchToTable(AllTableVMs.FirstOrDefault());
            else
            {
                EditingTableCopy = _originalTable?.Clone();
                TransitionToState(EditingState.None);
            }
        }

        private bool CanExecuteSave() => CanExecuteWhenDirty() && !HasValidationErrors;

        private bool CanExecuteWhenDirty() => CurrentState != EditingState.None && !_isBusy;

        private async Task<SaveChangesResult> PromptToSaveChangesAndContinueAsync()
        {
            var message = _currentState == EditingState.Creating
                ? "您新建的配置尚未保存，要保存吗？"
                : $"您对配置 “{EditingTableCopy.TableName}” 的更改尚未保存。要保存吗？";

            var result = await _dialogService.ShowSaveChangesDialogAsync("未保存的更改", message);
            switch (result)
            {
                case SaveChangesResult.Save:
                    await ExecuteSaveChangesAsync();
                    return _currentState == EditingState.None
                        ? SaveChangesResult.Save
                        : SaveChangesResult.Cancel;

                case SaveChangesResult.Discard:
                    ExecuteDiscardChanges();
                    return SaveChangesResult.Discard;

                default:
                    return SaveChangesResult.Cancel;
            }
        }
        #endregion

        #region Group Management
        private async Task ExecuteAddNewGroupAsync()
        {
            _isBusy = true;
            UpdateCommandStates();

            try
            {
                var newName = await _dialogService.ShowTextInputAsync("新建映射组", "请输入新映射组的名称：", "新映射组");
                if (newName != null)
                {
                    if (string.IsNullOrWhiteSpace(newName))
                        await _dialogService.ShowInfoAsync("创建失败", "映射组名称不能为空！");
                    else if (EditingTableCopy.MappingGroups.Select(r => r.GroupName).Contains(newName))
                        await _dialogService.ShowInfoAsync("创建失败", $"已存在名为 “{newName}” 的映射组！");
                    else
                    {
                        var newGroupModel = _groupFactory.CreateDefault();
                        newGroupModel.GroupName = newName;
                        EditingTableCopy.MappingGroups.Add(newGroupModel);

                        var newGroupVM = EditingTableVM.MappingGroups.FirstOrDefault(vm => vm.Model == newGroupModel);
                        if (newGroupVM != null) SelectedTreeItem = newGroupVM;
                    }
                }
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }

        private void ExecuteRemoveSelectedGroup()
        {
            if (SelectedTreeItem is DnsMappingGroupViewModel groupVM)
            {
                var groupModel = groupVM.Model;
                int index = EditingTableCopy.MappingGroups.IndexOf(groupModel);

                EditingTableCopy.MappingGroups.Remove(groupModel);

                SelectedTreeItem = EditingTableVM.MappingGroups.Any()
                    ? EditingTableVM.MappingGroups[Math.Max(0, index - 1)]
                    : null;
            }
        }

        private async Task ExecuteRenameSelectedGroupAsync()
        {
            if (SelectedTreeItem is not DnsMappingGroupViewModel groupVM) return;

            _isBusy = true;
            UpdateCommandStates();

            try
            {
                var group = groupVM.Model;

                var newName = await _dialogService.ShowTextInputAsync($"重命名 “{group.GroupName}”", "请输入新的映射组名称：", group.GroupName);
                if (newName != null && newName != group.GroupName)
                {

                    if (string.IsNullOrWhiteSpace(newName))
                        await _dialogService.ShowInfoAsync("重命名失败", "映射表名称不能为空！");
                    else if (EditingTableCopy.MappingGroups.Select(r => r.GroupName).Contains(newName))
                        await _dialogService.ShowInfoAsync("重命名失败", $"已存在名为 “{newName}” 的映射组！");
                    else group.GroupName = newName;
                }
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }

        }

        private void ExecuteMoveSelectedGroupUp()
        {
            if (SelectedTreeItem is not DnsMappingGroupViewModel groupVM) return;

            var group = groupVM.Model;

            int index = EditingTableCopy.MappingGroups.IndexOf(group);
            if (index > 0) EditingTableCopy.MappingGroups.Move(index, index - 1);
        }

        private void ExecuteMoveSelectedGroupDown()
        {
            if (SelectedTreeItem is not DnsMappingGroupViewModel groupVM) return;

            var group = groupVM.Model;

            int index = EditingTableCopy.MappingGroups.IndexOf(group);
            if (index < EditingTableCopy.MappingGroups.Count - 1)
                EditingTableCopy.MappingGroups.Move(index, index + 1);
        }

        private bool CanExecuteAddNewGroup() => EditingTableCopy != null && !_isBusy;

        private bool CanExecuteOnSelectedGroup() => IsGroupSelected && !_isBusy;

        private bool CanExecuteMoveSelectedGroupUp() =>
            CanExecuteOnSelectedGroup() &&
            SelectedTreeItem is DnsMappingGroupViewModel groupVM &&
            EditingTableCopy.MappingGroups.IndexOf(groupVM.Model) > 0;

        private bool CanExecuteMoveSelectedGroupDown() =>
            CanExecuteOnSelectedGroup() &&
            SelectedTreeItem is DnsMappingGroupViewModel groupVM &&
            EditingTableCopy.MappingGroups.IndexOf(groupVM.Model) < EditingTableCopy.MappingGroups.Count - 1;
        #endregion

        #region Rule Management
        private void ExecuteAddNewRule()
        {
            if (FindParentGroupViewModel(SelectedTreeItem) is not DnsMappingGroupViewModel targetGroupVM) return;

            var newRuleModel = _ruleFactory.CreateDefault();
            targetGroupVM.Model.MappingRules.Add(newRuleModel);

            var newRuleVM = targetGroupVM.MappingRules.FirstOrDefault(vm => vm.Model == newRuleModel);
            SelectedTreeItem = newRuleVM;
        }

        private void ExecuteRemoveSelectedRule()
        {
            if (SelectedTreeItem is not DnsMappingRuleViewModel ruleVM) return;

            var parentVM = ruleVM.Parent;
            if (parentVM == null) return;

            var parentModel = parentVM.Model;
            var ruleModel = ruleVM.Model;
            int index = parentModel.MappingRules.IndexOf(ruleModel);

            parentModel.MappingRules.Remove(ruleModel);

            SelectedTreeItem = parentVM.MappingRules.Any()
                ? parentVM.MappingRules[Math.Max(0, index - 1)]
                : parentVM;
        }

        private void ExecuteMoveSelectedRuleUp()
        {
            if (SelectedTreeItem is not DnsMappingRuleViewModel ruleVM) return;

            var currentGroupVM = ruleVM.Parent;
            if (currentGroupVM == null) return;

            var currentGroupModel = currentGroupVM.Model;
            var ruleModel = ruleVM.Model;
            int ruleIndex = currentGroupModel.MappingRules.IndexOf(ruleModel);

            if (ruleIndex > 0) currentGroupModel.MappingRules.Move(ruleIndex, ruleIndex - 1);
            else if (EditingTableVM.MappingGroups.IndexOf(currentGroupVM) > 0)
            {
                int currentGroupIndex = EditingTableVM.MappingGroups.IndexOf(currentGroupVM);

                var previousGroupVM = EditingTableVM.MappingGroups[currentGroupIndex - 1];
                var previousGroupModel = previousGroupVM.Model;

                currentGroupModel.MappingRules.Remove(ruleModel);
                previousGroupModel.MappingRules.Add(ruleModel);

                var newRuleVM = previousGroupVM.MappingRules.FirstOrDefault(vm => vm.Model == ruleModel);
                SelectedTreeItem = newRuleVM;
            }
        }

        private void ExecuteMoveSelectedRuleDown()
        {
            if (SelectedTreeItem is not DnsMappingRuleViewModel ruleVM) return;

            var currentGroupVM = ruleVM.Parent;
            if (currentGroupVM == null) return;

            var currentGroupModel = currentGroupVM.Model;
            var ruleModel = ruleVM.Model;
            int ruleIndex = currentGroupModel.MappingRules.IndexOf(ruleModel);

            if (ruleIndex < currentGroupModel.MappingRules.Count - 1)
                currentGroupModel.MappingRules.Move(ruleIndex, ruleIndex + 1);
            else if (EditingTableVM.MappingGroups.IndexOf(currentGroupVM) < EditingTableVM.MappingGroups.Count - 1)
            {
                int currentGroupIndex = EditingTableVM.MappingGroups.IndexOf(currentGroupVM);

                var nextGroupVM = EditingTableVM.MappingGroups[currentGroupIndex + 1];
                var nextGroupModel = nextGroupVM.Model;

                currentGroupModel.MappingRules.Remove(ruleModel);
                nextGroupModel.MappingRules.Insert(0, ruleModel);

                var newRuleVM = nextGroupVM.MappingRules.FirstOrDefault(vm => vm.Model == ruleModel);
                SelectedTreeItem = newRuleVM;
            }
        }

        private bool CanExecuteAddRule() => (IsGroupSelected || IsRuleSelected) && !_isBusy;

        private bool CanExecuteOnSelectedRule() => IsRuleSelected && !_isBusy;

        private bool CanExecuteMoveSelectedRuleUp()
        {
            if (!CanExecuteOnSelectedRule()) return false;

            var ruleVM = SelectedTreeItem as DnsMappingRuleViewModel;

            var currentGroup = FindParentGroupViewModel(ruleVM).Model;
            if (currentGroup == null) return false;

            int currentGroupIndex = EditingTableCopy.MappingGroups.IndexOf(currentGroup);
            int ruleIndex = currentGroup.MappingRules.IndexOf(ruleVM.Model);

            // 不是当前组的第一个规则，或者不是第一个组
            return ruleIndex > 0 || currentGroupIndex > 0;
        }

        private bool CanExecuteMoveSelectedRuleDown()
        {
            if (!CanExecuteOnSelectedRule()) return false;

            var ruleVM = SelectedTreeItem as DnsMappingRuleViewModel;

            var currentGroup = FindParentGroupViewModel(ruleVM).Model;
            if (currentGroup == null) return false;

            int currentGroupIndex = EditingTableCopy.MappingGroups.IndexOf(currentGroup);
            int ruleIndex = currentGroup.MappingRules.IndexOf(ruleVM.Model);
            int ruleCount = currentGroup.MappingRules.Count;

            // 不是当前组的最后一个规则，或者不是最后一个组
            return ruleIndex < ruleCount - 1 || currentGroupIndex < EditingTableCopy.MappingGroups.Count - 1;
        }
        #endregion

        #region Domain Pattern Management
        private async Task ExecuteAddDomainPatternAsync()
        {
            if (SelectedTreeItem is not DnsMappingRuleViewModel ruleVM) return;

            _isBusy = true;
            UpdateCommandStates();

            try
            {
                var rule = ruleVM.Model;

                var newPattern = await _dialogService.ShowTextInputAsync("添加模式", "请输入新的域名匹配模式：");
                if (newPattern != null)
                {
                    if (!string.IsNullOrWhiteSpace(newPattern))
                    {
                        if (newPattern.Trim().StartsWith("/"))
                        {
                            var regexContent = newPattern.Substring(1).Trim();
                            if (string.IsNullOrEmpty(regexContent))
                            {
                                await _dialogService.ShowInfoAsync("添加失败", $"“{newPattern}” 不是有效的域名匹配模式，正则表达式不能为空。");
                                return;
                            }
                            int pcreOptions = PcreOptions.PCRE_UTF8 | PcreOptions.PCRE_CASELESS;
                            if (!PcreRegex.TryValidatePattern(regexContent, pcreOptions, out string errorMessage, out int errorOffset))
                            {
                                await _dialogService.ShowInfoAsync("添加失败", $"“{newPattern}” 不是有效的域名匹配模式，正则表达式在位置 {errorOffset} 存在错误 “{errorMessage}”。");
                                return;
                            }
                        }
                        else
                        {
                            var trimmed = newPattern.Trim();
                            if (trimmed.StartsWith(">"))
                            {
                                if (trimmed.IndexOf('>', 1) >= 0)
                                {
                                    await _dialogService.ShowInfoAsync("添加失败", $"“{newPattern}” 不是有效的域名匹配模式，子域匹配符号 “>” 只能出现在模式开头。");
                                    return;
                                }
                                if (trimmed.Length == 1)
                                {
                                    await _dialogService.ShowInfoAsync("添加失败", $"“{newPattern}” 不是有效的域名匹配模式，子域匹配符号 “>” 后必须提供域名模式。");
                                    return;
                                }
                            }
                        }
                        if (rule.DomainPatterns.Contains(newPattern))
                        {
                            await _dialogService.ShowInfoAsync("添加失败", $"域名匹配模式 “{newPattern}” 已存在！");
                            return;
                        }
                        rule.DomainPatterns.Add(newPattern);
                    }
                    else await _dialogService.ShowInfoAsync("添加失败", "域名匹配模式不能为空！");
                }

            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }

        private async Task ExecuteEditDomainPatternAsync(string pattern)
        {
            if (SelectedTreeItem is not DnsMappingRuleViewModel ruleVM || pattern == null) return;

            _isBusy = true;
            UpdateCommandStates();

            try
            {
                var rule = ruleVM.Model;

                var newPattern = await _dialogService.ShowTextInputAsync($"编辑 “{pattern}”", "请输入新的域名匹配模式：", pattern);
                if (newPattern != null && newPattern != pattern)
                {
                    if (!string.IsNullOrWhiteSpace(newPattern))
                    {
                        if (newPattern.Trim().StartsWith("/"))
                        {
                            var regexContent = newPattern.Substring(1).Trim();
                            if (string.IsNullOrEmpty(regexContent))
                            {
                                await _dialogService.ShowInfoAsync("编辑失败", $"“{newPattern}” 不是有效的域名匹配模式，正则表达式不能为空。");
                                return;
                            }
                            int pcreOptions = PcreOptions.PCRE_UTF8 | PcreOptions.PCRE_CASELESS;
                            if (!PcreRegex.TryValidatePattern(regexContent, pcreOptions, out string errorMessage, out int errorOffset))
                            {
                                await _dialogService.ShowInfoAsync("编辑失败", $"“{newPattern}” 不是有效的域名匹配模式，正则表达式在位置 {errorOffset} 存在错误 “{errorMessage}”。");
                                return;
                            }
                        }
                        else
                        {
                            var trimmed = newPattern.Trim();
                            if (trimmed.StartsWith(">"))
                            {
                                if (trimmed.IndexOf('>', 1) >= 0)
                                {
                                    await _dialogService.ShowInfoAsync("编辑失败", $"“{newPattern}” 不是有效的域名匹配模式，子域匹配符号 “>” 只能出现在模式开头。");
                                    return;
                                }
                                if (trimmed.Length == 1)
                                {
                                    await _dialogService.ShowInfoAsync("编辑失败", $"“{newPattern}” 不是有效的域名匹配模式，子域匹配符号 “>” 后必须提供域名模式。");
                                    return;
                                }
                            }
                        }
                        if (rule.DomainPatterns.Contains(newPattern))
                        {
                            await _dialogService.ShowInfoAsync("编辑失败", $"域名匹配模式 “{newPattern}” 已存在！");
                            return;
                        }
                        int index = rule.DomainPatterns.IndexOf(pattern);
                        if (index >= 0) rule.DomainPatterns[index] = newPattern;
                    }
                    else await _dialogService.ShowInfoAsync("编辑失败", "域名匹配模式不能为空！");
                }
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }

        private void ExecuteDeleteDomainPattern(string pattern)
        {
            if (pattern is null) return;
            else if (SelectedTreeItem is DnsMappingRuleViewModel ruleVM)
            {
                var rule = ruleVM.Model;
                if (rule.DomainPatterns.Contains(pattern))
                    rule.DomainPatterns.Remove(pattern);
            }
        }

        private bool CanExecuteEditDomainPattern(string pattern) =>
            pattern != null && !_isBusy;
        #endregion

        #region Source Management
        private void ExecuteAddNewSource()
        {
            if (SelectedTreeItem is not DnsMappingRuleViewModel ruleVM) return;

            var newSource = _sourceFactory.CreateDefault();
            ruleVM.Model.TargetSources.Add(newSource);
            SelectedSource = newSource;
        }

        private void ExecuteRemoveSelectedSource()
        {
            if (SelectedSource == null) return;
            if (SelectedTreeItem is not DnsMappingRuleViewModel ruleVM) return;

            var rule = ruleVM.Model;
            int index = rule.TargetSources.IndexOf(SelectedSource);
            rule.TargetSources.Remove(SelectedSource);
            SelectedSource = rule.TargetSources.Any() ? rule.TargetSources[Math.Max(0, index - 1)] : null;
        }

        private void ExecuteMoveSelectedSourceUp()
        {
            if (SelectedSource == null) return;
            if (SelectedTreeItem is not DnsMappingRuleViewModel ruleVM) return;

            var rule = ruleVM.Model;
            int index = rule.TargetSources.IndexOf(SelectedSource);
            if (index > 0) rule.TargetSources.Move(index, index - 1);
        }

        private void ExecuteMoveSelectedSourceDown()
        {
            if (SelectedSource == null) return;
            if (SelectedTreeItem is not DnsMappingRuleViewModel ruleVM) return;

            var rule = ruleVM.Model;
            int index = rule.TargetSources.IndexOf(SelectedSource);
            if (index < rule.TargetSources.Count - 1) 
                rule.TargetSources.Move(index, index + 1);
        }

        private bool CanExecuteAddNewSource() => !_isBusy && IsRuleSelected;

        private bool CanExecuteOnSelectedSource() => SelectedSource != null && !_isBusy;

        private bool CanExecuteMoveSelectedSourceUp()
        {
            if (!CanExecuteOnSelectedSource() || SelectedTreeItem is not DnsMappingRuleViewModel ruleVM)
                return false;

            var sources = ruleVM.Model.TargetSources;
            int index = sources.IndexOf(SelectedSource);
            return index > 0;
        }

        private bool CanExecuteMoveSelectedSourceDown()
        {
            if (!CanExecuteOnSelectedSource() || SelectedTreeItem is not DnsMappingRuleViewModel ruleVM)
                return false;

            var sources = ruleVM.Model.TargetSources;
            int index = sources.IndexOf(SelectedSource);
            return index < sources.Count - 1;
        }
        #endregion

        #region Address Management
        private async Task ExecuteAddAddressAsync()
        {
            if (SelectedSource is null) return;

            _isBusy = true;
            UpdateCommandStates();

            try
            {
                var newIp = await _dialogService.ShowTextInputAsync("添加目标地址", "请输入 IP 地址：");
                if (newIp != null)
                {
                    string trimmed = newIp.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed))
                        await _dialogService.ShowInfoAsync("添加失败", "目标地址不能为空！");
                    else if (!NetworkUtils.IsValidIP(trimmed))
                        await _dialogService.ShowInfoAsync("添加失败", $"“{trimmed}” 不是合法的 IP 地址！");
                    else if (SelectedSource.Addresses.Contains(trimmed))
                        await _dialogService.ShowInfoAsync("添加失败", $"目标地址 “{trimmed}” 已存在！");
                    else SelectedSource.Addresses.Add(trimmed);
                }
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }

        private void ExecuteDeleteAddress(string address)
        {
            if (SelectedSource == null || address == null) return;
            if (SelectedSource.Addresses.Contains(address))
                SelectedSource.Addresses.Remove(address);
        }

        private async Task ExecuteDeleteAllAddressesAsync()
        {
            if (SelectedSource == null || !SelectedSource.Addresses.Any()) return;

            var confirmResult = await _dialogService.ShowConfirmationAsync("确认删除", "您确定要删除此来源的所有目标地址吗？", "删除");
            if (!confirmResult) return;
            SelectedSource.Addresses.Clear();
        }

        private void ExecuteMoveAddressUp(string address)
        {
            if (SelectedSource == null || address == null) return;

            int index = SelectedSource.Addresses.IndexOf(address);
            if (index > 0) SelectedSource.Addresses.Move(index, index - 1);
        }

        private void ExecuteMoveAddressDown(string address)
        {
            if (SelectedSource == null || address == null) return;

            int index = SelectedSource.Addresses.IndexOf(address);
            if (index < SelectedSource.Addresses.Count - 1)
                SelectedSource.Addresses.Move(index, index + 1);
        }

        private async Task ExecuteEditAddressAsync(string address)
        {
            if (SelectedSource == null || address == null) return;

            _isBusy = true;
            UpdateCommandStates();

            try
            {
                var newIp = await _dialogService.ShowTextInputAsync($"编辑 “{address}”", "请输入新的目标地址：", address);
                if (newIp != null && newIp != address)
                {
                    string trimmed = newIp.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed))
                        await _dialogService.ShowInfoAsync("编辑失败", "目标地址不能为空！");
                    else if (!NetworkUtils.IsValidIP(trimmed))
                        await _dialogService.ShowInfoAsync("编辑失败", $"“{trimmed}” 不是合法的 IP 地址！");
                    else if (SelectedSource.Addresses.Contains(trimmed))
                        await _dialogService.ShowInfoAsync("编辑失败", $"目标地址 “{trimmed}” 已存在！");
                    else
                    {
                        var addresses = SelectedSource.Addresses;
                        int index = addresses.IndexOf(address);
                        if (index >= 0) addresses[index] = trimmed;
                    }
                }
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }

        private bool CanExecuteMoveAddressUp(string address)
        {
            if (!CanExecuteOnSelectedSource() || SelectedTreeItem is not DnsMappingRuleViewModel)
                return false;

            var addresses = SelectedSource.Addresses;
            int index = addresses.IndexOf(address);
            return index > 0;
        }

        private bool CanExecuteMoveAddressDown(string address)
        {
            if (!CanExecuteOnSelectedSource() || SelectedTreeItem is not DnsMappingRuleViewModel)
                return false;

            var addresses = SelectedSource.Addresses;
            int index = addresses.IndexOf(address);
            return index < addresses.Count - 1;
        }

        private bool CanExecuteDeleteAllAddresses() =>
            CanExecuteOnSelectedSource() && SelectedSource.Addresses.Any();
        #endregion

        #region Resolver Link Management
        private async Task ExecutePasteResolverLinkCodeAsync()
        {
            if (SelectedSource is null) return;

            _isBusy = true;
            UpdateCommandStates();

            try
            {
                var linkCode = Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(linkCode)) return;

                var idString = Base64Utils.DecodeString(linkCode);
                if (idString == null) return;
                else if (Guid.TryParse(idString, out Guid resolverId))
                {
                    var resolver = _resolverService.AllConfigs.FirstOrDefault(r => r.Id == resolverId);
                    if (resolver != null)
                    {
                        SelectedSource.ResolverId = resolver.Id;
                        UpdateAssociatedResolverName();
                    }
                    else await _dialogService.ShowInfoAsync("关联失败", "未找到对应关联码的解析器。");
                }
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }

        private void ExecuteUnlinkResolver()
        {
            if (SelectedSource == null) return;

            SelectedSource.ResolverId = null;
            UpdateAssociatedResolverName();
        }

        private void UpdateAssociatedResolverName()
        {
            if (SelectedSource != null && SelectedSource.ResolverId.HasValue)
            {
                var resolver = _resolverService.AllConfigs.FirstOrDefault(r => r.Id == SelectedSource.ResolverId.Value);
                AssociatedResolverName = resolver != null ? resolver.ResolverName : "关联已失效";
            }
            else AssociatedResolverName = string.Empty;
        }
        #endregion

        #region Query Domain Management
        private async Task ExecuteAddQueryDomainAsync()
        {
            if (SelectedSource is null) return;

            _isBusy = true;
            UpdateCommandStates();

            try
            {
                var newDomain = await _dialogService.ShowTextInputAsync("添加域名", "请输入新的查询域名：");
                if (newDomain != null)
                {
                    var trimmed = newDomain.Trim();

                    if (!string.IsNullOrWhiteSpace(trimmed))
                    {
                        if (!NetworkUtils.IsValidDomain(trimmed))
                            await _dialogService.ShowInfoAsync("添加失败", $"“{trimmed}” 不是有效的查询域名，应符合 RFC 1035、RFC 1123 及国际化域名规范。");
                        else if (SelectedSource.QueryDomains.Contains(trimmed))
                            await _dialogService.ShowInfoAsync("添加失败", $"查询域名 “{trimmed}” 已存在！");
                        else SelectedSource.QueryDomains.Add(trimmed);
                    }
                    else await _dialogService.ShowInfoAsync("添加失败", "查询域名不能为空！");
                }
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }

        private async Task ExecuteEditQueryDomainAsync(string domain)
        {
            if (SelectedSource is null || domain is null) return;

            _isBusy = true;
            UpdateCommandStates();

            try
            {
                var newDomain = await _dialogService.ShowTextInputAsync($"编辑 “{domain}”", "请输入新的查询域名：", domain);
                if (newDomain != null && newDomain != domain)
                {
                    string trimmed = newDomain.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed))
                        await _dialogService.ShowInfoAsync("编辑失败", "查询域名不能为空！");
                    else if (!NetworkUtils.IsValidDomain(trimmed))
                        await _dialogService.ShowInfoAsync("编辑失败", $"“{trimmed}” 不是合法的查询域名，应符合 RFC 1035、RFC 1123 及国际化域名规范。");
                    else if (SelectedSource.QueryDomains.Contains(trimmed))
                        await _dialogService.ShowInfoAsync("编辑失败", $"查询域名 “{trimmed}” 已存在！");
                    else
                    {
                        var domains = SelectedSource.QueryDomains;
                        int index = domains.IndexOf(domain);
                        if (index >= 0) domains[index] = trimmed;
                    }
                }
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }

        private void ExecuteDeleteQueryDomain(string domain)
        {
            if (SelectedSource is null || domain is null) return;
            if (SelectedSource.QueryDomains.Contains(domain))
                SelectedSource.QueryDomains.Remove(domain);
        }

        private bool CanExecuteEditQueryDomain(string domain) =>
            domain != null && !_isBusy;
        #endregion

        #region Fallback Address Management
        private async Task ExecuteAddFallbackAddressAsync()
        {
            if (SelectedSource is null) return;

            _isBusy = true;
            UpdateCommandStates();

            try
            {
                var newIp = await _dialogService.ShowTextInputAsync("添加回落地址", "请输入 IP 地址：");
                if (newIp != null)
                {
                    string trimmed = newIp.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed))
                        await _dialogService.ShowInfoAsync("添加失败", "回落地址不能为空！");
                    else if (!NetworkUtils.IsValidIP(trimmed))
                        await _dialogService.ShowInfoAsync("添加失败", $"“{trimmed}” 不是合法的 IP 地址！");
                    else if (SelectedSource.FallbackIpAddresses.Select(f => f.Address).Contains(trimmed))
                        await _dialogService.ShowInfoAsync("添加失败", $"回落地址 “{trimmed}” 已存在！");
                    else
                    {
                        var newAddress = _addressFactory.CreateDefault();
                        newAddress.Address = trimmed;
                        SelectedSource.FallbackIpAddresses.Add(newAddress);
                    }
                }
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }

        private void ExecuteDeleteFallbackAddress(FallbackAddress address)
        {
            if (SelectedSource == null || address == null) return;

            if (SelectedSource.FallbackIpAddresses.Contains(address))
                SelectedSource.FallbackIpAddresses.Remove(address);
        }

        private async Task ExecuteEditFallbackAddressAsync(FallbackAddress address)
        {
            if (SelectedSource == null || address == null) return;

            _isBusy = true;
            UpdateCommandStates();

            try
            {
                var newIp = await _dialogService.ShowTextInputAsync($"编辑 “{address.Address}”", "请输入新的回落地址：", address.Address);
                if (newIp != null && newIp != address.Address)
                {
                    string trimmed = newIp.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed))
                        await _dialogService.ShowInfoAsync("编辑失败", "回落地址不能为空！");
                    else if (!NetworkUtils.IsValidIP(trimmed))
                        await _dialogService.ShowInfoAsync("编辑失败", $"“{trimmed}” 不是合法的 IP 地址！");
                    else if (SelectedSource.FallbackIpAddresses.Select(f => f.Address).Contains(trimmed))
                        await _dialogService.ShowInfoAsync("编辑失败", $"回落地址 “{trimmed}” 已存在！");
                    else
                    {
                        var addresses = SelectedSource.FallbackIpAddresses;
                        int index = addresses.IndexOf(address);
                        if (index >= 0) addresses[index].Address = trimmed;
                    }
                }
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }

        private async Task ExecuteDeleteAllFallbackAddressesAsync()
        {
            if (SelectedSource == null || !SelectedSource.FallbackIpAddresses.Any()) return;

            var confirmResult = await _dialogService.ShowConfirmationAsync("确认删除", "您确定要删除此来源的所有回落地址吗？", "删除");
            if (!confirmResult) return;
            SelectedSource.FallbackIpAddresses.Clear();
        }

        private void ExecuteMoveFallbackAddressUp(FallbackAddress address)
        {
            if (SelectedSource == null || address == null) return;

            int index = SelectedSource.FallbackIpAddresses.IndexOf(address);
            if (index > 0) SelectedSource.FallbackIpAddresses.Move(index, index - 1);
        }

        private void ExecuteMoveFallbackAddressDown(FallbackAddress address)
        {
            if (SelectedSource == null || address == null) return;

            int index = SelectedSource.FallbackIpAddresses.IndexOf(address);
            if (index < SelectedSource.FallbackIpAddresses.Count - 1)
                SelectedSource.FallbackIpAddresses.Move(index, index + 1);
        }

        private void ExecuteLockFallbackAddress(FallbackAddress address)
        {
            if (SelectedSource == null || address == null) return;

            int index = SelectedSource.FallbackIpAddresses.IndexOf(address);
            if (index >= 0) SelectedSource.FallbackIpAddresses[index].IsLocked = true;
        }

        private void ExecuteUnlockFallbackAddress(FallbackAddress address)
        {
            if (SelectedSource == null || address == null) return;

            int index = SelectedSource.FallbackIpAddresses.IndexOf(address);
            if (index >= 0) SelectedSource.FallbackIpAddresses[index].IsLocked = false;
        }

        private bool CanExecuteMoveFallbackAddressUp(FallbackAddress address)
        {
            if (!CanExecuteOnSelectedSource() || SelectedTreeItem is not DnsMappingRuleViewModel)
                return false;

            var addresses = SelectedSource.FallbackIpAddresses;
            int index = addresses.IndexOf(address);
            return index > 0;
        }

        private bool CanExecuteMoveFallbackAddressDown(FallbackAddress address)
        {
            if (!CanExecuteOnSelectedSource() || SelectedTreeItem is not DnsMappingRuleViewModel)
                return false;

            var addresses = SelectedSource.FallbackIpAddresses;
            int index = addresses.IndexOf(address);
            return index < addresses.Count - 1;
        }

        private bool CanExecuteLockFallbackAddress(FallbackAddress address) =>
            CanExecuteOnSelectedSource() && SelectedSource.FallbackIpAddresses.Contains(address) && !address.IsLocked;

        private bool CanExecuteUnlockFallbackAddress(FallbackAddress address) =>
            CanExecuteOnSelectedSource() && SelectedSource.FallbackIpAddresses.Contains(address) && address.IsLocked;

        private bool CanExecuteDeleteAllFallbackAddresses() =>
            CanExecuteOnSelectedSource() && SelectedSource.FallbackIpAddresses.Any();
        #endregion

        #region Group Icon Management
        private async Task ExecuteAddGroupIconAsync(DnsMappingGroupViewModel groupVM)
        {
            if (groupVM == null) return;

            _isBusy = true;
            UpdateCommandStates();

            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Title = "选择要为映射组设置的图标",
                    Filter = "图片文件 (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|所有文件 (*.*)|*.*",
                    FilterIndex = 1,
                    RestoreDirectory = true
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    try
                    {
                        byte[] imageBytes = await FileUtils.ReadAllBytesAsync(openFileDialog.FileName);
                        string base64String = Base64Utils.EncodeBytes(imageBytes);
                        groupVM.Model.GroupIconBase64 = base64String;
                    }
                    catch (Exception ex)
                    {
                        await _dialogService.ShowInfoAsync("设置失败", $"图片文件读取失败：\n{ex.Message}");
                    }
                }
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }

        private bool CanExecuteAddGroupIcon(DnsMappingGroupViewModel groupVM) => string.IsNullOrEmpty(groupVM.Model.GroupIconBase64) && !_isBusy;

        private void ExecuteRemoveGroupIcon(DnsMappingGroupViewModel groupVM)
        {
            if (groupVM != null) groupVM.Model.GroupIconBase64 = string.Empty;
            UpdateCommandStates();
        }

        private bool CanExecuteRemoveGroupIcon(DnsMappingGroupViewModel groupVM) => !string.IsNullOrEmpty(groupVM.Model.GroupIconBase64) && !_isBusy;
        #endregion
        #endregion

        #region External Event Handlers
        private void OnAllTablesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    int insertIndex = Math.Min(e.NewStartingIndex, _allTableVMs.Count);
                    foreach (DnsMappingTable model in e.NewItems)
                    {
                        _allTableVMs.Insert(insertIndex, new DnsMappingTableViewModel(model, DoesResolverRequireIPv6));
                        insertIndex++;
                    }
                    break;

                case NotifyCollectionChangedAction.Remove:
                    foreach (DnsMappingTable model in e.OldItems)
                    {
                        var vmToRemove = _allTableVMs.FirstOrDefault(vm => vm.Model == model);
                        if (vmToRemove != null)
                        {
                            vmToRemove.Dispose();
                            _allTableVMs.Remove(vmToRemove);
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Replace:
                    for (int i = 0; i < e.OldItems.Count; i++)
                    {
                        var oldModel = (DnsMappingTable)e.OldItems[i];
                        var newModel = (DnsMappingTable)e.NewItems[i];
                        var vmIndex = _allTableVMs.ToList().FindIndex(vm => vm.Model == oldModel);
                        if (vmIndex >= 0)
                        {
                            _allTableVMs[vmIndex].Dispose();
                            _allTableVMs[vmIndex] = new DnsMappingTableViewModel(newModel, DoesResolverRequireIPv6);
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Move:
                    if (e.OldStartingIndex < _allTableVMs.Count && e.NewStartingIndex < _allTableVMs.Count)
                        _allTableVMs.Move(e.OldStartingIndex, e.NewStartingIndex);
                    break;

                case NotifyCollectionChangedAction.Reset:
                    foreach (var vm in _allTableVMs) vm.Dispose();
                    _allTableVMs.Clear();

                    foreach (var model in _tableService.AllConfigs)
                        _allTableVMs.Add(new DnsMappingTableViewModel(model, DoesResolverRequireIPv6));
                    break;
            }
        }

        private async void HandleResolverRemoved(Guid removedResolverId)
        {
            List<Task> tasks = [];

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (EditingTableCopy != null)
                    StopListeningToChanges(EditingTableCopy);

                try
                {
                    var modifiedTables = new List<DnsMappingTable>();

                    if (EditingTableCopy != null)
                    {
                        foreach (var rule in EditingTableCopy.MappingGroups.SelectMany(g => g.MappingRules))
                            foreach (var source in rule.TargetSources)
                                if (source.ResolverId == removedResolverId)
                                    source.ResolverId = null;

                        if (SelectedSource != null && SelectedSource.ResolverId == removedResolverId)
                            UpdateAssociatedResolverName();
                    }

                    foreach (var table in _tableService.AllConfigs)
                    {
                        bool wasModified = false;
                        foreach (var rule in table.MappingGroups.SelectMany(g => g.MappingRules))
                            foreach (var source in rule.TargetSources)
                                if (source.ResolverId == removedResolverId)
                                {
                                    source.ResolverId = null;
                                    wasModified = true;
                                }
                        if (wasModified) modifiedTables.Add(table);
                    }

                    if (modifiedTables.Any())
                        foreach (var tableToSave in modifiedTables)
                            tasks.Add(_tableService.SaveChangesAsync(tableToSave));
                }
                finally
                {
                    if (EditingTableCopy != null)
                    {
                        StartListeningToChanges(EditingTableCopy);
                        ValidateEditingCopy();
                    }
                }
            });

            if (tasks.Any())
                await Task.WhenAll(tasks);

            Application.Current.Dispatcher.Invoke(() =>
            {
                EditingTableVM?.RefreshAllRuleIPv6Status();
            });
        }

        private void HandleResolverRenamed(Guid resolverId, string newName)
        {
            if (SelectedSource != null && SelectedSource.ResolverId == resolverId)
                UpdateAssociatedResolverName();
        }

        private void HandleResolverUpdated(Guid updatedResolverId)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (EditingTableVM == null) return;
                foreach (var groupVM in EditingTableVM.MappingGroups)
                {
                    foreach (var ruleVM in groupVM.MappingRules)
                    {
                        bool isAffected = ruleVM.Model.TargetSources
                            .Any(s => s.ResolverId == updatedResolverId);

                        if (isAffected) ruleVM.RefreshIPv6Status();
                    }
                }
            });
        }
        #endregion

        #region Other Commands & Helpers
        #region Copy Link Code
        private async Task ExecuteCopyLinkCodeAsync(DnsMappingTableViewModel tableVM)
        {
            if (tableVM is null || !_canExecuteCopy) return;

            try
            {
                _canExecuteCopy = false;
                (CopyLinkCodeCommand as AsyncCommand<DnsMappingTableViewModel>)?.RaiseCanExecuteChanged();

                var table = tableVM.Model;
                var linkCode = Base64Utils.EncodeString(table.Id.ToString());
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        Clipboard.SetText(linkCode);
                        break;
                    }
                    catch (System.Runtime.InteropServices.COMException)
                    {
                        await Task.Delay(50);
                    }
                }

                await Task.Delay(500);
            }
            finally
            {
                _canExecuteCopy = true;
                (CopyLinkCodeCommand as AsyncCommand<DnsMappingTableViewModel>)?.RaiseCanExecuteChanged();
            }
        }

        private bool CanExecuteCopyLinkCode(DnsMappingTableViewModel tableVM) => tableVM != null && !_isBusy && _canExecuteCopy;
        #endregion

        #region General CanExecute Predicates & Helpers
        private bool CanExecuteWhenNotBusy() => !_isBusy;

        private bool CanExecuteOnEditableTable() => TableSelector.SelectedItem != null && !TableSelector.SelectedItem.IsBuiltIn && !_isBusy;

        private DnsMappingGroupViewModel FindParentGroupViewModel(object item)
        {
            // 如果是组，直接返回自己
            if (item is DnsMappingGroupViewModel groupVM) return groupVM;

            // 如果是规则，直接问它的 Parent 属性是谁
            if (item is DnsMappingRuleViewModel ruleVM) return ruleVM.Parent;

            return null;
        }

        private bool DoesResolverRequireIPv6(Guid? resolverId)
        {
            if (!resolverId.HasValue)
                return false;
            var resolver = _resolverService.AllConfigs.FirstOrDefault(r => r.Id == resolverId.Value);
            return resolver?.RequiresIPv6 ?? false;
        }
        #endregion
        #endregion

        #region IDisposable Implementation
        public void Dispose()
        {
            _resolverService.ConfigRemoved -= HandleResolverRemoved;
            _resolverService.ConfigRenamed -= HandleResolverRenamed;
            _resolverService.ConfigUpdated -= HandleResolverUpdated;
            if (EditingTableCopy != null)
                StopListeningToChanges(EditingTableCopy);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}