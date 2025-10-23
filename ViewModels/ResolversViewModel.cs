using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using SNIBypassGUI.Common;
using SNIBypassGUI.Common.Codecs;
using SNIBypassGUI.Common.Commands;
using SNIBypassGUI.Common.Dns;
using SNIBypassGUI.Common.Extensions;
using SNIBypassGUI.Common.Network;
using SNIBypassGUI.Enums;
using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Models;
using SNIBypassGUI.Validators;
using SNIBypassGUI.ViewModels.Dialogs.Items;
using SNIBypassGUI.ViewModels.Helpers;
using SNIBypassGUI.ViewModels.Items;

namespace SNIBypassGUI.ViewModels
{
    public class ResolversViewModel : NotifyPropertyChangedBase, IDisposable
    {
        #region Constants
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

        #region Dependencies & Instance State
        private readonly IConfigSetService<Resolver> _resolverService;
        private readonly IDialogService _dialogService;
        private readonly ResolverValidator _resolverValidator;
        private EditingState _currentState;
        private bool _isBusy;
        private bool _canExecuteCopy = true;
        private Resolver _originalResolver;
        private Resolver _editingResolverCopy;
        private ResolverViewModel _editingResolverVM;
        private IReadOnlyList<string> _validationErrors;
        private IReadOnlyList<string> _validationWarnings;
        private IReadOnlyList<string> _allPossibleCipherSuites;
        private readonly ObservableCollection<ResolverViewModel> _allResolverVMs = [];
        #endregion

        #region Constructor
        public ResolversViewModel(
            IConfigSetService<Resolver> resolverService, 
            IDialogService dialogService)
        {
            _resolverService = resolverService;
            _dialogService = dialogService;
            _resolverValidator = new();

            AllResolvers = new ReadOnlyObservableCollection<Resolver>(_resolverService.AllConfigs);
            AllResolverVMs = new ReadOnlyObservableCollection<ResolverViewModel>(_allResolverVMs);
            ResolverSelector = new SilentSelector<ResolverViewModel>(HandleUserSelectionChangedAsync);

            CopyLinkCodeCommand = new AsyncCommand<ResolverViewModel>(ExecuteCopyLinkCode, CanExecuteCopyLinkCode);
            ImportFromDnsStampCommand = new AsyncCommand(ExecuteImportFromDnsStampAsync, CanExecuteWhenNotBusy);

            AddNewResolverCommand = new AsyncCommand(ExecuteAddNewResolverAsync, CanExecuteWhenNotBusy);
            DuplicateResolverCommand = new AsyncCommand(ExecuteDuplicateResolverAsync, CanExecuteDuplicateResolver);
            DeleteResolverCommand = new AsyncCommand(ExecuteDeleteResolverAsync, CanExecuteOnEditableResolver);
            RenameResolverCommand = new AsyncCommand(ExecuteRenameResolverAsync, CanExecuteOnEditableResolver);
            ImportResolverCommand = new AsyncCommand(ExecuteImportResolverAsync, CanExecuteWhenNotBusy);
            ExportResolverCommand = new AsyncCommand(ExecuteExportResolverAsync, CanExecuteExport);

            SaveChangesCommand = new AsyncCommand(ExecuteSaveChangesAsync, CanExecuteSave);
            DiscardChangesCommand = new RelayCommand(ExecuteDiscardChanges, CanExecuteWhenDirty);

            EditHeaderNameCommand = new AsyncCommand<HttpHeader>(ExecuteEditHeaderNameAsync, CanExecuteWhenNotBusy);
            EditHeaderValueCommand = new AsyncCommand<HttpHeader>(ExecuteEditHeaderValueAsync, CanExecuteWhenNotBusy);
            MoveHeaderUpCommand = new RelayCommand<HttpHeader>(ExecuteMoveHeaderUp, CanExecuteMoveHeaderUp);
            MoveHeaderDownCommand = new RelayCommand<HttpHeader>(ExecuteMoveHeaderDown, CanExecuteMoveHeaderDown);
            DeleteHeaderCommand = new RelayCommand<HttpHeader>(ExecuteDeleteHeader, CanExecuteWhenNotBusy);
            AddHeaderCommand = new AsyncCommand(ExecuteAddHeaderAsync, CanExecuteWhenNotBusy);
            DeleteAllHeadersCommand = new AsyncCommand(ExecuteDeleteAllHeadersAsync, CanExecuteDeleteAllHeaders);

            EditAlpnProtocolCommand = new AsyncCommand<string>(ExecuteEditAlpnProtocolAsync, CanExecuteEditAlpnProtocol);
            AddAlpnProtocolCommand = new AsyncCommand(ExecuteAddAlpnProtocolAsync, CanExecuteWhenNotBusy);
            DeleteAlpnProtocolCommand = new RelayCommand<string>(ExecuteDeleteAlpnProtocol, CanExecuteWhenNotBusy);

            EditQuicAlpnTokenCommand = new AsyncCommand<string>(ExecuteEditQuicAlpnTokenAsync, CanExecuteEditQuicAlpnToken);
            AddQuicAlpnTokenCommand = new AsyncCommand(ExecuteAddQuicAlpnTokenAsync, CanExecuteWhenNotBusy);
            DeleteQuicAlpnTokenCommand = new RelayCommand<string>(ExecuteDeleteQuicAlpnToken, CanExecuteWhenNotBusy);

            SelectClientCertCommand = new RelayCommand(ExecuteSelectClientCert, CanExecuteSelectClientCert);
            ClearClientCertCommand = new RelayCommand(ExecuteClearClientCert, CanExecuteClearClientCert);
            SelectClientKeyCommand = new RelayCommand(ExecuteSelectClientKey, CanExecuteSelectClientKey);
            ClearClientKeyCommand = new RelayCommand(ExecuteClearClientKey, CanExecuteClearClientKey);

            _resolverService.AllConfigs.CollectionChanged += OnAllResolversCollectionChanged;

            _resolverService.LoadData();
            if (AllResolverVMs.Any()) SwitchToConfig(AllResolverVMs.First());
        }
        #endregion

        #region Public Properties
        public ReadOnlyObservableCollection<Resolver> AllResolvers { get; }

        public ReadOnlyObservableCollection<ResolverViewModel> AllResolverVMs { get; }

        public ResolverViewModel EditingResolverVM
        {
            get => _editingResolverVM;
            private set => SetProperty(ref _editingResolverVM, value);
        }

        public Resolver EditingResolverCopy
        {
            get => _editingResolverCopy;
            private set
            {
                if (_editingResolverCopy != null) StopListeningToChanges(_editingResolverCopy);

                if (SetProperty(ref _editingResolverCopy, value))
                {
                    EditingResolverVM?.Dispose();

                    if (_editingResolverCopy != null)
                    {
                        EditingResolverVM = new ResolverViewModel(_editingResolverCopy);
                        StartListeningToChanges(_editingResolverCopy);
                    }
                    else EditingResolverVM = null;

                    OnPropertyChanged(nameof(EditingResolverVM));
                }
            }
        }

        public IReadOnlyList<string> AllPossibleCipherSuites
        {
            get => _allPossibleCipherSuites;
            private set => SetProperty(ref _allPossibleCipherSuites, value);
        }

        public IReadOnlyList<string> AllPossibleTlsCurves => s_allTlsCurves;

        public SilentSelector<ResolverViewModel> ResolverSelector { get; }

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
        public ICommand AddNewResolverCommand { get; }
        public ICommand DuplicateResolverCommand { get; }
        public ICommand DeleteResolverCommand { get; }
        public ICommand RenameResolverCommand { get; }
        public ICommand ImportResolverCommand { get; }
        public ICommand ExportResolverCommand { get; }
        public ICommand DiscardChangesCommand { get; }
        public ICommand SaveChangesCommand { get; }
        public ICommand CopyLinkCodeCommand { get; }
        public ICommand DeleteHeaderCommand { get; }
        public ICommand EditHeaderNameCommand { get; }
        public ICommand EditHeaderValueCommand { get; }
        public ICommand MoveHeaderUpCommand { get; }
        public ICommand MoveHeaderDownCommand { get; }
        public ICommand AddHeaderCommand { get; }
        public ICommand DeleteAllHeadersCommand { get; }
        public ICommand EditAlpnProtocolCommand { get; }
        public ICommand AddAlpnProtocolCommand { get; }
        public ICommand DeleteAlpnProtocolCommand { get; }
        public ICommand EditQuicAlpnTokenCommand { get; }
        public ICommand AddQuicAlpnTokenCommand { get; }
        public ICommand DeleteQuicAlpnTokenCommand { get; }
        public ICommand SelectClientCertCommand { get; }
        public ICommand ClearClientCertCommand { get; }
        public ICommand SelectClientKeyCommand { get; }
        public ICommand ClearClientKeyCommand { get; }
        public ICommand ImportFromDnsStampCommand { get; }
        #endregion

        #region Lifecycle & State Management
        private async Task HandleUserSelectionChangedAsync(ResolverViewModel newItem, ResolverViewModel oldItem)
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
                            ResolverSelector.SetItemSilently(oldItem);
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

        private void SwitchToConfig(ResolverViewModel newConfigVM)
        {
            ResolverSelector.SetItemSilently(newConfigVM);
            ResetToSelectedConfig();
        }

        private void ResetToSelectedConfig()
        {
            _originalResolver = ResolverSelector.SelectedItem?.Model;
            EditingResolverCopy = _originalResolver?.Clone();
            UpdateAvailableCipherSuites();
            TransitionToState(EditingState.None);
        }

        private void EnterCreationMode(string profileName)
        {
            ResolverSelector.SetItemSilently(null);
            _originalResolver = null;
            EditingResolverCopy = _resolverService.CreateDefault();
            EditingResolverCopy.ResolverName = profileName;
            UpdateAvailableCipherSuites();
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
            (CopyLinkCodeCommand as AsyncCommand<ResolverViewModel>)?.RaiseCanExecuteChanged();

            (AddNewResolverCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (DuplicateResolverCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (DeleteResolverCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (RenameResolverCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (ImportResolverCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (ExportResolverCommand as AsyncCommand)?.RaiseCanExecuteChanged();

            (ImportFromDnsStampCommand as AsyncCommand)?.RaiseCanExecuteChanged();

            (EditHeaderNameCommand as AsyncCommand<HttpHeader>)?.RaiseCanExecuteChanged();
            (EditHeaderValueCommand as AsyncCommand<HttpHeader>)?.RaiseCanExecuteChanged();
            (MoveHeaderUpCommand as RelayCommand<HttpHeader>)?.RaiseCanExecuteChanged();
            (MoveHeaderDownCommand as RelayCommand<HttpHeader>)?.RaiseCanExecuteChanged();
            (DeleteHeaderCommand as RelayCommand<HttpHeader>)?.RaiseCanExecuteChanged();
            (AddHeaderCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (DeleteAllHeadersCommand as AsyncCommand)?.RaiseCanExecuteChanged();

            (EditAlpnProtocolCommand as AsyncCommand<string>)?.RaiseCanExecuteChanged();
            (AddAlpnProtocolCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (DeleteAlpnProtocolCommand as RelayCommand<string>)?.RaiseCanExecuteChanged();

            (EditQuicAlpnTokenCommand as AsyncCommand<string>)?.RaiseCanExecuteChanged();
            (AddQuicAlpnTokenCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (DeleteQuicAlpnTokenCommand as RelayCommand<string>)?.RaiseCanExecuteChanged();

            (SelectClientCertCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ClearClientCertCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SelectClientKeyCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ClearClientKeyCommand as RelayCommand)?.RaiseCanExecuteChanged();

            (SaveChangesCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (DiscardChangesCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
        #endregion

        #region Resolver Management
        #region Add New Resolver
        private async Task ExecuteAddNewResolverAsync()
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

            var newName = await _dialogService.ShowTextInputAsync("新建解析器", "请输入新解析器的名称：", "新解析器");
            if (newName != null)
            {
                if (!string.IsNullOrWhiteSpace(newName)) EnterCreationMode(newName);
                else await _dialogService.ShowInfoAsync("创建失败", "解析器名称不能为空！");
            }
            _isBusy = false;
            UpdateCommandStates();
        }
        #endregion

        #region Duplicate Resolver
        private async Task ExecuteDuplicateResolverAsync()
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
            var configToCloneVM = ResolverSelector.SelectedItem;
            if (configToCloneVM == null) return;

            var configToClone = configToCloneVM.Model;

            var suggestedName = $"{configToClone.ResolverName} - 副本";
            var newName = await _dialogService.ShowTextInputAsync("创建副本", "请输入新解析器的名称：", suggestedName);

            if (newName != null && !string.IsNullOrWhiteSpace(newName))
            {
                var newConfig = configToClone.Clone();
                newConfig.ResolverName = newName;
                newConfig.IsBuiltIn = false;
                newConfig.Id = Guid.NewGuid();

                _resolverService.AllConfigs.Add(newConfig);
                await _resolverService.SaveChangesAsync(newConfig);

                var newConfigVM = AllResolverVMs.FirstOrDefault(vm => vm.Model == newConfig);
                if (newConfigVM != null) SwitchToConfig(newConfigVM);
            }
            else if (newName != null)
                await _dialogService.ShowInfoAsync("创建失败", "解析器名称不能为空！");

            _isBusy = false;
            UpdateCommandStates();
        }

        private bool CanExecuteDuplicateResolver() => ResolverSelector.SelectedItem != null && !_isBusy;
        #endregion

        #region Delete Resolver
        private async Task ExecuteDeleteResolverAsync()
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

            var configToDeleteVM = ResolverSelector.SelectedItem;
            var configToDelete = configToDeleteVM.Model;
            var confirmResult = await _dialogService.ShowConfirmationAsync("确认删除", $"您确定要删除 “{configToDelete.ResolverName}” 吗？\n此操作不可恢复！", "删除");

            if (confirmResult)
            {
                ResolverViewModel nextSelectionVM = null;
                if (AllResolverVMs.Count > 1)
                {
                    int currentIndex = AllResolverVMs.IndexOf(configToDeleteVM);
                    nextSelectionVM = currentIndex == AllResolverVMs.Count - 1
                        ? AllResolverVMs[currentIndex - 1]
                        : AllResolverVMs[currentIndex + 1];
                }
                _resolverService.DeleteConfig(configToDelete);
                SwitchToConfig(nextSelectionVM);
            }

            _isBusy = false;
            UpdateCommandStates();
        }
        #endregion

        #region Rename Resolver
        private async Task ExecuteRenameResolverAsync()
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
                
                var newName = await _dialogService.ShowTextInputAsync($"重命名 “{EditingResolverCopy.ResolverName}”", "请输入新的解析器名称：", EditingResolverCopy.ResolverName);
                if (newName != null && !string.IsNullOrWhiteSpace(newName))
                {
                    if (newName != EditingResolverCopy.ResolverName)
                    {
                        EditingResolverCopy.ResolverName = newName;
                        await _resolverService.SaveChangesAsync(EditingResolverCopy);
                        ResetToSelectedConfig();
                    }
                }
                else if (newName != null)
                    await _dialogService.ShowInfoAsync("重命名失败", "解析器名称不能为空！");
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }
        #endregion

        #region Import & Export Resolver
        private async Task ExecuteImportResolverAsync()
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
                    Filter = "解析器文件 (*.srs)|*.srs",
                    Title = "选择要导入的解析器文件",
                    RestoreDirectory = true
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    var importedConfig = _resolverService.ImportConfig(openFileDialog.FileName);
                    if (importedConfig != null)
                    {
                        var importedConfigVM = AllResolverVMs.FirstOrDefault(vm => vm.Model == importedConfig);
                        if (importedConfigVM != null) SwitchToConfig(importedConfigVM);
                    }
                    else await _dialogService.ShowInfoAsync("错误", "解析器导入失败。");
                }
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }

        private async Task ExecuteExportResolverAsync()
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
                var choice = await _dialogService.ShowExportConfirmationAsync(EditingResolverCopy.ResolverName);
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

            var configToExportVM = ResolverSelector.SelectedItem;
            if (configToExportVM == null)
            {
                await _dialogService.ShowInfoAsync("错误", "没有可导出的配置。");
                return;
            }

            var configToExport = configToExportVM.Model;
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "解析器文件 (*.src)|*.src",
                Title = "选择导出位置",
                FileName = $"{configToExport.ResolverName}.src",
                RestoreDirectory = true
            };
            if (saveFileDialog.ShowDialog() == true)
                _resolverService.ExportConfig(configToExport, saveFileDialog.FileName);
        }

        private bool CanExecuteExport()
        {
            if (_isBusy) return false;

            // 当正在创建一个新解析器时，允许导出以便触发保存并导出的流程
            if (_currentState == EditingState.Creating) return true;

            if (ResolverSelector.SelectedItem != null && !ResolverSelector.SelectedItem.IsBuiltIn) return true;

            // 其他所有情况都禁用
            return false;
        }
        #endregion
        #endregion

        #region Editing Area Operations
        #region Change Listening & Validation
        private void StartListeningToChanges(Resolver config)
        {
            config.PropertyChanged += OnEditingCopyPropertyChanged;
            if (config.HttpHeaders != null)
            {
                config.HttpHeaders.CollectionChanged += OnChildCollectionChanged;
                foreach (var header in config.HttpHeaders)
                    header.PropertyChanged += OnHttpHeaderPropertyChanged;
            }
            if (config.TlsCipherSuites != null)
                config.TlsCipherSuites.CollectionChanged += OnChildCollectionChanged;
            if (config.TlsCurvePreferences != null)
                config.TlsCurvePreferences.CollectionChanged += OnChildCollectionChanged;
            if (config.TlsNextProtos != null)
                config.TlsNextProtos.CollectionChanged += OnChildCollectionChanged;
            if (config.QuicAlpnTokens != null)
                config.QuicAlpnTokens.CollectionChanged += OnChildCollectionChanged;
        }

        private void StopListeningToChanges(Resolver config)
        {
            config.PropertyChanged -= OnEditingCopyPropertyChanged;
            if (config.HttpHeaders != null)
            {
                config.HttpHeaders.CollectionChanged -= OnChildCollectionChanged;
                foreach (var header in config.HttpHeaders)
                    header.PropertyChanged -= OnHttpHeaderPropertyChanged;
            }
            if (config.TlsCipherSuites != null)
                config.TlsCipherSuites.CollectionChanged -= OnChildCollectionChanged;
            if (config.TlsCurvePreferences != null)
                config.TlsCurvePreferences.CollectionChanged -= OnChildCollectionChanged;
            if (config.TlsNextProtos != null)
                config.TlsNextProtos.CollectionChanged -= OnChildCollectionChanged;
            if (config.QuicAlpnTokens != null)
                config.QuicAlpnTokens.CollectionChanged -= OnChildCollectionChanged;
        }

        private void OnAllResolversCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    int insertIndex = Math.Min(e.NewStartingIndex, _allResolverVMs.Count);
                    foreach (Resolver model in e.NewItems)
                    {
                        _allResolverVMs.Insert(insertIndex, new ResolverViewModel(model));
                        insertIndex++;
                    }
                    break;

                case NotifyCollectionChangedAction.Remove:
                    foreach (Resolver model in e.OldItems)
                    {
                        var vmToRemove = _allResolverVMs.FirstOrDefault(vm => vm.Model == model);
                        if (vmToRemove != null)
                        {
                            vmToRemove.Dispose();
                            _allResolverVMs.Remove(vmToRemove);
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Replace:
                    for (int i = 0; i < e.OldItems.Count; i++)
                    {
                        var oldModel = (Resolver)e.OldItems[i];
                        var newModel = (Resolver)e.NewItems[i];
                        var vmIndex = _allResolverVMs.ToList().FindIndex(vm => vm.Model == oldModel);
                        if (vmIndex >= 0)
                        {
                            _allResolverVMs[vmIndex].Dispose();
                            _allResolverVMs[vmIndex] = new ResolverViewModel(newModel);
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Move:
                    if (e.OldStartingIndex < _allResolverVMs.Count && e.NewStartingIndex < _allResolverVMs.Count)
                        _allResolverVMs.Move(e.OldStartingIndex, e.NewStartingIndex);
                    break;

                case NotifyCollectionChangedAction.Reset:
                    foreach (var vm in _allResolverVMs) vm.Dispose();
                    _allResolverVMs.Clear();

                    foreach (var model in _resolverService.AllConfigs)
                        _allResolverVMs.Add(new ResolverViewModel(model));
                    break;
            }
        }

        private void OnEditingCopyPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_currentState == EditingState.None) TransitionToState(EditingState.Editing);
            if (e.PropertyName is nameof(Resolver.TlsMinVersion) or nameof(Resolver.TlsMaxVersion))
                UpdateAvailableCipherSuites();
            ValidateEditingCopy();
        }

        private void OnChildCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
                foreach (var item in e.OldItems.OfType<HttpHeader>())
                    (item as INotifyPropertyChanged).PropertyChanged -= OnHttpHeaderPropertyChanged;
            if (e.NewItems != null)
                foreach (var item in e.NewItems.OfType<HttpHeader>())
                    item.PropertyChanged += OnHttpHeaderPropertyChanged;

            if (_currentState == EditingState.None)
                TransitionToState(EditingState.Editing);
            ValidateEditingCopy();
        }

        private void OnHttpHeaderPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_currentState == EditingState.None) TransitionToState(EditingState.Editing);
            ValidateEditingCopy();
        }

        private void ValidateEditingCopy()
        {
            if (EditingResolverCopy == null) ValidationErrors = ValidationWarnings = null;
            else
            {
                var result = _resolverValidator.Validate(EditingResolverCopy);
                if (result.IsValid) ValidationErrors = null;
                else
                {
                    ValidationErrors = [.. result.Errors.Where(e => e.Severity == FluentValidation.Severity.Error).Select(e => e.ErrorMessage)];
                    ValidationWarnings = [.. result.Errors.Where(e => e.Severity == FluentValidation.Severity.Warning).Select(e => e.ErrorMessage)];
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
                    var newConfig = EditingResolverCopy;
                    _resolverService.AllConfigs.Add(newConfig);
                    await _resolverService.SaveChangesAsync(newConfig);
                    var newConfigVM = AllResolverVMs.FirstOrDefault(vm => vm.Model == newConfig);
                    if (newConfigVM != null)
                        SwitchToConfig(newConfigVM);
                }
                else if (_currentState == EditingState.Editing)
                {
                    _originalResolver.UpdateFrom(EditingResolverCopy);
                    await _resolverService.SaveChangesAsync(_originalResolver);
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
            if (_currentState == EditingState.Creating) SwitchToConfig(AllResolverVMs.FirstOrDefault());
            else
            {
                EditingResolverCopy = _originalResolver?.Clone();
                TransitionToState(EditingState.None);
            }
        }

        private bool CanExecuteSave() => _currentState != EditingState.None && !EditingResolverCopy.IsBuiltIn && !HasValidationErrors && !_isBusy;

        private bool CanExecuteWhenDirty() => _currentState != EditingState.None && !_isBusy;

        private async Task<SaveChangesResult> PromptToSaveChangesAndContinueAsync()
        {
            var message = _currentState == EditingState.Creating
                ? "您新建的解析器尚未保存，要保存吗？"
                : $"您对解析器 “{EditingResolverCopy.ResolverName}” 的修改尚未保存。要保存吗？";

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
        private async Task ExecuteEditHeaderNameAsync(HttpHeader item)
        {
            if (item is null) return;

            _isBusy = true;
            UpdateCommandStates();

            try
            {
                var newName = await _dialogService.ShowTextInputAsync($"编辑 “{item.Name}”", "请输入新的字段名：", item.Name);
                if (newName != null && newName != item.Name)
                {
                    string trimmedName = newName.Trim();
                    if (!string.IsNullOrWhiteSpace(trimmedName))
                    {
                        if (!NetworkUtils.IsValidHttpHeaderName(trimmedName))
                            await _dialogService.ShowInfoAsync("编辑失败", $"“{trimmedName}” 不是合法的字段名！");
                        else
                        {
                            int index = EditingResolverCopy.HttpHeaders.IndexOf(item);
                            if (index >= 0) EditingResolverCopy.HttpHeaders[index].Name = trimmedName;
                        }
                    }
                    else await _dialogService.ShowInfoAsync("编辑失败", "字段名不能为空！");
                }
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }

        private async Task ExecuteEditHeaderValueAsync(HttpHeader item)
        {
            if (item is null) return;

            _isBusy = true;
            UpdateCommandStates();

            try
            {
                var newValue = await _dialogService.ShowTextInputAsync($"编辑 “{item.Value}”", $"请为字段 “{item.Name}” 输入新的值：", item.Value);
                if (newValue != null && newValue != item.Value)
                {
                    if (!NetworkUtils.IsValidHttpHeaderValue(newValue))
                        await _dialogService.ShowInfoAsync("编辑失败", $"“{newValue}” 不是合法的字段值！");
                    else
                    {
                        int index = EditingResolverCopy.HttpHeaders.IndexOf(item);
                        if (index >= 0) EditingResolverCopy.HttpHeaders[index].Value = newValue;
                    }
                }
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }

        private void ExecuteMoveHeaderUp(HttpHeader item)
        {
            if (EditingResolverCopy == null || item == null) return;

            int index = EditingResolverCopy.HttpHeaders.IndexOf(item);
            if (index > 0) EditingResolverCopy.HttpHeaders.Move(index, index - 1);
        }

        private void ExecuteMoveHeaderDown(HttpHeader item)
        {
            if (EditingResolverCopy == null || item == null) return;

            int index = EditingResolverCopy.HttpHeaders.IndexOf(item);
            if (index < EditingResolverCopy.HttpHeaders.Count - 1)
                EditingResolverCopy.HttpHeaders.Move(index, index + 1);
        }

        private async Task ExecuteAddHeaderAsync()
        {
            if (EditingResolverCopy is null) return;

            _isBusy = true;
            UpdateCommandStates();

            try
            {
                var newName = await _dialogService.ShowTextInputAsync("添加字段", "请输入新字段的名称：");
                if (newName != null)
                {
                    string trimmedName = newName.Trim();
                    if (!string.IsNullOrWhiteSpace(trimmedName))
                    {
                        if (!NetworkUtils.IsValidHttpHeaderName(trimmedName))
                        {
                            await _dialogService.ShowInfoAsync("添加失败", $"“{trimmedName}” 不是合法的字段名！");
                            return;
                        }

                        var newValue = await _dialogService.ShowTextInputAsync("添加字段", $"请为字段 “{trimmedName}” 输入值：");
                        if (newValue != null)
                        {
                            if (!NetworkUtils.IsValidHttpHeaderValue(newValue))
                            {
                                await _dialogService.ShowInfoAsync("添加失败", $"“{newValue}” 不是合法的字段值！");
                                return;
                            }
                            var newItem = new HttpHeader
                            {
                                Name = newName,
                                Value = newValue.OrDefault()
                            };
                            EditingResolverCopy.HttpHeaders.Add(newItem);
                        }
                    }
                    else await _dialogService.ShowInfoAsync("添加失败", "字段名不能为空！");
                }
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }

        /*
         * 两个字段在一个对话框中输入的版本，用户使用体验有待评估
        private async Task ExecuteAddHeaderAsync()
        {
            (object buttonResult, Dictionary<string, object> fieldResults) = await _dialogService.ShowDialogAsync(new DialogViewModel(
                "添加字段",
                [
                    new InputField("name") { Label = "请输入新字段的名称：" },
                    new InputField("value") { Label = "请输入新字段的值：" }
                ],
                [
                    new DialogButtonViewModel { Content = "确定", Result = "OK", IsDefault = true },
                    new DialogButtonViewModel { Content = "取消", Result = "CANCEL", IsCancel = true }
                ]
            ));
            if (buttonResult as string == "OK")
            {
                string newName = fieldResults["name"] as string;
                string newValue = fieldResults["value"] as string;
                if (!string.IsNullOrWhiteSpace(newName))
                {
                    string trimmedName = newName.Trim();
                    if (!NetworkUtils.IsValidHttpHeaderName(trimmedName))
                    {
                        await _dialogService.ShowInfoAsync("添加失败", $"“{trimmedName}” 不是合法的字段名称！");
                        return;
                    }
                    if (EditingResolverCopy.HttpHeaders.Select(v => v.Name).Contains(trimmedName))
                    {
                        await _dialogService.ShowInfoAsync("添加失败", $"字段名称 “{trimmedName}” 已存在！");
                        return;
                    }
                    if (!NetworkUtils.IsValidHttpHeaderValue(newValue is null ? string.Empty : newValue))
                    {
                        await _dialogService.ShowInfoAsync("添加失败", $"“{newValue}” 不是合法的字段值！");
                        return;
                    }
                    var newItem = new HttpHeader
                    {
                        Name = newName,
                        Value = newValue.OrDefault()
                    };
                    EditingResolverCopy.HttpHeaders.Add(newItem);
                }
                else await _dialogService.ShowInfoAsync("添加失败", "字段名称不能为空！");
            }
        }
        */

        private void ExecuteDeleteHeader(HttpHeader item)
        {
            if (EditingResolverCopy == null || item is null) return;
            if (EditingResolverCopy.HttpHeaders.Contains(item))
                EditingResolverCopy.HttpHeaders.Remove(item);
        }

        private async Task ExecuteDeleteAllHeadersAsync()
        {
            if (EditingResolverCopy is null) return;

            _isBusy = true;
            UpdateCommandStates();

            try
            {
                if (EditingResolverCopy.HttpHeaders.Count > 0)
                {
                    var confirmResult = await _dialogService.ShowConfirmationAsync("确认删除", "您确定要删除 HTTP 头部中的所有字段吗？", "删除");
                    if (!confirmResult) return;
                    EditingResolverCopy.HttpHeaders.Clear();
                }
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }

        private bool CanExecuteMoveHeaderUp(HttpHeader item)
        {
            if (EditingResolverCopy == null || item is null)
                return false;

            var items = EditingResolverCopy.HttpHeaders;
            int index = items.IndexOf(item);
            return index > 0;
        }

        private bool CanExecuteMoveHeaderDown(HttpHeader item)
        {
            if (EditingResolverCopy == null || item is null)
                return false;

            var items = EditingResolverCopy.HttpHeaders;
            int index = items.IndexOf(item);
            return index < items.Count - 1;
        }

        private bool CanExecuteDeleteAllHeaders() => EditingResolverCopy?.HttpHeaders.Any() == true && !_isBusy;
        #endregion

        #region TLS ALPN Protocol Management
        private async Task ExecuteAddAlpnProtocolAsync()
        {
            if (EditingResolverCopy == null) return;

            _isBusy = true;
            UpdateCommandStates();

            try {
                var newName = await _dialogService.ShowTextInputAsync("添加协议", "请输入新的 ALPN 名称：");
                if (newName != null)
                {
                    if (!string.IsNullOrEmpty(newName))
                    {
                        if (!NetworkUtils.IsValidAlpnName(newName))
                            await _dialogService.ShowInfoAsync("添加失败", $"“{newName}” 不是合法的协议名称！");
                        else if (EditingResolverCopy.TlsNextProtos.Contains(newName))
                            await _dialogService.ShowInfoAsync("添加失败", $"协议名称 “{newName}” 已存在！");
                        else EditingResolverCopy.TlsNextProtos.Add(newName);
                    }
                    else await _dialogService.ShowInfoAsync("添加失败", "协议名称不能为空！");
                }
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }

        private async Task ExecuteEditAlpnProtocolAsync(string protocol)
        {
            if (protocol == null || EditingResolverCopy == null) return;

            _isBusy = true;
            UpdateCommandStates();

            try
            {
                var newProtocol = await _dialogService.ShowTextInputAsync($"编辑 “{protocol}”", "请输入新的 ALPN 名称：", protocol);
                if (newProtocol != null && newProtocol != protocol)
                {
                    if (!string.IsNullOrEmpty(newProtocol))
                    {
                        if (!NetworkUtils.IsValidAlpnName(newProtocol))
                            await _dialogService.ShowInfoAsync("编辑失败", $"“{newProtocol}” 不是合法的协议名称！");
                        else if (EditingResolverCopy.TlsNextProtos.Contains(newProtocol))
                            await _dialogService.ShowInfoAsync("编辑失败", $"协议名称 “{newProtocol}” 已存在！");
                        else
                        {
                            int index = EditingResolverCopy.TlsNextProtos.IndexOf(protocol);
                            if (index >= 0) EditingResolverCopy.TlsNextProtos[index] = newProtocol;
                        }
                    }
                    else await _dialogService.ShowInfoAsync("编辑失败", "协议名称不能为空！");
                }

            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }

        private void ExecuteDeleteAlpnProtocol(string protocol)
        {
            if (protocol is null) return;
            if (EditingResolverCopy.TlsNextProtos.Contains(protocol))
                EditingResolverCopy.TlsNextProtos.Remove(protocol);
        }

        private bool CanExecuteEditAlpnProtocol(string protocol) =>
            protocol != null && !_isBusy;
        #endregion

        #region QUIC ALPN Token Management
        private async Task ExecuteAddQuicAlpnTokenAsync()
        {
            if (EditingResolverCopy == null) return;

            _isBusy = true;
            UpdateCommandStates();

            try
            {
                var newName = await _dialogService.ShowTextInputAsync("添加协议", "请输入新的 QUIC ALPN 令牌：");
                if (newName != null)
                {
                    if (!string.IsNullOrEmpty(newName))
                    {
                        if (!NetworkUtils.IsValidAlpnName(newName))
                        {
                            await _dialogService.ShowInfoAsync("添加失败", $"“{newName}” 不是合法的 QUIC ALPN 令牌！");
                            return;
                        }
                        if (EditingResolverCopy.QuicAlpnTokens.Contains(newName))
                        {
                            await _dialogService.ShowInfoAsync("添加失败", $"QUIC ALPN 令牌 “{newName}” 已存在！");
                            return;
                        }
                        EditingResolverCopy.QuicAlpnTokens.Add(newName);
                    }
                    else await _dialogService.ShowInfoAsync("添加失败", "QUIC ALPN 令牌不能为空！");
                }
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }

        private async Task ExecuteEditQuicAlpnTokenAsync(string token)
        {
            if (token == null || EditingResolverCopy == null) return;

            _isBusy = true;
            UpdateCommandStates();

            try
            {
                var newToken = await _dialogService.ShowTextInputAsync($"编辑 “{token}”", "请输入新的 QUIC ALPN 令牌：", token);
                if (newToken != null && newToken != token)
                {
                    if (!string.IsNullOrEmpty(newToken))
                    {
                        if (!NetworkUtils.IsValidAlpnName(newToken))
                            await _dialogService.ShowInfoAsync("编辑失败", $"“{newToken}” 不是合法的 QUIC ALPN 令牌！");
                        else if (EditingResolverCopy.QuicAlpnTokens.Contains(newToken))
                            await _dialogService.ShowInfoAsync("编辑失败", $"QUIC ALPN 令牌 “{newToken}” 已存在！");
                        else
                        {
                            int index = EditingResolverCopy.QuicAlpnTokens.IndexOf(token);
                            if (index >= 0) EditingResolverCopy.QuicAlpnTokens[index] = newToken;
                        }
                    }
                    else await _dialogService.ShowInfoAsync("编辑失败", "QUIC ALPN 令牌不能为空！");
                }
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }

        private void ExecuteDeleteQuicAlpnToken(string token)
        {
            if (token is null) return;
            if (EditingResolverCopy.QuicAlpnTokens.Contains(token))
                EditingResolverCopy.QuicAlpnTokens.Remove(token);
        }

        private bool CanExecuteEditQuicAlpnToken(string token) =>
            token != null && !_isBusy;
        #endregion

        #region TLS Client Certificate Management
        private void ExecuteSelectClientCert()
        {
            if (EditingResolverCopy == null) return;

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
                    EditingResolverCopy.TlsClientCertPath = openFileDialog.FileName;
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }

        private void ExecuteClearClientCert()
        {
            if (EditingResolverCopy == null) return;

            EditingResolverCopy.TlsClientCertPath = null;
            EditingResolverCopy.TlsClientKeyPath = null;
        }

        private void ExecuteSelectClientKey()
        {
            if (EditingResolverCopy == null) return;

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
                    EditingResolverCopy.TlsClientKeyPath = openFileDialog.FileName;
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }

        private void ExecuteClearClientKey()
        {
            if (EditingResolverCopy == null) return;

            EditingResolverCopy.TlsClientKeyPath = null;
        }

        private bool CanExecuteSelectClientCert() => string.IsNullOrWhiteSpace(EditingResolverCopy?.TlsClientCertPath) && !_isBusy;

        private bool CanExecuteClearClientCert() => !string.IsNullOrWhiteSpace(EditingResolverCopy?.TlsClientCertPath) && !_isBusy;

        private bool CanExecuteSelectClientKey() => string.IsNullOrWhiteSpace(EditingResolverCopy?.TlsClientKeyPath) && !_isBusy;

        private bool CanExecuteClearClientKey() => !string.IsNullOrWhiteSpace(EditingResolverCopy?.TlsClientKeyPath) && !_isBusy;
        #endregion

        #region CipherSuites Management
        private void UpdateAvailableCipherSuites()
        {
            if (EditingResolverCopy is null)
            {
                AllPossibleCipherSuites = [];
                OnPropertyChanged(nameof(AllPossibleCipherSuites));
                return;
            }

            IReadOnlyList<string> newAvailableSuites;

            if (EditingResolverCopy.TlsMinVersion == 1.3m)
                newAvailableSuites = s_tls13CipherSuites;
            else
            {
                if (EditingResolverCopy.TlsMaxVersion == 1.3m)
                    newAvailableSuites = [.. s_allCipherSuites];
                else newAvailableSuites = s_tls12CipherSuites;
            }

            AllPossibleCipherSuites = newAvailableSuites;
            OnPropertyChanged(nameof(AllPossibleCipherSuites));

            if (EditingResolverCopy.TlsCipherSuites.Any())
            {
                var suitesToUnselect = EditingResolverCopy.TlsCipherSuites
                    .Except(newAvailableSuites)
                    .ToList();

                foreach (var suite in suitesToUnselect)
                    EditingResolverCopy.TlsCipherSuites.Remove(suite);
            }
        }
        #endregion

        #region Import from DNS Stamp
        private async Task ExecuteImportFromDnsStampAsync()
        {
            if (EditingResolverCopy == null) return;

            _isBusy = true;
            UpdateCommandStates();

            try
            {
                var stampStr = await _dialogService.ShowTextInputAsync("导入信息", "请在此处粘贴 DNS Stamp 字符串：");
                if (stampStr != null)
                {
                    string trimmedStamp = stampStr.Trim();
                    if (!string.IsNullOrWhiteSpace(trimmedStamp))
                    {
                        if (DnsStampParser.TryParse(stampStr, out var serverStamp))
                        {
                            ApplyStampToConfig(EditingResolverCopy, serverStamp);
                            await _dialogService.ShowInfoAsync("导入成功", "服务器信息已成功填充到当前表单。");
                        }
                        else await _dialogService.ShowInfoAsync("导入失败", "无法解析提供的 DNS Stamp，请检查格式是否正确。");
                    }
                    else await _dialogService.ShowInfoAsync("导入失败", "DNS Stamp 不能为空。");
                }
            }
            finally
            {
                _isBusy = false;
                UpdateCommandStates();
            }
        }

        private void ApplyStampToConfig(Resolver config, ServerStamp stamp)
        {
            config.Dnssec = stamp.Props.HasFlag(ServerInformalProperties.Dnssec);

            // 根据协议类型填充特定信息
            switch (stamp.Proto)
            {
                case StampProtoType.DnsCrypt:
                    config.ProtocolType = ResolverProtocol.DnsCrypt;
                    config.ServerAddress = stamp.ServerAddrStr;
                    config.DnsCryptProvider = stamp.ProviderName;
                    config.DnsCryptPublicKey = stamp.ServerPk?.ToHexString().ToUpper();
                    break;

                case StampProtoType.DoH:
                    config.ProtocolType = ResolverProtocol.DnsOverHttps;
                    config.ServerAddress = $"{stamp.ServerAddrStr}{stamp.Path}";
                    config.TlsServerName = stamp.ProviderName;
                    break;

                case StampProtoType.Tls:
                    config.ProtocolType = ResolverProtocol.DnsOverTls;
                    config.ServerAddress = stamp.ServerAddrStr;
                    config.TlsServerName = stamp.ProviderName;
                    break;

                case StampProtoType.DoQ:
                    config.ProtocolType = ResolverProtocol.DnsOverQuic;
                    config.ServerAddress = stamp.ServerAddrStr;
                    config.TlsServerName = stamp.ProviderName;
                    break;

                case StampProtoType.Plain:
                    config.ProtocolType = ResolverProtocol.Plain;
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
        private async Task ExecuteCopyLinkCode(ResolverViewModel configVM)
        {
            if (configVM is null || !_canExecuteCopy) return;

            try
            {
                _canExecuteCopy = false;
                (CopyLinkCodeCommand as AsyncCommand<ResolverViewModel>)?.RaiseCanExecuteChanged();

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
                (CopyLinkCodeCommand as AsyncCommand<ResolverViewModel>)?.RaiseCanExecuteChanged();
            }
        }

        private bool CanExecuteCopyLinkCode(ResolverViewModel configVM) => configVM != null && !_isBusy && _canExecuteCopy;
        #endregion

        #region General CanExecute Predicates
        private bool CanExecuteWhenNotBusy() => !_isBusy;

        private bool CanExecuteOnEditableResolver() => ResolverSelector.SelectedItem != null && !ResolverSelector.SelectedItem.IsBuiltIn && !_isBusy;
        #endregion
        #endregion

        #region IDisposable Implementation
        public void Dispose()
        {
            _resolverService.AllConfigs.CollectionChanged -= OnAllResolversCollectionChanged;
            if (_editingResolverCopy != null)
                StopListeningToChanges(_editingResolverCopy);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
