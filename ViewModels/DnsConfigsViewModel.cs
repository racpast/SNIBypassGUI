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
using SNIBypassGUI.Common.Extensions;
using SNIBypassGUI.Consts;
using SNIBypassGUI.Enums;
using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Models;
using SNIBypassGUI.Validators;
using SNIBypassGUI.ViewModels.Helpers;
using SNIBypassGUI.ViewModels.Items;
using SNIBypassGUI.ViewModels.Validation;

namespace SNIBypassGUI.ViewModels
{
    public class DnsConfigsViewModel : NotifyPropertyChangedBase, IDisposable
    {
        #region Constants
        // DnsProtocol.pas
        private static readonly IReadOnlyList<string> s_allPossibleQueryTypes =
        [
            "A", "AAAA", "CNAME", "HTTPS", "MX", "NS",
            "PTR", "SOA", "SRV", "TXT", "ANY"
        ];
        private static readonly IReadOnlyList<string> s_allPossibleLogEvents =
        [
            "X", "H", "C", "F", "R", "U"
        ];
        #endregion

        #region Dependencies & Instance State
        private readonly IConfigSetService<DnsConfig> _configService;
        private readonly IDialogService _dialogService;
        private readonly IFactory<DnsServer> _serverFactory;
        private readonly IFactory<AffinityRule> _ruleFactory;
        private readonly DnsConfigValidator _configValidator;
        private EditingState _currentState = EditingState.None;
        private bool _isBusy;
        private bool _canExecuteCopy = true;
        private DnsConfig _originalConfig;
        private DnsConfig _editingConfigCopy;
        private DnsConfigViewModel _editingConfigVM;
        private DnsServerViewModel _selectedDnsServerVM;
        private IReadOnlyList<ValidationErrorNode> _validationErrors;
        private IReadOnlyList<ValidationErrorNode> _validationWarnings;
        private readonly ObservableCollection<DnsConfigViewModel> _allConfigVMs = [];
        #endregion

        #region Constructor
        public DnsConfigsViewModel(
            IConfigSetService<DnsConfig> configService,
            IFactory<DnsServer> serverFactory,
            IFactory<AffinityRule> ruleFactory,
            IDialogService dialogService)
        {
            _configService = configService;
            _serverFactory = serverFactory;
            _ruleFactory = ruleFactory;
            _dialogService = dialogService;
            _configValidator = new();

            AllConfigs = new ReadOnlyObservableCollection<DnsConfig>(_configService.AllConfigs);
            AllConfigVMs = new ReadOnlyObservableCollection<DnsConfigViewModel>(_allConfigVMs);
            _configService.AllConfigs.CollectionChanged += OnAllConfigsCollectionChanged;

            ConfigSelector = new SilentSelector<DnsConfigViewModel>(HandleUserSelectionChangedAsync);

            CopyLinkCodeCommand = new AsyncCommand<DnsConfigViewModel>(ExecuteCopyLinkCode, CanExecuteCopyLinkCode);

            AddNewConfigCommand = new AsyncCommand(ExecuteAddNewConfigAsync, CanExecuteWhenNotBusy);
            DuplicateConfigCommand = new AsyncCommand(ExecuteDuplicateConfigAsync, CanExecuteDuplicateConfig);
            DeleteConfigCommand = new AsyncCommand(ExecuteDeleteConfigAsync, CanExecuteOnEditableConfig);
            RenameConfigCommand = new AsyncCommand(ExecuteRenameConfigAsync, CanExecuteOnEditableConfig);
            ImportConfigCommand = new AsyncCommand(ExecuteImportConfigAsync, CanExecuteWhenNotBusy);
            ExportConfigCommand = new AsyncCommand(ExecuteExportConfigAsync, CanExecuteExport);

            AddNewServerCommand = new RelayCommand(ExecuteAddNewServer, CanExecuteAddNewServer);
            RemoveSelectedServerCommand = new RelayCommand(ExecuteRemoveSelectedServer, CanExecuteOnSelectedServer);
            MoveSelectedServerUpCommand = new RelayCommand(ExecuteMoveSelectedServerUp, CanExecuteMoveUp);
            MoveSelectedServerDownCommand = new RelayCommand(ExecuteMoveSelectedServerDown, CanExecuteMoveDown);

            EditDomainMatchingRulePatternCommand = new AsyncCommand<AffinityRule>(ExecuteEditDomainMatchingRulePatternAsync, CanExecuteEditDomainMatchingRule);
            MoveDomainMatchingRuleUpCommand = new RelayCommand<AffinityRule>(ExecuteMoveDomainMatchingRuleUp, CanExecuteMoveDomainMatchingRuleUp);
            MoveDomainMatchingRuleDownCommand = new RelayCommand<AffinityRule>(ExecuteMoveDomainMatchingRuleDown, CanExecuteMoveDomainMatchingRuleDown);
            DeleteDomainMatchingRuleCommand = new RelayCommand<AffinityRule>(ExecuteDeleteDomainMatchingRule, CanExecuteWhenNotBusy);
            AddDomainMatchingRuleCommand = new AsyncCommand(ExecuteAddDomainMatchingRuleAsync, CanExecuteWhenNotBusy);
            DeleteAllDomainMatchingRulesCommand = new AsyncCommand(ExecuteDeleteAllDomainMatchingRulesAsync, CanExecuteDeleteAllDomainMatchingRules);

            EditCacheDomainMatchingRulePatternCommand = new AsyncCommand<AffinityRule>(ExecuteEditCacheDomainMatchingRulePatternAsync, CanExecuteEditCacheDomainMatchingRule);
            MoveCacheDomainMatchingRuleUpCommand = new RelayCommand<AffinityRule>(ExecuteMoveCacheDomainMatchingRuleUp, CanExecuteMoveCacheDomainMatchingRuleUp);
            MoveCacheDomainMatchingRuleDownCommand = new RelayCommand<AffinityRule>(ExecuteMoveCacheDomainMatchingRuleDown, CanExecuteMoveCacheDomainMatchingRuleDown);
            DeleteCacheDomainMatchingRuleCommand = new RelayCommand<AffinityRule>(ExecuteDeleteCacheDomainMatchingRule, CanExecuteWhenNotBusy);
            AddCacheDomainMatchingRuleCommand = new AsyncCommand(ExecuteAddCacheDomainMatchingRuleAsync, CanExecuteWhenNotBusy);
            DeleteAllCacheDomainMatchingRulesCommand = new AsyncCommand(ExecuteDeleteAllCacheDomainMatchingRulesAsync, CanExecuteDeleteAllCacheDomainMatchingRules);

            SaveChangesCommand = new AsyncCommand(ExecuteSaveChangesAsync, CanExecuteSave);
            DiscardChangesCommand = new RelayCommand(ExecuteDiscardChanges, CanExecuteWhenDirty);

            _configService.LoadData();
            if (AllConfigVMs.Any()) SwitchToConfig(AllConfigVMs.First());
        }
        #endregion

        #region Public Properties
        public IReadOnlyList<string> AllPossibleQueryTypes => s_allPossibleQueryTypes;

        public IReadOnlyList<string> AllPossibleLogEvents => s_allPossibleLogEvents;

        public ReadOnlyObservableCollection<DnsConfig> AllConfigs { get; }

        public ReadOnlyObservableCollection<DnsConfigViewModel> AllConfigVMs { get; }

        public SilentSelector<DnsConfigViewModel> ConfigSelector { get; }

        public DnsConfigViewModel EditingConfigVM
        {
            get => _editingConfigVM;
            private set => SetProperty(ref _editingConfigVM, value);
        }

        public DnsConfig EditingConfigCopy
        {
            get => _editingConfigCopy;
            private set
            {
                if (_editingConfigCopy != null) StopListeningToChanges(_editingConfigCopy);

                if (SetProperty(ref _editingConfigCopy, value))
                {
                    EditingConfigVM?.Dispose();
                    if (_editingConfigCopy != null)
                    {
                        EditingConfigVM = new DnsConfigViewModel(_editingConfigCopy);
                        StartListeningToChanges(_editingConfigCopy);
                    }
                    else EditingConfigVM = null;
                    OnPropertyChanged(nameof(EditingConfigVM));
                    SelectedDnsServerVM = EditingConfigVM?.DnsServers.FirstOrDefault();
                }
            }
        }

        public DnsServerViewModel SelectedDnsServerVM
        {
            get => _selectedDnsServerVM;
            set
            {
                if (SetProperty(ref _selectedDnsServerVM, value))
                    UpdateCommandStates();
            }
        }

        public IReadOnlyList<ValidationErrorNode> ValidationErrors
        {
            get => _validationErrors;
            private set => SetProperty(ref _validationErrors, value);
        }

        public bool HasValidationErrors => ValidationErrors?.Any() == true;

        public IReadOnlyList<ValidationErrorNode> ValidationWarnings
        {
            get => _validationWarnings;
            private set => SetProperty(ref _validationWarnings, value);
        }

        public bool HasValidationWarnings => ValidationWarnings?.Any() == true;
        #endregion

        #region Public Commands
        public ICommand CopyLinkCodeCommand { get; }
        public ICommand AddNewConfigCommand { get; }
        public ICommand DuplicateConfigCommand { get; }
        public ICommand DeleteConfigCommand { get; }
        public ICommand RenameConfigCommand { get; }
        public ICommand ImportConfigCommand { get; }
        public ICommand ExportConfigCommand { get; }
        public ICommand DiscardChangesCommand { get; }
        public ICommand SaveChangesCommand { get; }
        public ICommand AddNewServerCommand { get; }
        public ICommand RemoveSelectedServerCommand { get; }
        public ICommand MoveSelectedServerUpCommand { get; }
        public ICommand MoveSelectedServerDownCommand { get; }

        public ICommand EditDomainMatchingRulePatternCommand { get; }
        public ICommand MoveDomainMatchingRuleUpCommand { get; }
        public ICommand MoveDomainMatchingRuleDownCommand { get; }
        public ICommand DeleteDomainMatchingRuleCommand { get; }
        public ICommand AddDomainMatchingRuleCommand { get; }
        public ICommand DeleteAllDomainMatchingRulesCommand { get; }

        public ICommand EditCacheDomainMatchingRulePatternCommand { get; }
        public ICommand MoveCacheDomainMatchingRuleUpCommand { get; }
        public ICommand MoveCacheDomainMatchingRuleDownCommand { get; }
        public ICommand DeleteCacheDomainMatchingRuleCommand { get; }
        public ICommand AddCacheDomainMatchingRuleCommand { get; }
        public ICommand DeleteAllCacheDomainMatchingRulesCommand { get; }
        #endregion

        #region Lifecycle & State Management
        private async Task HandleUserSelectionChangedAsync(DnsConfigViewModel newItem, DnsConfigViewModel oldItem)
        {
            _isBusy = true;
            UpdateCommandStates();
            try
            {
                if (_currentState != EditingState.None)
                {
                    var result = await PromptToSaveChangesAndContinueAsync();
                    if (result == SaveChangesResult.Save || result == SaveChangesResult.Discard)
                        SwitchToConfig(newItem);
                    else ConfigSelector.SetItemSilently(oldItem);
                }
                else SwitchToConfig(newItem);
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }

        private void SwitchToConfig(DnsConfigViewModel newConfigVM)
        {
            ConfigSelector.SetItemSilently(newConfigVM);
            ResetToSelectedConfig();
        }

        private void ResetToSelectedConfig()
        {
            _originalConfig = ConfigSelector.SelectedItem?.Model;
            EditingConfigCopy = _originalConfig?.Clone();
            TransitionToState(EditingState.None);
        }

        private void EnterCreationMode(string configName)
        {
            ConfigSelector.SetItemSilently(null);
            _originalConfig = null;
            var newConfig = _configService.CreateDefault();
            newConfig.ConfigName = configName;
            EditingConfigCopy = newConfig;
            TransitionToState(EditingState.Creating);
        }

        private void TransitionToState(EditingState newState)
        {
            _currentState = newState;
            ValidateEditingCopy();
            UpdateCommandStates();
        }

        private void UpdateCommandStates()
        {
            (CopyLinkCodeCommand as AsyncCommand<DnsConfigViewModel>)?.RaiseCanExecuteChanged();

            (AddNewConfigCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (DuplicateConfigCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (DeleteConfigCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (RenameConfigCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (ImportConfigCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (ExportConfigCommand as AsyncCommand)?.RaiseCanExecuteChanged();

            (AddNewServerCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (RemoveSelectedServerCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (MoveSelectedServerUpCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (MoveSelectedServerDownCommand as RelayCommand)?.RaiseCanExecuteChanged();

            (EditDomainMatchingRulePatternCommand as AsyncCommand<AffinityRule>)?.RaiseCanExecuteChanged();
            (MoveDomainMatchingRuleUpCommand as RelayCommand<AffinityRule>)?.RaiseCanExecuteChanged();
            (MoveDomainMatchingRuleDownCommand as RelayCommand<AffinityRule>)?.RaiseCanExecuteChanged();
            (DeleteDomainMatchingRuleCommand as RelayCommand<AffinityRule>)?.RaiseCanExecuteChanged();
            (AddDomainMatchingRuleCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (DeleteAllDomainMatchingRulesCommand as AsyncCommand)?.RaiseCanExecuteChanged();

            (EditCacheDomainMatchingRulePatternCommand as AsyncCommand<AffinityRule>)?.RaiseCanExecuteChanged();
            (MoveCacheDomainMatchingRuleUpCommand as RelayCommand<AffinityRule>)?.RaiseCanExecuteChanged();
            (MoveCacheDomainMatchingRuleDownCommand as RelayCommand<AffinityRule>)?.RaiseCanExecuteChanged();
            (DeleteCacheDomainMatchingRuleCommand as RelayCommand<AffinityRule>)?.RaiseCanExecuteChanged();
            (AddCacheDomainMatchingRuleCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (DeleteAllCacheDomainMatchingRulesCommand as AsyncCommand)?.RaiseCanExecuteChanged();

            (SaveChangesCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (DiscardChangesCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
        #endregion

        #region Configuration Management
        #region Add New Config
        private async Task ExecuteAddNewConfigAsync()
        {
            _isBusy = true;
            UpdateCommandStates();

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

            var newName = await _dialogService.ShowTextInputAsync("新建 DNS 配置", "请输入新配置的名称：", "新 DNS 配置");
            if (newName != null)
            {
                if (!string.IsNullOrWhiteSpace(newName)) EnterCreationMode(newName);
                else await _dialogService.ShowInfoAsync("创建失败", "配置名称不能为空！");
            }
            _isBusy = false;
            UpdateCommandStates();
        }
        #endregion

        #region Duplicate Config
        private async Task ExecuteDuplicateConfigAsync()
        {
            _isBusy = true;
            UpdateCommandStates();

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
            var configToCloneVM = ConfigSelector.SelectedItem;
            if (configToCloneVM == null) return;

            var configToClone = configToCloneVM.Model;

            var suggestedName = $"{configToClone.ConfigName} - 副本";
            var newName = await _dialogService.ShowTextInputAsync("创建副本", "请输入新配置的名称：", suggestedName);

            if (newName != null && !string.IsNullOrWhiteSpace(newName))
            {
                var newConfig = configToClone.Clone();
                newConfig.ConfigName = newName;
                newConfig.IsBuiltIn = false;
                newConfig.Id = Guid.NewGuid();

                _configService.AllConfigs.Add(newConfig);
                await _configService.SaveChangesAsync(newConfig);

                var newConfigVM = AllConfigVMs.FirstOrDefault(vm => vm.Model == newConfig);
                if (newConfigVM != null) SwitchToConfig(newConfigVM);
            }
            else if (newName != null)
            {
                await _dialogService.ShowInfoAsync("创建失败", "配置名称不能为空！");
            }

            _isBusy = false;
            UpdateCommandStates();
        }

        private bool CanExecuteDuplicateConfig() =>
            ConfigSelector.SelectedItem != null &&
            !_isBusy;
        #endregion

        #region Delete Config
        private async Task ExecuteDeleteConfigAsync()
        {
            _isBusy = true;
            UpdateCommandStates();

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

            var configToDeleteVM = ConfigSelector.SelectedItem;
            var configToDelete = configToDeleteVM.Model;
            var confirmResult = await _dialogService.ShowConfirmationAsync(
                "确认删除",
                $"您确定要删除 “{configToDelete.ConfigName}” 吗？\n此操作不可恢复！",
                "删除");

            if (confirmResult)
            {
                DnsConfigViewModel nextSelection = null;
                if (AllConfigVMs.Count > 1)
                {
                    int currentIndex = AllConfigVMs.IndexOf(configToDeleteVM);
                    nextSelection = currentIndex == AllConfigVMs.Count - 1
                        ? AllConfigVMs[currentIndex - 1]
                        : AllConfigVMs[currentIndex + 1];
                }
                _configService.DeleteConfig(configToDelete);
                SwitchToConfig(nextSelection);
            }

            _isBusy = false;
            UpdateCommandStates();
        }
        #endregion

        #region Rename Config
        private async Task ExecuteRenameConfigAsync()
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

                var newName = await _dialogService.ShowTextInputAsync($"重命名 “{EditingConfigCopy.ConfigName}”", "请输入新的配置名称：", EditingConfigCopy.ConfigName);
                if (newName != null && !string.IsNullOrWhiteSpace(newName))
                {
                    if (newName != EditingConfigCopy.ConfigName)
                    {
                        EditingConfigCopy.ConfigName = newName;
                        await _configService.SaveChangesAsync(EditingConfigCopy);
                        ResetToSelectedConfig();
                    }
                }
                else if (newName != null)
                    await _dialogService.ShowInfoAsync("重命名失败", "配置名称不能为空！");
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }
        #endregion

        #region Import & Export Config
        private async Task ExecuteImportConfigAsync()
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
                    Filter = " DNS 配置文件 (*.sdc)|*.sdc",
                    Title = "选择要导入的 DNS 配置文件",
                    RestoreDirectory = true
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    var importedConfig = _configService.ImportConfig(openFileDialog.FileName);
                    if (importedConfig != null)
                    {
                        var importedConfigVM = AllConfigVMs.FirstOrDefault(vm => vm.Model == importedConfig);
                        if (importedConfigVM != null) SwitchToConfig(importedConfigVM);
                    }
                    else await _dialogService.ShowInfoAsync("错误", " DNS 配置导入失败。");
                }
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }

        private async Task ExecuteExportConfigAsync()
        {
            if (_currentState == EditingState.Creating)
            {
                var confirmResult = await _dialogService.ShowConfirmationAsync(
                    "保存并导出",
                    "此配置尚未保存，必须先保存才能导出。\n是否立即保存并继续导出？",
                    "保存并导出");

                if (!confirmResult) return;

                await ExecuteSaveChangesAsync();
                if (_currentState != EditingState.None) return;
            }
            else if (_currentState == EditingState.Editing)
            {
                var choice = await _dialogService.ShowExportConfirmationAsync(EditingConfigCopy.ConfigName);
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

            var configToExportVM = ConfigSelector.SelectedItem;
            if (configToExportVM == null)
            {
                await _dialogService.ShowInfoAsync("错误", "没有可导出的配置。");
                return;
            }

            var configToExport = configToExportVM.Model;
            if (configToExport == null)
            {
                await _dialogService.ShowInfoAsync("错误", "没有可导出的配置。");
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "DNS 配置文件 (*.sdc)|*.sdc",
                Title = "选择配置导出位置",
                FileName = $"{configToExport.ConfigName}.sdc",
                RestoreDirectory = true
            };
            if (saveFileDialog.ShowDialog() == true)
                _configService.ExportConfig(configToExport, saveFileDialog.FileName);
        }

        private bool CanExecuteExport()
        {
            if (_isBusy) return false;

            // 当正在创建一个新配置时，允许导出以便触发保存并导出的流程
            if (_currentState == EditingState.Creating) return true;

            if (ConfigSelector.SelectedItem != null && !ConfigSelector.SelectedItem.IsBuiltIn) return true;

            // 其他所有情况都禁用
            return false;
        }
        #endregion
        #endregion

        #region Editing Area Operations
        #region Change Listening & Validation
        private void StartListeningToChanges(DnsConfig config)
        {
            config.PropertyChanged += OnEditingCopyPropertyChanged;
            if (config.DnsServers != null)
            {
                config.DnsServers.CollectionChanged += OnDnsServersCollectionChanged;
                foreach (var server in config.DnsServers)
                {
                    server.PropertyChanged += OnEditingCopyPropertyChanged;
                    if (server.LimitQueryTypes != null)
                        server.LimitQueryTypes.CollectionChanged += OnEditingCopyPropertyChanged;
                    if (server.DomainMatchingRules != null)
                    {
                        server.DomainMatchingRules.CollectionChanged += OnAffinityRulesCollectionChanged;
                        foreach (var rule in server.DomainMatchingRules)
                            rule.PropertyChanged += OnEditingCopyPropertyChanged;
                    }
                }
            }
            if (config.LimitQueryTypesCache != null)
                config.LimitQueryTypesCache.CollectionChanged += OnEditingCopyPropertyChanged;
            if (config.LogEvents != null)
                config.LogEvents.CollectionChanged += OnEditingCopyPropertyChanged;
            if (config.CacheDomainMatchingRules != null)
            {
                config.CacheDomainMatchingRules.CollectionChanged += OnAffinityRulesCollectionChanged;
                foreach (var rule in config.CacheDomainMatchingRules)
                    rule.PropertyChanged += OnEditingCopyPropertyChanged;
            }
        }

        private void StopListeningToChanges(DnsConfig config)
        {
            config.PropertyChanged -= OnEditingCopyPropertyChanged;
            if (config.DnsServers != null)
            {
                config.DnsServers.CollectionChanged -= OnDnsServersCollectionChanged;
                foreach (var server in config.DnsServers)
                {
                    server.PropertyChanged -= OnEditingCopyPropertyChanged;
                    if (server.LimitQueryTypes != null)
                        server.LimitQueryTypes.CollectionChanged -= OnEditingCopyPropertyChanged;
                    if (server.DomainMatchingRules != null)
                    {
                        server.DomainMatchingRules.CollectionChanged -= OnAffinityRulesCollectionChanged;
                        foreach (var rule in server.DomainMatchingRules)
                            rule.PropertyChanged -= OnEditingCopyPropertyChanged;
                    }
                }
            }
            if (config.LimitQueryTypesCache != null)
                config.LimitQueryTypesCache.CollectionChanged -= OnEditingCopyPropertyChanged;
            if (config.LogEvents != null)
                config.LogEvents.CollectionChanged -= OnEditingCopyPropertyChanged;
            if (config.CacheDomainMatchingRules != null)
            {
                config.CacheDomainMatchingRules.CollectionChanged -= OnAffinityRulesCollectionChanged;
                foreach (var rule in config.CacheDomainMatchingRules)
                    rule.PropertyChanged -= OnEditingCopyPropertyChanged;
            }
        }

        private void OnDnsServersCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (DnsServer server in e.NewItems)
                {
                    server.PropertyChanged += OnEditingCopyPropertyChanged;
                    if (server.LimitQueryTypes != null)
                        server.LimitQueryTypes.CollectionChanged += OnEditingCopyPropertyChanged;
                    if (server.DomainMatchingRules != null)
                    {
                        server.DomainMatchingRules.CollectionChanged += OnEditingCopyPropertyChanged;
                        foreach (var rule in server.DomainMatchingRules)
                            rule.PropertyChanged += OnEditingCopyPropertyChanged;
                    }
                }
            }
            if (e.OldItems != null)
            {
                foreach (DnsServer server in e.OldItems)
                {
                    server.PropertyChanged -= OnEditingCopyPropertyChanged;
                    if (server.LimitQueryTypes != null)
                        server.LimitQueryTypes.CollectionChanged -= OnEditingCopyPropertyChanged;
                    if (server.DomainMatchingRules != null)
                    {
                        server.DomainMatchingRules.CollectionChanged -= OnEditingCopyPropertyChanged;
                        foreach (var rule in server.DomainMatchingRules)
                            rule.PropertyChanged -= OnEditingCopyPropertyChanged;
                    }
                }
            }
            OnEditingCopyPropertyChanged(sender, e);
        }

        private void OnEditingCopyPropertyChanged(object sender, EventArgs e)
        {
            if (_currentState == EditingState.None)
                TransitionToState(EditingState.Editing);

            ValidateEditingCopy();
        }

        private void OnAffinityRulesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (AffinityRule rule in e.NewItems)
                    rule.PropertyChanged += OnEditingCopyPropertyChanged;
            if (e.OldItems != null)
                foreach (AffinityRule rule in e.OldItems)
                    rule.PropertyChanged -= OnEditingCopyPropertyChanged;
            OnEditingCopyPropertyChanged(sender, e);
        }

        private void OnAllConfigsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    int insertIndex = Math.Min(e.NewStartingIndex, _allConfigVMs.Count);
                    foreach (DnsConfig model in e.NewItems)
                    {
                        _allConfigVMs.Insert(insertIndex, new DnsConfigViewModel(model));
                        insertIndex++;
                    }
                    break;

                case NotifyCollectionChangedAction.Remove:
                    foreach (DnsConfig model in e.OldItems)
                    {
                        var vmToRemove = _allConfigVMs.FirstOrDefault(vm => vm.Model == model);
                        if (vmToRemove != null)
                        {
                            vmToRemove.Dispose();
                            _allConfigVMs.Remove(vmToRemove);
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Replace:
                    for (int i = 0; i < e.OldItems.Count; i++)
                    {
                        var oldModel = (DnsConfig)e.OldItems[i];
                        var newModel = (DnsConfig)e.NewItems[i];
                        var vmIndex = _allConfigVMs.ToList().FindIndex(vm => vm.Model == oldModel);
                        if (vmIndex >= 0)
                        {
                            _allConfigVMs[vmIndex].Dispose();
                            _allConfigVMs[vmIndex] = new DnsConfigViewModel(newModel);
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Move:
                    if (e.OldStartingIndex < _allConfigVMs.Count && e.NewStartingIndex < _allConfigVMs.Count)
                        _allConfigVMs.Move(e.OldStartingIndex, e.NewStartingIndex);
                    break;

                case NotifyCollectionChangedAction.Reset:
                    foreach (var vm in _allConfigVMs) vm.Dispose();
                    _allConfigVMs.Clear();

                    foreach (var model in _configService.AllConfigs)
                        _allConfigVMs.Add(new DnsConfigViewModel(model));
                    break;
            }
        }

        private void ValidateEditingCopy()
        {
            if (EditingConfigCopy == null) ValidationErrors = ValidationWarnings = null;
            else
            {
                var result = _configValidator.Validate(EditingConfigCopy);
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

            OnPropertyChanged(nameof(ValidationWarnings));
            OnPropertyChanged(nameof(HasValidationWarnings));
            OnPropertyChanged(nameof(ValidationErrors));
            OnPropertyChanged(nameof(HasValidationErrors));
            UpdateCommandStates();
        }
        #endregion

        #region Save & Discard Changes
        private async Task ExecuteSaveChangesAsync()
        {
            _isBusy = true;
            UpdateCommandStates();

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
                    var newConfig = EditingConfigCopy;
                    _configService.AllConfigs.Add(newConfig);
                    await _configService.SaveChangesAsync(newConfig);
                    var newVm = AllConfigVMs.FirstOrDefault(vm => vm.Model == newConfig);
                    SwitchToConfig(newVm);
                }
                else if (_currentState == EditingState.Editing)
                {
                    _originalConfig.UpdateFrom(EditingConfigCopy);
                    await _configService.SaveChangesAsync(_originalConfig);
                    TransitionToState(EditingState.None);
                }
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }

        private bool CanExecuteSave() =>
            _currentState != EditingState.None &&
            EditingConfigCopy != null &&
            !EditingConfigCopy.IsBuiltIn &&
            !HasValidationErrors &&
            !_isBusy;

        private void ExecuteDiscardChanges()
        {
            if (_currentState == EditingState.Creating) SwitchToConfig(AllConfigVMs.FirstOrDefault());
            else
            {
                EditingConfigCopy = _originalConfig?.Clone();
                TransitionToState(EditingState.None);
            }
        }

        private bool CanExecuteWhenDirty() =>
            _currentState != EditingState.None &&
            !_isBusy;

        private async Task<SaveChangesResult> PromptToSaveChangesAndContinueAsync()
        {
            var message = _currentState == EditingState.Creating
                ? "您新建的配置尚未保存，要保存吗？"
                : $"您对配置 “{EditingConfigCopy.ConfigName}” 的修改尚未保存。要保存吗？";

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

        #region DNS Server Management
        private void ExecuteAddNewServer()
        {
            var newServerModel = _serverFactory.CreateDefault();
            EditingConfigCopy.DnsServers.Add(newServerModel);
            SelectedDnsServerVM = EditingConfigVM.DnsServers.FirstOrDefault(vm => vm.Model == newServerModel);
        }

        private bool CanExecuteAddNewServer() =>
            !_isBusy &&
            EditingConfigCopy != null &&
            EditingConfigCopy.DnsServers.Count < 10;

        private void ExecuteRemoveSelectedServer()
        {
            var vmToRemove = SelectedDnsServerVM;
            if (vmToRemove == null) return;

            int oldIndex = EditingConfigVM.DnsServers.IndexOf(vmToRemove);
            EditingConfigCopy.DnsServers.Remove(vmToRemove.Model);

            if (EditingConfigVM.DnsServers.Any())
            {
                int newIndex = Math.Min(oldIndex, EditingConfigVM.DnsServers.Count - 1);
                SelectedDnsServerVM = EditingConfigVM.DnsServers[newIndex];
            }
            else SelectedDnsServerVM = null;
        }

        private void ExecuteMoveSelectedServerUp()
        {
            var serverToMove = SelectedDnsServerVM;
            if (serverToMove == null) return;

            var serversList = EditingConfigCopy.DnsServers;
            int currentIndex = serversList.IndexOf(serverToMove.Model);
            if (currentIndex > 0)
            {
                serversList.Move(currentIndex, currentIndex - 1);
                if (_currentState == EditingState.None)
                    TransitionToState(EditingState.Editing);
                ValidateEditingCopy();
            }
        }

        private bool CanExecuteMoveUp()
        {
            if (!CanExecuteOnSelectedServer()) return false;
            return EditingConfigCopy.DnsServers.IndexOf(SelectedDnsServerVM.Model) > 0;
        }

        private void ExecuteMoveSelectedServerDown()
        {
            var serverToMove = SelectedDnsServerVM;
            if (serverToMove == null) return;

            var serversList = EditingConfigCopy.DnsServers;
            int currentIndex = serversList.IndexOf(serverToMove.Model);
            if (currentIndex < serversList.Count - 1)
            {
                serversList.Move(currentIndex, currentIndex + 1);
                if (_currentState == EditingState.None)
                    TransitionToState(EditingState.Editing);
                ValidateEditingCopy();
            }
        }

        private bool CanExecuteMoveDown()
        {
            if (!CanExecuteOnSelectedServer()) return false;
            return EditingConfigCopy.DnsServers.IndexOf(SelectedDnsServerVM.Model) < EditingConfigCopy.DnsServers.Count - 1;
        }

        private bool CanExecuteOnSelectedServer() => !_isBusy &&
            EditingConfigCopy != null && SelectedDnsServerVM != null;
        #endregion

        #region Domain Matching Rule Management
        private async Task ExecuteAddDomainMatchingRuleAsync()
        {
            if (SelectedDnsServerVM is null) return;

            _isBusy = true;
            UpdateCommandStates();

            try
            {
                var server = SelectedDnsServerVM.Model;

                var newPattern = await _dialogService.ShowTextInputAsync("添加模式", "请输入新的匹配模式：");
                if (newPattern != null)
                {
                    if (!string.IsNullOrWhiteSpace(newPattern))
                    {
                        if (newPattern.ContainsAny('^', ';', Chars.Whitespaces))
                        {
                            await _dialogService.ShowInfoAsync("添加失败", $"“{newPattern}” 不是有效的匹配模式，不应包含 “^”、“;” 或任意空白字符。");
                            return;
                        }
                        if (server.DomainMatchingRules.Any(rule => rule.Pattern == newPattern))
                        {
                            await _dialogService.ShowInfoAsync("添加失败", $"匹配模式 “{newPattern}” 已存在！");
                            return;
                        }
                        var newRule = _ruleFactory.CreateDefault();
                        newRule.Pattern = newPattern;
                        server.DomainMatchingRules.Add(newRule);
                    }
                    else await _dialogService.ShowInfoAsync("添加失败", "匹配模式不能为空！");
                }
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }

        private async Task ExecuteEditDomainMatchingRulePatternAsync(AffinityRule rule)
        {
            if (SelectedDnsServerVM is null || rule is null) return;

            _isBusy = true;
            UpdateCommandStates();

            try
            {
                var server = SelectedDnsServerVM.Model;
                var pattern = rule.Pattern;

                var newPattern = await _dialogService.ShowTextInputAsync($"编辑 “{pattern}”", "请输入新的匹配模式：", pattern);
                if (newPattern != null && newPattern != pattern)
                {
                    if (!string.IsNullOrWhiteSpace(newPattern))
                    {
                        if (newPattern.ContainsAny('^', ';', Chars.Whitespaces))
                        {
                            await _dialogService.ShowInfoAsync("编辑失败", $"“{newPattern}” 不是有效的匹配模式，不应包含 “^”、“;” 或任意空白字符。");
                            return;
                        }
                        if (server.DomainMatchingRules.Any(rule => rule.Pattern == newPattern))
                        {
                            await _dialogService.ShowInfoAsync("编辑失败", $"匹配模式 “{newPattern}” 已存在！");
                            return;
                        }
                        int index = server.DomainMatchingRules.IndexOf(rule);
                        if (index >= 0) server.DomainMatchingRules[index].Pattern = newPattern;
                    }
                    else await _dialogService.ShowInfoAsync("编辑失败", "匹配模式不能为空！");
                }
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }

        private void ExecuteDeleteDomainMatchingRule(AffinityRule rule)
        {
            if (SelectedDnsServerVM is null || rule is null) return;
            var server = SelectedDnsServerVM.Model;
            if (server.DomainMatchingRules.Contains(rule))
                server.DomainMatchingRules.Remove(rule);
        }

        private void ExecuteMoveDomainMatchingRuleUp(AffinityRule rule)
        {
            if (SelectedDnsServerVM == null || rule == null) return;

            var server = SelectedDnsServerVM.Model;
            int index = server.DomainMatchingRules.IndexOf(rule);
            if (index > 0) server.DomainMatchingRules.Move(index, index - 1);
        }

        private void ExecuteMoveDomainMatchingRuleDown(AffinityRule rule)
        {
            if (SelectedDnsServerVM == null || rule == null) return;

            var server = SelectedDnsServerVM.Model;
            int index = server.DomainMatchingRules.IndexOf(rule);
            if (index < server.DomainMatchingRules.Count - 1)
                server.DomainMatchingRules.Move(index, index + 1);
        }

        private async Task ExecuteDeleteAllDomainMatchingRulesAsync()
        {
            if (SelectedDnsServerVM is null) return;

            _isBusy = true;
            UpdateCommandStates();

            try
            {
                var server = SelectedDnsServerVM.Model;
                if (server.DomainMatchingRules.Count > 0)
                {
                    var confirmResult = await _dialogService.ShowConfirmationAsync("确认删除", "您确定要删除域名匹配规则中的所有条目吗？", "删除");
                    if (!confirmResult) return;
                    server.DomainMatchingRules.Clear();
                }
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }

        private bool CanExecuteMoveDomainMatchingRuleUp(AffinityRule rule)
        {
            if (SelectedDnsServerVM is null || rule is null)
                return false;

            var items = SelectedDnsServerVM.Model.DomainMatchingRules;
            int index = items.IndexOf(rule);
            return index > 0;
        }

        private bool CanExecuteMoveDomainMatchingRuleDown(AffinityRule rule)
        {
            if (SelectedDnsServerVM is null || rule is null)
                return false;

            var items = SelectedDnsServerVM.Model.DomainMatchingRules;
            int index = items.IndexOf(rule);
            return index < items.Count - 1;
        }

        private bool CanExecuteDeleteAllDomainMatchingRules() => SelectedDnsServerVM?.Model?.DomainMatchingRules.Any() == true && !_isBusy;

        private bool CanExecuteEditDomainMatchingRule(AffinityRule rule) =>
            rule != null && !_isBusy;
        #endregion

        #region Cache Domain Matching Rule Management
        private async Task ExecuteAddCacheDomainMatchingRuleAsync()
        {
            if (EditingConfigCopy is null) return;

            _isBusy = true;
            UpdateCommandStates();

            try
            {
                var newPattern = await _dialogService.ShowTextInputAsync("添加模式", "请输入新的匹配模式：");
                if (newPattern != null)
                {
                    if (!string.IsNullOrWhiteSpace(newPattern))
                    {
                        if (newPattern.ContainsAny('^', ';', Chars.Whitespaces))
                        {
                            await _dialogService.ShowInfoAsync("添加失败", $"“{newPattern}” 不是有效的匹配模式，不应包含 “^”、“;” 或任意空白字符。");
                            return;
                        }
                        if (EditingConfigCopy.CacheDomainMatchingRules.Any(rule => rule.Pattern == newPattern))
                        {
                            await _dialogService.ShowInfoAsync("添加失败", $"匹配模式 “{newPattern}” 已存在！");
                            return;
                        }
                        var newRule = _ruleFactory.CreateDefault();
                        newRule.Pattern = newPattern;
                        EditingConfigCopy.CacheDomainMatchingRules.Add(newRule);
                    }
                    else await _dialogService.ShowInfoAsync("添加失败", "匹配模式不能为空！");
                }
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }

        private async Task ExecuteEditCacheDomainMatchingRulePatternAsync(AffinityRule rule)
        {
            if (EditingConfigCopy is null || rule is null) return;

            _isBusy = true;
            UpdateCommandStates();

            try
            {
                var pattern = rule.Pattern;
                var newPattern = await _dialogService.ShowTextInputAsync($"编辑 “{pattern}”", "请输入新的匹配模式：", pattern);
                if (newPattern != null && newPattern != pattern)
                {
                    if (!string.IsNullOrWhiteSpace(newPattern))
                    {
                        if (newPattern.ContainsAny('^', ';', Chars.Whitespaces))
                        {
                            await _dialogService.ShowInfoAsync("编辑失败", $"“{newPattern}” 不是有效的匹配模式，不应包含 “^”、“;” 或任意空白字符。");
                            return;
                        }
                        if (EditingConfigCopy.CacheDomainMatchingRules.Any(rule => rule.Pattern == newPattern))
                        {
                            await _dialogService.ShowInfoAsync("编辑失败", $"匹配模式 “{newPattern}” 已存在！");
                            return;
                        }
                        int index = EditingConfigCopy.CacheDomainMatchingRules.IndexOf(rule);
                        if (index >= 0) EditingConfigCopy.CacheDomainMatchingRules[index].Pattern = newPattern;
                    }
                    else await _dialogService.ShowInfoAsync("编辑失败", "匹配模式不能为空！");
                }
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }

        private void ExecuteDeleteCacheDomainMatchingRule(AffinityRule rule)
        {
            if (EditingConfigCopy is null || rule is null) return;
            if (EditingConfigCopy.CacheDomainMatchingRules.Contains(rule))
                EditingConfigCopy.CacheDomainMatchingRules.Remove(rule);
        }

        private void ExecuteMoveCacheDomainMatchingRuleUp(AffinityRule rule)
        {
            if (EditingConfigCopy == null || rule == null) return;

            int index = EditingConfigCopy.CacheDomainMatchingRules.IndexOf(rule);
            if (index > 0) EditingConfigCopy.CacheDomainMatchingRules.Move(index, index - 1);
        }

        private void ExecuteMoveCacheDomainMatchingRuleDown(AffinityRule rule)
        {
            if (EditingConfigCopy == null || rule == null) return;

            int index = EditingConfigCopy.CacheDomainMatchingRules.IndexOf(rule);
            if (index < EditingConfigCopy.CacheDomainMatchingRules.Count - 1)
                EditingConfigCopy.CacheDomainMatchingRules.Move(index, index + 1);
        }

        private async Task ExecuteDeleteAllCacheDomainMatchingRulesAsync()
        {
            if (EditingConfigCopy is null) return;

            _isBusy = true;
            UpdateCommandStates();

            try
            {
                if (EditingConfigCopy.CacheDomainMatchingRules.Count > 0)
                {
                    var confirmResult = await _dialogService.ShowConfirmationAsync("确认删除", "您确定要删除缓存域名匹配规则中的所有条目吗？", "删除");
                    if (!confirmResult) return;
                    EditingConfigCopy.CacheDomainMatchingRules.Clear();
                }
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }

        private bool CanExecuteMoveCacheDomainMatchingRuleUp(AffinityRule rule)
        {
            if (EditingConfigCopy is null || rule is null)
                return false;

            int index = EditingConfigCopy.CacheDomainMatchingRules.IndexOf(rule);
            return index > 0;
        }

        private bool CanExecuteMoveCacheDomainMatchingRuleDown(AffinityRule rule)
        {
            if (EditingConfigCopy is null || rule is null)
                return false;

            var items = EditingConfigCopy.CacheDomainMatchingRules;
            int index = items.IndexOf(rule);
            return index < items.Count - 1;
        }

        private bool CanExecuteDeleteAllCacheDomainMatchingRules() => EditingConfigCopy?.CacheDomainMatchingRules.Any() == true && !_isBusy;

        private bool CanExecuteEditCacheDomainMatchingRule(AffinityRule rule) =>
            rule != null && !_isBusy;
        #endregion
        #endregion

        #region Other Commands & Helpers
        #region Copy Link Code
        private async Task ExecuteCopyLinkCode(DnsConfigViewModel configVM)
        {
            if (configVM is null || !_canExecuteCopy) return;

            try
            {
                _canExecuteCopy = false;
                (CopyLinkCodeCommand as AsyncCommand<DnsConfigViewModel>)?.RaiseCanExecuteChanged();

                var config = configVM.Model;
                var linkCode = Base64Utils.EncodeString(config.Id.ToString());
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
                (CopyLinkCodeCommand as AsyncCommand<DnsConfigViewModel>)?.RaiseCanExecuteChanged();
            }
        }

        private bool CanExecuteCopyLinkCode(DnsConfigViewModel configVM) => configVM != null && !_isBusy && _canExecuteCopy;
        #endregion

        #region General CanExecute Predicates
        private bool CanExecuteWhenNotBusy() => !_isBusy;

        private bool CanExecuteOnEditableConfig() =>
            ConfigSelector.SelectedItem != null &&
            !ConfigSelector.SelectedItem.IsBuiltIn &&
            !_isBusy;
        #endregion
        #endregion

        #region IDisposable Implementation
        public void Dispose()
        {
            _configService.AllConfigs.CollectionChanged -= OnAllConfigsCollectionChanged;

            if (_editingConfigCopy != null) StopListeningToChanges(_editingConfigCopy);
            EditingConfigVM?.Dispose();

            foreach (var vm in _allConfigVMs) vm.Dispose();
            _allConfigVMs.Clear();

            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
