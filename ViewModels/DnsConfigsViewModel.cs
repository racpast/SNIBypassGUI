using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using SNIBypassGUI.Behaviors;
using SNIBypassGUI.Commands;
using SNIBypassGUI.Enums;
using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Models;
using SNIBypassGUI.Services;
using SNIBypassGUI.Utils.Codecs;
using SNIBypassGUI.Validators;
using SNIBypassGUI.ViewModels.Validation;

namespace SNIBypassGUI.ViewModels
{
    public class DnsConfigsViewModel : NotifyPropertyChangedBase, IDisposable
    {
        #region Dependencies & Core State
        private static readonly IReadOnlyList<string> s_allPossibleQueryTypes =
        [
            "A", "AAAA", "CNAME", "HTTPS", "MX", "NS",
            "PTR", "SOA", "SRV", "TXT"
        ];
        private static readonly IReadOnlyList<string> s_allPossibleLogEvents =
        [
            "X", "H", "C", "F", "R", "U"
        ];
        private readonly IConfigSetService<DnsConfig> _configService;
        private readonly IDialogService _dialogService;
        private readonly IFactory<DnsServer> _serverFactory;
        private readonly DnsConfigValidator _configValidator;
        private EditingState _currentState = EditingState.None;
        private bool _isBusy;
        private bool _canExecuteCopy = true;
        private DnsConfig _editingConfigCopy;
        private DnsServer _selectedDnsServer;
        private IReadOnlyList<ValidationErrorNode> _validationErrors;
        #endregion

        #region Constructor
        public DnsConfigsViewModel(
            IConfigSetService<DnsConfig> configService,
            IFactory<DnsServer> serverFactory,
            IDialogService dialogService)
        {
            _configService = configService;
            _serverFactory = serverFactory;
            _dialogService = dialogService;
            _configValidator = new();
            AllConfigs = new ReadOnlyObservableCollection<DnsConfig>(_configService.AllConfigs);
            ConfigSelector = new SilentSelector<DnsConfig>(HandleUserSelectionChangedAsync);

            AddNewConfigCommand = new AsyncCommand(ExecuteAddNewConfigAsync, CanExecuteWhenNotBusy);
            DuplicateConfigCommand = new AsyncCommand(ExecuteDuplicateConfigAsync, CanExecuteDuplicateConfig);
            DeleteConfigCommand = new AsyncCommand(ExecuteDeleteConfigAsync, CanExecuteOnEditableConfig);
            RenameConfigCommand = new AsyncCommand(ExecuteRenameConfigAsync, CanExecuteOnEditableConfig);
            ImportConfigCommand = new AsyncCommand(ExecuteImportConfigAsync, CanExecuteWhenNotBusy);
            ExportConfigCommand = new AsyncCommand(ExecuteExportConfigAsync, CanExecuteExport);
            SaveChangesCommand = new AsyncCommand(ExecuteSaveChangesAsync, CanExecuteSave);
            DiscardChangesCommand = new RelayCommand(ExecuteDiscardChanges, CanExecuteWhenDirty);
            AddNewServerCommand = new RelayCommand(ExecuteAddNewServer, CanExecuteAddNewServer);
            RemoveSelectedServerCommand = new RelayCommand(ExecuteRemoveSelectedServer, CanExecuteOnSelectedServer);
            MoveSelectedServerUpCommand = new RelayCommand(ExecuteMoveSelectedServerUp, CanExecuteMoveUp);
            MoveSelectedServerDownCommand = new RelayCommand(ExecuteMoveSelectedServerDown, CanExecuteMoveDown);
            CopyLinkCodeCommand = new AsyncCommand<DnsConfig>(ExecuteCopyLinkCode, CanExecuteCopyLinkCode);

            _configService.LoadData();
            if (AllConfigs.Any()) SwitchToConfig(AllConfigs.First());
        }
        #endregion

        #region Public Properties
        public IReadOnlyList<string> AllPossibleQueryTypes => s_allPossibleQueryTypes;

        public IReadOnlyList<string> AllPossibleLogEvents => s_allPossibleLogEvents;

        public ReadOnlyObservableCollection<DnsConfig> AllConfigs { get; }

        public SilentSelector<DnsConfig> ConfigSelector { get; }

        public DnsConfig EditingConfigCopy
        {
            get => _editingConfigCopy;
            private set
            {
                // 取消对旧副本及其子项的监听
                if (_editingConfigCopy != null)
                    StopListeningToChanges(_editingConfigCopy);
                // 你问我为什么 EditingConfigCopy.DnsServers[n].LimitQueryTypes的CollectionChanged 不需要被订阅也可以进入脏状态？
                // 别问，问就是 DnsServer 的 LimitQueryTypes 集合的变化会通过 PropertyChanged 事件向上传播。

                // 设置新属性
                SetProperty(ref _editingConfigCopy, value);

                // 订阅新副本及其子项的事件
                if (_editingConfigCopy != null)
                    StartListeningToChanges(_editingConfigCopy);
            }
        }

        public DnsServer SelectedDnsServer
        {
            get => _selectedDnsServer;
            set
            {
                if (SetProperty(ref _selectedDnsServer, value))
                    UpdateCommandStates();
            }
        }

        public IReadOnlyList<ValidationErrorNode> ValidationErrors
        {
            get => _validationErrors;
            private set => SetProperty(ref _validationErrors, value);
        }

        public bool HasValidationErrors => ValidationErrors?.Any() == true;
        #endregion

        #region Public Commands
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
        public ICommand CopyLinkCodeCommand { get; }

        #endregion

        #region Lifecycle & State Management
        private async Task HandleUserSelectionChangedAsync(DnsConfig newItem, DnsConfig oldItem)
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
                            SwitchToConfig(newItem);
                            break;
                        case SaveChangesResult.Cancel:
                            ConfigSelector.SetItemSilently(oldItem);
                            break;
                    }
                }
                else SwitchToConfig(newItem);
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }

        private void SwitchToConfig(DnsConfig newConfig)
        {
            // 更新 UI 选择器，确保 UI 与状态一致
            ConfigSelector.SetItemSilently(newConfig);

            // 加载到编辑区域，并重置编辑状态
            ResetToSelectedConfig();

            if (EditingConfigCopy != null && EditingConfigCopy.DnsServers.Any())
                SelectedDnsServer = EditingConfigCopy.DnsServers.First();
            else SelectedDnsServer = null;
        }

        private void ResetToSelectedConfig()
        {
            // 保存当前选中的服务器（如果有）
            var currentSelectedServer = SelectedDnsServer;

            EditingConfigCopy = ConfigSelector.SelectedItem?.Clone();

            // 重置后总是尝试选中第一个服务器
            if (EditingConfigCopy != null && EditingConfigCopy.DnsServers.Any())
            {
                // 优先尝试保持原有选中，否则选第一个
                SelectedDnsServer = EditingConfigCopy.DnsServers.Contains(currentSelectedServer)
                    ? currentSelectedServer
                    : EditingConfigCopy.DnsServers.First();
            }
            else SelectedDnsServer = null;

            TransitionToState(EditingState.None);
        }

        private void EnterCreationMode(string configName)
        {
            ConfigSelector.SetItemSilently(null);
            EditingConfigCopy = _configService.CreateDefault();
            EditingConfigCopy.ConfigName = configName;

            // 创建新配置时选中第一个服务器（说不定有呢）
            if (EditingConfigCopy.DnsServers.Any())
                SelectedDnsServer = EditingConfigCopy.DnsServers.First();

            TransitionToState(EditingState.Creating);
        }

        private void TransitionToState(EditingState newState)
        {
            _currentState = newState;
            ValidateEditingCopy();
        }

        private void UpdateCommandStates()
        {
            (AddNewConfigCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (DuplicateConfigCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (DeleteConfigCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (RenameConfigCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (ImportConfigCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (ExportConfigCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (SaveChangesCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (DiscardChangesCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AddNewServerCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (RemoveSelectedServerCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (MoveSelectedServerUpCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (MoveSelectedServerDownCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (CopyLinkCodeCommand as AsyncCommand<DnsConfig>)?.RaiseCanExecuteChanged();
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

            var configToClone = ConfigSelector.SelectedItem;
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

                SwitchToConfig(newConfig);
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

            var configToDelete = ConfigSelector.SelectedItem;
            var confirmResult = await _dialogService.ShowConfirmationAsync(
                "确认删除",
                $"您确定要删除 \"{configToDelete.ConfigName}\" 吗？\n此操作不可恢复！",
                "删除");

            if (confirmResult)
            {
                DnsConfig nextSelection = null;
                if (AllConfigs.Count > 1)
                {
                    int currentIndex = AllConfigs.IndexOf(configToDelete);
                    nextSelection = currentIndex == AllConfigs.Count - 1
                        ? AllConfigs[currentIndex - 1]
                        : AllConfigs[currentIndex + 1];
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

                var newName = await _dialogService.ShowTextInputAsync("重命名配置", $"为 \"{EditingConfigCopy.ConfigName}\" 输入新名称：", EditingConfigCopy.ConfigName);
                if (newName != null)
                {
                    if (!string.IsNullOrWhiteSpace(newName))
                    {
                        if (newName != EditingConfigCopy.ConfigName)
                        {
                            EditingConfigCopy.ConfigName = newName;
                            await _configService.SaveChangesAsync(EditingConfigCopy);
                        }
                        // 刷新显示并确保选中状态正确
                        SwitchToConfig(EditingConfigCopy);
                    }
                    else
                    {
                        await _dialogService.ShowInfoAsync("重命名失败", "配置名称不能为空！");
                        // 输入空名称后也需要重置选中状态
                        ResetToSelectedConfig();
                    }
                }
                else ResetToSelectedConfig(); // 用户取消输入后重置选中状态
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
                    if (importedConfig != null) SwitchToConfig(importedConfig);
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

            var configToExport = ConfigSelector.SelectedItem;
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
            {
                _configService.ExportConfig(configToExport, saveFileDialog.FileName);
            }
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
                    server.PropertyChanged += OnDnsServerPropertyChanged;
            }
            if (config.LimitQueryTypesCache != null)
                config.LimitQueryTypesCache.CollectionChanged += OnChildCollectionChanged;
            if (config.LogEvents != null)
                config.LogEvents.CollectionChanged += OnChildCollectionChanged;
        }

        private void StopListeningToChanges(DnsConfig config)
        {
            config.PropertyChanged -= OnEditingCopyPropertyChanged;
            if (config.DnsServers != null)
            {
                config.DnsServers.CollectionChanged -= OnDnsServersCollectionChanged;
                foreach (var server in config.DnsServers)
                    server.PropertyChanged -= OnDnsServerPropertyChanged;
            }
            if (config.LimitQueryTypesCache != null)
                config.LimitQueryTypesCache.CollectionChanged -= OnChildCollectionChanged;
            if (config.LogEvents != null)
                config.LogEvents.CollectionChanged -= OnChildCollectionChanged;
        }

        private void OnEditingCopyPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DnsConfig.DnsServers)) return;
            if (_currentState == EditingState.None)
                TransitionToState(EditingState.Editing);
            ValidateEditingCopy();
        }

        private void OnDnsServersCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // 为新添加的服务器订阅事件
            if (e.NewItems != null)
                foreach (INotifyPropertyChanged item in e.NewItems)
                    item.PropertyChanged += OnDnsServerPropertyChanged;

            // 为被移除的服务器取消订阅事件
            if (e.OldItems != null)
                foreach (INotifyPropertyChanged item in e.OldItems)
                    item.PropertyChanged -= OnDnsServerPropertyChanged;

            if (_currentState == EditingState.None)
                TransitionToState(EditingState.Editing);
            else ValidateEditingCopy();
        }

        private void OnDnsServerPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_currentState == EditingState.None)
                TransitionToState(EditingState.Editing);
            ValidateEditingCopy();
        }

        private void OnChildCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (_currentState == EditingState.None)
                TransitionToState(EditingState.Editing);
            ValidateEditingCopy();
        }

        private void ValidateEditingCopy()
        {
            if (EditingConfigCopy == null) ValidationErrors = null;
            else
            {
                var result = _configValidator.Validate(EditingConfigCopy);
                if (result.IsValid) ValidationErrors = null;
                else
                {
                    var errors = new List<ValidationErrorNode>();

                    foreach (var error in result.Errors)
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
                }
            }

            OnPropertyChanged(nameof(ValidationErrors));
            OnPropertyChanged(nameof(HasValidationErrors));
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
                    var newConfig = EditingConfigCopy;
                    _configService.AllConfigs.Add(newConfig);
                    await _configService.SaveChangesAsync(newConfig);
                    SwitchToConfig(newConfig);
                }
                else if (_currentState == EditingState.Editing)
                {
                    var originalConfig = ConfigSelector.SelectedItem;
                    originalConfig.UpdateFrom(EditingConfigCopy);
                    await _configService.SaveChangesAsync(originalConfig);
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
            if (_currentState == EditingState.Creating) SwitchToConfig(AllConfigs.FirstOrDefault());
            else ResetToSelectedConfig();
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

        #region DNS Server List Management
        private void ExecuteAddNewServer()
        {
            // 使用注入的工厂创建一个新的默认 DnsServer 实例
            var newServer = _serverFactory.CreateDefault();
            EditingConfigCopy.DnsServers.Add(newServer);

            // 自动选中新添加的项，方便用户立即编辑
            SelectedDnsServer = newServer;

            GlobalPropertyService.Instance.SetValue("DnsConfigsEPVM", newServer, "SelectedDnsServer");

            // 任何修改都应该进入编辑状态
            if (_currentState == EditingState.None)
                TransitionToState(EditingState.Editing);
            ValidateEditingCopy();
        }

        private bool CanExecuteAddNewServer() =>
            !_isBusy &&
            EditingConfigCopy != null &&
            EditingConfigCopy.DnsServers.Count < 10;

        private void ExecuteRemoveSelectedServer()
        {
            var serverToRemove = SelectedDnsServer;
            if (serverToRemove == null) return;

            var serversList = EditingConfigCopy.DnsServers;
            int oldIndex = serversList.IndexOf(serverToRemove);
            serversList.Remove(serverToRemove);

            if (serversList.Any())
            {
                int newIndex = oldIndex >= serversList.Count ? serversList.Count - 1 : oldIndex;
                SelectedDnsServer = serversList[newIndex];
            }
            else SelectedDnsServer = null;

            if (_currentState == EditingState.None)
                TransitionToState(EditingState.Editing);
            ValidateEditingCopy();
        }

        private void ExecuteMoveSelectedServerUp()
        {
            var serverToMove = SelectedDnsServer;
            if (serverToMove == null) return;

            var serversList = EditingConfigCopy.DnsServers;
            int currentIndex = serversList.IndexOf(serverToMove);
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
            return EditingConfigCopy.DnsServers.IndexOf(SelectedDnsServer) > 0;
        }

        private void ExecuteMoveSelectedServerDown()
        {
            var serverToMove = SelectedDnsServer;
            if (serverToMove == null) return;

            var serversList = EditingConfigCopy.DnsServers;
            int currentIndex = serversList.IndexOf(serverToMove);
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
            return EditingConfigCopy.DnsServers.IndexOf(SelectedDnsServer) < EditingConfigCopy.DnsServers.Count - 1;
        }

        private bool CanExecuteOnSelectedServer() =>
            !_isBusy &&
            EditingConfigCopy != null &&
            SelectedDnsServer != null;
        #endregion
        #endregion

        #region Other Commands & Helpers
        #region Copy Link Code
        private async Task ExecuteCopyLinkCode(DnsConfig config)
        {
            if (config is null || !_canExecuteCopy) return;

            try
            {
                _canExecuteCopy = false;
                (CopyLinkCodeCommand as RelayCommand<DnsConfig>)?.RaiseCanExecuteChanged();

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
                (CopyLinkCodeCommand as RelayCommand<DnsConfig>)?.RaiseCanExecuteChanged();
            }
        }

        private bool CanExecuteCopyLinkCode(DnsConfig config) => config != null && !_isBusy && _canExecuteCopy;
        #endregion

        #region General CanExecute Predicates
        private bool CanExecuteWhenNotBusy() => !_isBusy;

        private bool CanExecuteOnEditableConfig() =>
            ConfigSelector.SelectedItem != null &&
            !ConfigSelector.SelectedItem.IsBuiltIn &&
            !_isBusy;
        #endregion
        #endregion

        #region Disposal
        public void Dispose()
        {
            if (_editingConfigCopy != null)
                StopListeningToChanges(_editingConfigCopy);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}