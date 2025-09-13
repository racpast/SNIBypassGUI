﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using SNIBypassGUI.Behaviors;
using SNIBypassGUI.Commands;
using SNIBypassGUI.Enums;
using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Models;
using SNIBypassGUI.Utils.Codecs;
using SNIBypassGUI.Utils.Dns;
using SNIBypassGUI.Utils.Extensions;
using SNIBypassGUI.Utils.Network;
using SNIBypassGUI.Validators;
using SNIBypassGUI.ViewModels.Dialogs.Items;

namespace SNIBypassGUI.ViewModels
{
    public class ResolverConfigsViewModel : NotifyPropertyChangedBase, IDisposable
    {
        #region Dependencies & Core State
        private readonly IConfigSetService<ResolverConfig> _configService;
        private readonly IDialogService _dialogService;
        private readonly ResolverConfigValidator _configValidator;
        private EditingState _currentState;
        private bool _isBusy;
        private bool _canExecuteCopy = true;
        private ResolverConfig _editingConfigCopy;
        private IReadOnlyList<string> _validationErrors;
        private IReadOnlyList<string> _validationWarnings;
        private IReadOnlyList<string> _allPossibleCipherSuites;
        private static readonly IReadOnlyList<string> s_tls12CipherSuites =
        [
            "TLS_RSA_WITH_RC4_128_SHA",
            "TLS_RSA_WITH_3DES_EDE_CBC_SHA",
            "TLS_RSA_WITH_AES_128_CBC_SHA",
            "TLS_RSA_WITH_AES_256_CBC_SHA",
            "TLS_RSA_WITH_AES_128_CBC_SHA256",
            "TLS_RSA_WITH_AES_128_GCM_SHA256",
            "TLS_RSA_WITH_AES_256_GCM_SHA384",
            "TLS_ECDHE_ECDSA_WITH_RC4_128_SHA",
            "TLS_ECDHE_ECDSA_WITH_AES_128_CBC_SHA",
            "TLS_ECDHE_ECDSA_WITH_AES_256_CBC_SHA",
            "TLS_ECDHE_RSA_WITH_RC4_128_SHA",
            "TLS_ECDHE_RSA_WITH_3DES_EDE_CBC_SHA",
            "TLS_ECDHE_RSA_WITH_AES_128_CBC_SHA",
            "TLS_ECDHE_RSA_WITH_AES_256_CBC_SHA",
            "TLS_ECDHE_ECDSA_WITH_AES_128_CBC_SHA256",
            "TLS_ECDHE_RSA_WITH_AES_128_CBC_SHA256",
            "TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256",
            "TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256",
            "TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384",
            "TLS_ECDHE_ECDSA_WITH_AES_256_GCM_SHA384",
            "TLS_ECDHE_RSA_WITH_CHACHA20_POLY1305_SHA256",
            "TLS_ECDHE_ECDSA_WITH_CHACHA20_POLY1305_SHA256",
        ];
        private static readonly IReadOnlyList<string> s_tls13CipherSuites =
        [
            "TLS_AES_128_GCM_SHA256",
            "TLS_AES_256_GCM_SHA384",
            "TLS_CHACHA20_POLY1305_SHA256",
        ];
        private static readonly IReadOnlyList<string> s_allCipherSuites = [.. s_tls12CipherSuites, .. s_tls13CipherSuites];
        private static readonly IReadOnlyList<string> s_allTlsCurves = ["P256", "P384", "P521", "X25519"];
        #endregion

        #region Constructor
        public ResolverConfigsViewModel(IConfigSetService<ResolverConfig> configService, IDialogService dialogService)
        {
            _configService = configService;
            _dialogService = dialogService;
            _configValidator = new();
            AllConfigs = new ReadOnlyObservableCollection<ResolverConfig>(_configService.AllConfigs);
            ConfigSelector = new SilentSelector<ResolverConfig>(HandleUserSelectionChangedAsync);

            AddNewConfigCommand = new AsyncCommand(ExecuteAddNewConfigAsync, CanExecuteWhenNotBusy);
            DuplicateConfigCommand = new AsyncCommand(ExecuteDuplicateConfigAsync, CanExecuteDuplicateConfig);
            DeleteConfigCommand = new AsyncCommand(ExecuteDeleteConfigAsync, CanExecuteOnEditableConfig);
            RenameConfigCommand = new AsyncCommand(ExecuteRenameConfigAsync, CanExecuteOnEditableConfig);
            ImportConfigCommand = new AsyncCommand(ExecuteImportConfigAsync, CanExecuteWhenNotBusy);
            ExportConfigCommand = new AsyncCommand(ExecuteExportConfigAsync, CanExecuteExport);
            SaveChangesCommand = new AsyncCommand(ExecuteSaveChangesAsync, CanExecuteSave);
            DiscardChangesCommand = new RelayCommand(ExecuteDiscardChanges, CanExecuteWhenDirty);
            CopyLinkCodeCommand = new AsyncCommand<ResolverConfig>(ExecuteCopyLinkCode, CanExecuteCopyLinkCode);
            DeleteHeaderCommand = new RelayCommand<HttpHeaderItem>(ExecuteDeleteHeader, CanExecuteWhenNotBusy);
            DeleteAllHeadersCommand = new AsyncCommand(ExecuteDeleteAllHeadersAsync, CanExecuteDeleteAllHeaders);
            AddHeaderCommand = new AsyncCommand(ExecuteAddHeaderAsync, CanExecuteWhenNotBusy);
            DeleteAlpnProtocolCommand = new RelayCommand<string>(ExecuteDeleteAlpnProtocol, CanExecuteWhenNotBusy);
            AddAlpnProtocolCommand = new AsyncCommand(ExecuteAddAlpnProtocolAsync, CanExecuteWhenNotBusy);
            AddQuicAlpnTokenCommand = new AsyncCommand(ExecuteAddQuicAlpnTokenAsync, CanExecuteWhenNotBusy);
            DeleteQuicAlpnTokenCommand = new RelayCommand<string>(ExecuteDeleteQuicAlpnToken, CanExecuteWhenNotBusy);
            SelectClientCertCommand = new RelayCommand(ExecuteSelectClientCert, CanExecuteSelectClientCert);
            ClearClientCertCommand = new RelayCommand(ExecuteClearClientCert, CanExecuteClearClientCert);
            SelectClientKeyCommand = new RelayCommand(ExecuteSelectClientKey, CanExecuteSelectClientKey);
            ClearClientKeyCommand = new RelayCommand(ExecuteClearClientKey, CanExecuteClearClientKey);
            ImportFromDnsStampCommand = new AsyncCommand(ExecuteImportFromDnsStampAsync, CanExecuteWhenNotBusy);

            _configService.LoadData();
            if (AllConfigs.Any()) SwitchToConfig(AllConfigs.First());
            _dialogService = dialogService;
        }
        #endregion

        #region Public Properties
        public ReadOnlyObservableCollection<ResolverConfig> AllConfigs { get; }

        public ResolverConfig EditingConfigCopy
        {
            get => _editingConfigCopy;
            private set
            {
                if (_editingConfigCopy != null)
                    StopListeningToChanges(_editingConfigCopy);

                SetProperty(ref _editingConfigCopy, value);

                if (_editingConfigCopy != null)
                    StartListeningToChanges(_editingConfigCopy);
            }
        }

        public IReadOnlyList<string> AllPossibleCipherSuites
        {
            get => _allPossibleCipherSuites;
            private set => SetProperty(ref _allPossibleCipherSuites, value);
        }

        public IReadOnlyList<string> AllPossibleTlsCurves => s_allTlsCurves;

        public SilentSelector<ResolverConfig> ConfigSelector { get; }

        public IReadOnlyList<string> ValidationErrors
        {
            get => _validationErrors;
            private set => SetProperty(ref _validationErrors, value);
        }

        public bool HasValidationErrors => ValidationErrors?.Any() == true;

        public IReadOnlyList<string> ValidationWarnings
        {
            get => _validationWarnings;
            private set => SetProperty(ref _validationWarnings, value);
        }

        public bool HasValidationWarnings => ValidationWarnings?.Any() == true;
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
        public ICommand CopyLinkCodeCommand { get; }
        public ICommand DeleteHeaderCommand { get; }
        public ICommand AddHeaderCommand { get; }
        public ICommand DeleteAllHeadersCommand { get; }
        public ICommand AddAlpnProtocolCommand { get; }
        public ICommand DeleteAlpnProtocolCommand { get; }
        public ICommand AddQuicAlpnTokenCommand { get; }
        public ICommand DeleteQuicAlpnTokenCommand { get; }
        public ICommand SelectClientCertCommand { get; }
        public ICommand ClearClientCertCommand { get; }
        public ICommand SelectClientKeyCommand { get; }
        public ICommand ClearClientKeyCommand { get; }
        public ICommand ImportFromDnsStampCommand { get; }
        #endregion

        #region Lifecycle & State Management
        private async Task HandleUserSelectionChangedAsync(ResolverConfig newItem, ResolverConfig oldItem)
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
                            // 保存成功后继续切换
                            SwitchToConfig(newItem);
                            break;
                        case SaveChangesResult.Discard:
                            // 放弃更改后继续切换
                            SwitchToConfig(newItem);
                            break;
                        case SaveChangesResult.Cancel:
                            // 取消操作，恢复原选择
                            ConfigSelector.SetItemSilently(oldItem);
                            break;
                    }
                }
                else SwitchToConfig(newItem); // 无编辑状态直接切换
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }

        private void SwitchToConfig(ResolverConfig newConfig)
        {
            // 更新 UI 选择器，确保 UI 与状态一致，且不会触发回环
            ConfigSelector.SetItemSilently(newConfig);

            // 加载到编辑区域，并重置编辑状态
            ResetToSelectedConfig();
        }

        private void ResetToSelectedConfig()
        {
            EditingConfigCopy = ConfigSelector.SelectedItem?.Clone();
            UpdateAvailableCipherSuites();
            TransitionToState(EditingState.None);
        }

        private void EnterCreationMode(string profileName)
        {
            ConfigSelector.SetItemSilently(null);
            EditingConfigCopy = _configService.CreateDefault();
            EditingConfigCopy.ConfigName = profileName;
            UpdateAvailableCipherSuites();
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
            (CopyLinkCodeCommand as AsyncCommand<ResolverConfig>)?.RaiseCanExecuteChanged();
            (DeleteAlpnProtocolCommand as RelayCommand<string>)?.RaiseCanExecuteChanged();
            (DeleteHeaderCommand as RelayCommand<HttpHeaderItem>)?.RaiseCanExecuteChanged();
            (AddHeaderCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (DeleteAllHeadersCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (AddAlpnProtocolCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (AddQuicAlpnTokenCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (DeleteQuicAlpnTokenCommand as RelayCommand<string>)?.RaiseCanExecuteChanged();
            (SelectClientCertCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ClearClientCertCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SelectClientKeyCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ClearClientKeyCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ImportFromDnsStampCommand as AsyncCommand)?.RaiseCanExecuteChanged();
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

            var newName = await _dialogService.ShowTextInputAsync("新建解析器配置", "请输入新配置的名称：", "新解析器配置");
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
                await _dialogService.ShowInfoAsync("创建失败", "配置名称不能为空！");

            _isBusy = false;
            UpdateCommandStates();
        }

        private bool CanExecuteDuplicateConfig() => ConfigSelector.SelectedItem != null && !_isBusy;
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
            var confirmResult = await _dialogService.ShowConfirmationAsync("确认删除", $"您确定要删除 “{configToDelete.ConfigName}” 吗？\n此操作不可恢复！", "删除");

            if (confirmResult)
            {
                ResolverConfig nextSelection = null;
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

                var newName = await _dialogService.ShowTextInputAsync("重命名配置", $"为 “{EditingConfigCopy.ConfigName}” 输入新名称：", EditingConfigCopy.ConfigName);
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
                    Filter = "解析器配置文件 (*.src)|*.src",
                    Title = "选择要导入的解析器配置文件",
                    RestoreDirectory = true
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    var importedConfig = _configService.ImportConfig(openFileDialog.FileName);
                    if (importedConfig != null) SwitchToConfig(importedConfig);
                    else await _dialogService.ShowInfoAsync("错误", "解析器配置导入失败。");
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
                var confirmResult = await _dialogService.ShowConfirmationAsync("保存并导出", "此配置尚未保存，必须先保存才能导出。\n是否立即保存并继续导出？", "保存并导出");

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
                Filter = "解析器配置文件 (*.src)|*.src",
                Title = "选择配置导出位置",
                FileName = $"{configToExport.ConfigName}.src",
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
        private void StartListeningToChanges(ResolverConfig config)
        {
            config.PropertyChanged += OnEditingCopyPropertyChanged;
            if (config.HttpHeaders != null)
                config.HttpHeaders.CollectionChanged += OnChildCollectionChanged;
            if (config.TlsCipherSuites != null)
                config.TlsCipherSuites.CollectionChanged += OnChildCollectionChanged;
            if (config.TlsCurvePreferences != null)
                config.TlsCurvePreferences.CollectionChanged += OnChildCollectionChanged;
            if (config.TlsNextProtos != null)
                config.TlsNextProtos.CollectionChanged += OnChildCollectionChanged;
            if (config.QuicAlpnTokens != null)
                config.QuicAlpnTokens.CollectionChanged += OnChildCollectionChanged;
        }

        private void StopListeningToChanges(ResolverConfig config)
        {
            config.PropertyChanged -= OnEditingCopyPropertyChanged;
            if (config.HttpHeaders != null)
                config.HttpHeaders.CollectionChanged -= OnChildCollectionChanged;
            if (config.TlsCipherSuites != null)
                config.TlsCipherSuites.CollectionChanged -= OnChildCollectionChanged;
            if (config.TlsCurvePreferences != null)
                config.TlsCurvePreferences.CollectionChanged -= OnChildCollectionChanged;
            if (config.TlsNextProtos != null)
                config.TlsNextProtos.CollectionChanged -= OnChildCollectionChanged;
            if (config.QuicAlpnTokens != null)
                config.QuicAlpnTokens.CollectionChanged -= OnChildCollectionChanged;
        }

        private void OnEditingCopyPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_currentState == EditingState.None) TransitionToState(EditingState.Editing);
            if (e.PropertyName is nameof(ResolverConfig.TlsMinVersion) or nameof(ResolverConfig.TlsMaxVersion))
                UpdateAvailableCipherSuites();
            if (e.PropertyName == nameof(ResolverConfig.ProtocolType))
            {
                if (EditingConfigCopy.ProtocolType == ResolverConfigProtocol.DnsOverQuic)
                {
                    EditingConfigCopy.TlsMinVersion = 1.3m;
                    EditingConfigCopy.TlsMaxVersion = 1.3m;
                }
            }
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
            if (EditingConfigCopy == null) ValidationErrors = ValidationWarnings = null;
            else
            {
                var result = _configValidator.Validate(EditingConfigCopy);
                if (result.IsValid) ValidationErrors = null;
                else
                {
                    ValidationErrors = [.. result.Errors.Where(e => e.Severity == FluentValidation.Severity.Error).Select(e => e.ErrorMessage)];
                    ValidationWarnings = [.. result.Errors.Where(e => e.Severity == FluentValidation.Severity.Warning).Select(e => e.ErrorMessage)];
                }
            }
            OnPropertyChanged(nameof(ValidationErrors));
            OnPropertyChanged(nameof(HasValidationErrors));
            OnPropertyChanged(nameof(ValidationWarnings));
            OnPropertyChanged(nameof(HasValidationWarnings));
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
                var errorItems = ValidationErrors.Select(e => new BulletedListItem
                {
                    Text = e,
                    IconKind = PackIconKind.AlertCircleOutline
                });
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

        private void ExecuteDiscardChanges()
        {
            if (_currentState == EditingState.Creating) SwitchToConfig(AllConfigs.FirstOrDefault());
            else ResetToSelectedConfig();
        }

        private bool CanExecuteSave() => _currentState != EditingState.None && !EditingConfigCopy.IsBuiltIn && !HasValidationErrors && !_isBusy;

        private bool CanExecuteWhenDirty() => _currentState != EditingState.None && !_isBusy;

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

        #region HTTP Header Management
        private async Task ExecuteAddHeaderAsync()
        {
            var newName = await _dialogService.ShowTextInputAsync("添加条目", "请输入新条目的名称：");
            if (newName != null)
            {
                string trimmedName = newName.Trim();
                if (!string.IsNullOrWhiteSpace(trimmedName))
                {
                    if (!NetworkUtils.IsValidHttpHeaderName(trimmedName))
                    {
                        await _dialogService.ShowInfoAsync("添加失败", $"“{trimmedName}” 不是合法的条目名称！");
                        return;
                    }
                    if (EditingConfigCopy.HttpHeaders.Select(v => v.Name).Contains(trimmedName))
                    {
                        await _dialogService.ShowInfoAsync("添加失败", $"条目名称 “{trimmedName}” 已存在！");
                        return;
                    }
                    var newValue = await _dialogService.ShowTextInputAsync("添加条目", $"请为条目 “{trimmedName}” 输入值：");
                    if (newValue != null)
                    {
                        if (!NetworkUtils.IsValidHttpHeaderValue(newValue))
                        {
                            await _dialogService.ShowInfoAsync("添加失败", $"“{newValue}” 不是合法的条目值！");
                            return;
                        }
                        var newItem = new HttpHeaderItem
                        {
                            Name = newName,
                            Value = newValue.OrDefault()
                        };
                        EditingConfigCopy.HttpHeaders.Add(newItem);
                    }
                }
                else await _dialogService.ShowInfoAsync("添加失败", "条目名称不能为空！");
            }
        }

        private void ExecuteDeleteHeader(HttpHeaderItem item)
        {
            if (item is null) return;
            if (EditingConfigCopy.HttpHeaders.Contains(item))
                EditingConfigCopy.HttpHeaders.Remove(item);
        }

        private async Task ExecuteDeleteAllHeadersAsync()
        {
            if (EditingConfigCopy.HttpHeaders.Count > 0)
            {
                var confirmResult = await _dialogService.ShowConfirmationAsync("确认删除", "您确定要删除 HTTP 头部中的所有条目吗？", "删除");
                if (!confirmResult) return;
                EditingConfigCopy.HttpHeaders.Clear();
            }
        }

        private bool CanExecuteDeleteAllHeaders() => EditingConfigCopy?.HttpHeaders.Any() == true && !_isBusy;
        #endregion

        #region TLS ALPN Protocol Management
        private async Task ExecuteAddAlpnProtocolAsync()
        {
            var newName = await _dialogService.ShowTextInputAsync("添加协议", "请输入新 ALPN 协议的名称：");
            if (newName != null)
            {
                if (!string.IsNullOrEmpty(newName))
                {
                    if (!NetworkUtils.IsValidAlpnName(newName))
                    {
                        await _dialogService.ShowInfoAsync("添加失败", $"“{newName}” 不是合法的协议名称！");
                        return;
                    }
                    if (EditingConfigCopy.TlsNextProtos.Contains(newName))
                    {
                        await _dialogService.ShowInfoAsync("添加失败", $"协议名称 “{newName}” 已存在！");
                        return;
                    }
                    EditingConfigCopy.TlsNextProtos.Add(newName);
                }
                else await _dialogService.ShowInfoAsync("添加失败", "协议名称不能为空！");
            }
        }

        private void ExecuteDeleteAlpnProtocol(string protocol)
        {
            if (protocol is null) return;
            if (EditingConfigCopy.TlsNextProtos.Contains(protocol))
                EditingConfigCopy.TlsNextProtos.Remove(protocol);
        }

        #endregion

        #region QUIC ALPN Token Management
        private async Task ExecuteAddQuicAlpnTokenAsync()
        {
            var newName = await _dialogService.ShowTextInputAsync("添加协议", "请输入新 QUIC ALPN 令牌：");
            if (newName != null)
            {
                if (!string.IsNullOrEmpty(newName))
                {
                    if (!NetworkUtils.IsValidAlpnName(newName))
                    {
                        await _dialogService.ShowInfoAsync("添加失败", $"“{newName}” 不是合法的 QUIC ALPN 令牌！");
                        return;
                    }
                    if (EditingConfigCopy.QuicAlpnTokens.Contains(newName))
                    {
                        await _dialogService.ShowInfoAsync("添加失败", $"QUIC ALPN 令牌 “{newName}” 已存在！");
                        return;
                    }
                    EditingConfigCopy.QuicAlpnTokens.Add(newName);
                }
                else await _dialogService.ShowInfoAsync("添加失败", "QUIC ALPN 令牌不能为空！");
            }
        }

        private void ExecuteDeleteQuicAlpnToken(string token)
        {
            if (token is null) return;
            if (EditingConfigCopy.QuicAlpnTokens.Contains(token))
                EditingConfigCopy.QuicAlpnTokens.Remove(token);
        }
        #endregion

        #region TLS Client Certificate Management
        private void ExecuteSelectClientCert()
        {
            if (EditingConfigCopy == null) return;
            _isBusy = true;
            UpdateCommandStates();

            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "PEM 证书文件 (*.pem;*.crt;*.cer)|*.pem;*.crt;*.cer|所有文件 (*.*)|*.*",
                    Title = "选择 TLS 客户端证书文件",
                    FilterIndex = 1,
                    RestoreDirectory = true
                };

                if (openFileDialog.ShowDialog() == true)
                    EditingConfigCopy.TlsClientCertPath = openFileDialog.FileName;
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }

        private void ExecuteClearClientCert()
        {
            if (EditingConfigCopy != null)
            {
                EditingConfigCopy.TlsClientCertPath = null;
                EditingConfigCopy.TlsClientKeyPath = null;
            }
        }

        private void ExecuteSelectClientKey()
        {
            if (EditingConfigCopy == null) return;
            _isBusy = true;
            UpdateCommandStates();

            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "PEM 私钥文件 (*.key;*.pem)|*.key;*.pem|所有文件 (*.*)|*.*",
                    Title = "选择 TLS 客户端私钥文件",
                    FilterIndex = 1,
                    RestoreDirectory = true
                };

                if (openFileDialog.ShowDialog() == true)
                    EditingConfigCopy.TlsClientKeyPath = openFileDialog.FileName;
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }

        private void ExecuteClearClientKey()
        {
            if (EditingConfigCopy != null)
                EditingConfigCopy.TlsClientKeyPath = null;
        }

        private bool CanExecuteSelectClientCert() => string.IsNullOrWhiteSpace(EditingConfigCopy?.TlsClientCertPath) && !_isBusy;

        private bool CanExecuteClearClientCert() => !string.IsNullOrWhiteSpace(EditingConfigCopy?.TlsClientCertPath) && !_isBusy;

        private bool CanExecuteSelectClientKey() => string.IsNullOrWhiteSpace(EditingConfigCopy?.TlsClientKeyPath) && !_isBusy;

        private bool CanExecuteClearClientKey() => !string.IsNullOrWhiteSpace(EditingConfigCopy?.TlsClientKeyPath) && !_isBusy;
        #endregion

        #region CipherSuites Management
        private void UpdateAvailableCipherSuites()
        {
            if (EditingConfigCopy is null)
            {
                AllPossibleCipherSuites = [];
                OnPropertyChanged(nameof(AllPossibleCipherSuites));
                return;
            }

            IReadOnlyList<string> newAvailableSuites;

            if (EditingConfigCopy.TlsMinVersion == 1.3m)
                newAvailableSuites = s_tls13CipherSuites;
            else
            {
                if (EditingConfigCopy.TlsMaxVersion == 1.3m)
                    newAvailableSuites = [.. s_allCipherSuites];
                else newAvailableSuites = s_tls12CipherSuites;
            }

            AllPossibleCipherSuites = newAvailableSuites;
            OnPropertyChanged(nameof(AllPossibleCipherSuites));

            if (EditingConfigCopy.TlsCipherSuites.Any())
            {
                var suitesToUnselect = EditingConfigCopy.TlsCipherSuites
                    .Except(newAvailableSuites)
                    .ToList();

                foreach (var suite in suitesToUnselect)
                    EditingConfigCopy.TlsCipherSuites.Remove(suite);
            }
        }
        #endregion

        #region Import from DNS Stamp
        private async Task ExecuteImportFromDnsStampAsync()
        {
            if (EditingConfigCopy == null) return;
            _isBusy = true;
            UpdateCommandStates();

            try
            {
                var stampStr = await _dialogService.ShowTextInputAsync("从 DNS Stamp 导入", "请在此处粘贴 DNS Stamp 字符串：");
                if (string.IsNullOrWhiteSpace(stampStr)) return;

                if (DnsStampParser.TryParse(stampStr, out var serverStamp))
                {
                    ApplyStampToConfig(EditingConfigCopy, serverStamp);
                    await _dialogService.ShowInfoAsync("导入成功", "服务器信息已成功填充到当前表单。");
                }
                else await _dialogService.ShowInfoAsync("导入失败", "无法解析提供的 DNS Stamp，请检查格式是否正确。");
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }

        private void ApplyStampToConfig(ResolverConfig config, ServerStamp stamp)
        {
            config.Dnssec = stamp.Props.HasFlag(ServerInformalProperties.Dnssec);

            // 根据协议类型填充特定信息
            switch (stamp.Proto)
            {
                case StampProtoType.DnsCrypt:
                    config.ProtocolType = ResolverConfigProtocol.DnsCrypt;
                    config.ServerAddress = stamp.ServerAddrStr;
                    config.DnsCryptProvider = stamp.ProviderName;
                    config.DnsCryptPublicKey = stamp.ServerPk?.ToHexString().ToUpper();
                    break;

                case StampProtoType.DoH:
                    config.ProtocolType = ResolverConfigProtocol.DnsOverHttps;
                    config.ServerAddress = $"{stamp.ServerAddrStr}{stamp.Path}";
                    config.TlsServerName = stamp.ProviderName;
                    break;

                case StampProtoType.Tls:
                    config.ProtocolType = ResolverConfigProtocol.DnsOverTls;
                    config.ServerAddress = stamp.ServerAddrStr;
                    config.TlsServerName = stamp.ProviderName;
                    break;

                case StampProtoType.DoQ:
                    config.ProtocolType = ResolverConfigProtocol.DnsOverQuic;
                    config.ServerAddress = stamp.ServerAddrStr;
                    config.TlsServerName = stamp.ProviderName;
                    break;

                case StampProtoType.Plain:
                    config.ProtocolType = ResolverConfigProtocol.Plain;
                    config.ServerAddress = stamp.ServerAddrStr;
                    break;

                default:
                    _dialogService.ShowInfoAsync("导入失败", $"暂不支持导入 “{stamp.Proto.GetName()}” 类型的 Stamp。");
                    break;
            }
        }
        #endregion
        #endregion

        #region Other Commands & Helpers
        #region Copy Link Code
        private async Task ExecuteCopyLinkCode(ResolverConfig config)
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

        private bool CanExecuteCopyLinkCode(ResolverConfig config) => config != null && !_isBusy && _canExecuteCopy;
        #endregion

        #region General CanExecute Predicates
        private bool CanExecuteWhenNotBusy() => !_isBusy;

        private bool CanExecuteOnEditableConfig() => ConfigSelector.SelectedItem != null && !ConfigSelector.SelectedItem.IsBuiltIn && !_isBusy;
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