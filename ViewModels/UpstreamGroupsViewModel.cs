using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using FluentValidation;
using Microsoft.Win32;
using SNIBypassGUI.Behaviors;
using SNIBypassGUI.Commands;
using SNIBypassGUI.Enums;
using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Models;
using SNIBypassGUI.Utils.Codecs;
using SNIBypassGUI.Utils.Network;
using SNIBypassGUI.Validators;
using SNIBypassGUI.ViewModels.Dialogs.Items;
using SNIBypassGUI.ViewModels.Validation;

namespace SNIBypassGUI.ViewModels
{
	public class UpstreamGroupsViewModel : NotifyPropertyChangedBase, IDisposable
	{
		#region Dependencies & Core State
		private readonly IConfigSetService<UpstreamGroup> _groupService;
		private readonly IConfigSetService<ResolverConfig> _resolverService;
		private readonly IDialogService _dialogService;
		private readonly IFactory<UpstreamSource> _sourceFactory;
		private readonly UpstreamGroupValidator _groupValidator;
		private UpstreamGroup _editingGroupCopy;
		private UpstreamSource _selectedSource;
		private string _associatedResolverName;
		private bool _isBusy;
		private bool _canExecuteCopy = true;
		private EditingState _currentState;
		private IReadOnlyList<ValidationErrorNode> _validationErrors;
		private IReadOnlyList<ValidationErrorNode> _validationWarnings;
		#endregion

		#region Constructor
		public UpstreamGroupsViewModel(IConfigSetService<UpstreamGroup> groupService, IConfigSetService<ResolverConfig> resolverService,
			IFactory<UpstreamSource> sourceFactory, IDialogService dialogService)
		{
			_groupService = groupService;
			_resolverService = resolverService;
			_dialogService = dialogService;
			_sourceFactory = sourceFactory;
			_groupValidator = new UpstreamGroupValidator();

			AllGroups = new ReadOnlyObservableCollection<UpstreamGroup>(_groupService.AllConfigs);
			GroupSelector = new SilentSelector<UpstreamGroup>(HandleUserSelectionChangedAsync);

			_resolverService.ConfigRemoved += HandleResolverRemoved;
			_resolverService.ConfigRenamed += HandleResolverRenamed;

			AddNewGroupCommand = new AsyncCommand(ExecuteAddNewGroupAsync, CanExecuteWhenNotBusy);
			DuplicateGroupCommand = new AsyncCommand(ExecuteDuplicateGroupAsync, CanExecuteDuplicateGroup);
			DeleteGroupCommand = new AsyncCommand(ExecuteDeleteGroupAsync, CanExecuteOnEditableGroup);
			RenameGroupCommand = new AsyncCommand(ExecuteRenameGroupAsync, CanExecuteOnEditableGroup);
			ImportGroupCommand = new AsyncCommand(ExecuteImportGroupAsync, CanExecuteWhenNotBusy);
			ExportGroupCommand = new AsyncCommand(ExecuteExportGroupAsync, CanExecuteExport);
			CopyLinkCodeCommand = new AsyncCommand<UpstreamGroup>(ExecuteCopyLinkCode, CanExecuteCopyLinkCode);

			AddNewSourceCommand = new RelayCommand(ExecuteAddNewSource, CanExecuteAddNewSource);
			RemoveSelectedSourceCommand = new RelayCommand(ExecuteRemoveSelectedSource, CanExecuteOnSelectedSource);
			MoveSelectedSourceUpCommand = new RelayCommand(ExecuteMoveSelectedSourceUp, CanExecuteMoveSelectedSourceUp);
			MoveSelectedSourceDownCommand = new RelayCommand(ExecuteMoveSelectedSourceDown, CanExecuteMoveSelectedSourceDown);

			AddFallbackAddressCommand = new AsyncCommand(ExecuteAddFallbackAddressAsync, CanExecuteOnSelectedSource);
			DeleteFallbackAddressCommand = new RelayCommand<string>(ExecuteDeleteFallbackAddress, CanExecuteOnSelectedSource);
			DeleteAllFallbackAddressesCommand = new AsyncCommand(ExecuteDeleteAllFallbackAddressesAsync, CanExecuteDeleteAllFallbackAddresses);
			MoveFallbackAddressUpCommand = new RelayCommand<string>(ExecuteMoveFallbackAddressUp, CanExecuteOnSelectedSource);
			MoveFallbackAddressDownCommand = new RelayCommand<string>(ExecuteMoveFallbackAddressDown, CanExecuteOnSelectedSource);

			PasteResolverLinkCodeCommand = new AsyncCommand(ExecutePasteResolverLinkCodeAsync, CanExecuteOnSelectedSource);
			UnlinkResolverCommand = new RelayCommand(ExecuteUnlinkResolver, CanExecuteOnSelectedSource);

			SaveChangesCommand = new AsyncCommand(ExecuteSaveChangesAsync, CanExecuteSave);
			DiscardChangesCommand = new RelayCommand(ExecuteDiscardChanges, CanExecuteWhenDirty);

			_groupService.LoadData();
			if (AllGroups.Any()) SwitchToGroup(AllGroups.First());
		}
		#endregion

		#region Public Properties
		public ReadOnlyObservableCollection<UpstreamGroup> AllGroups { get; }

		public SilentSelector<UpstreamGroup> GroupSelector { get; }

		public UpstreamGroup EditingGroupCopy
		{
			get => _editingGroupCopy;
			private set
			{
				if (_editingGroupCopy != null) StopListeningToChanges(_editingGroupCopy);
				if (SetProperty(ref _editingGroupCopy, value))
				{
					if (_editingGroupCopy != null) StartListeningToChanges(_editingGroupCopy);
					SelectedSource = _editingGroupCopy?.ServerSources.FirstOrDefault();
				}
			}
		}

		public UpstreamSource SelectedSource
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

		public EditingState CurrentState { get => _currentState; private set => SetProperty(ref _currentState, value); }

		public string AssociatedResolverName { get => _associatedResolverName; private set => SetProperty(ref _associatedResolverName, value); }

		public IReadOnlyList<ValidationErrorNode> ValidationErrors { get => _validationErrors; private set => SetProperty(ref _validationErrors, value); }

		public bool HasValidationErrors => ValidationErrors?.Any() == true;

		public IReadOnlyList<ValidationErrorNode> ValidationWarnings { get => _validationWarnings; private set => SetProperty(ref _validationWarnings, value); }

		public bool HasValidationWarnings => ValidationWarnings?.Any() == true;
		#endregion

		#region Public Commands
		public ICommand AddNewGroupCommand { get; }
		public ICommand DuplicateGroupCommand { get; }
		public ICommand DeleteGroupCommand { get; }
		public ICommand RenameGroupCommand { get; }
		public ICommand ImportGroupCommand { get; }
		public ICommand ExportGroupCommand { get; }
		public ICommand CopyLinkCodeCommand { get; }
		public ICommand AddNewSourceCommand { get; }
		public ICommand RemoveSelectedSourceCommand { get; }
		public ICommand MoveSelectedSourceUpCommand { get; }
		public ICommand MoveSelectedSourceDownCommand { get; }
		public ICommand AddFallbackAddressCommand { get; }
		public ICommand DeleteFallbackAddressCommand { get; }
		public ICommand DeleteAllFallbackAddressesCommand { get; }
		public ICommand MoveFallbackAddressUpCommand { get; }
		public ICommand MoveFallbackAddressDownCommand { get; }
		public ICommand PasteResolverLinkCodeCommand { get; }
		public ICommand UnlinkResolverCommand { get; }
		public ICommand SaveChangesCommand { get; }
		public ICommand DiscardChangesCommand { get; }
		#endregion

		#region Lifecycle & State Management
		private async Task HandleUserSelectionChangedAsync(UpstreamGroup newItem, UpstreamGroup oldItem)
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
							SwitchToGroup(newItem);
							break;
						case SaveChangesResult.Cancel:
							GroupSelector.SetItemSilently(oldItem);
							break;
					}
				}
				else SwitchToGroup(newItem);
			}
			finally
			{
				_isBusy = false;
				UpdateCommandStates();
			}
		}

		private void SwitchToGroup(UpstreamGroup newGroup)
		{
			GroupSelector.SetItemSilently(newGroup);
			ResetToSelectedGroup();
		}

		private void ResetToSelectedGroup()
		{
			EditingGroupCopy = GroupSelector.SelectedItem?.Clone();
			TransitionToState(EditingState.None);
		}

		private void EnterCreationMode(string name)
		{
			GroupSelector.SetItemSilently(null);
			EditingGroupCopy = _groupService.CreateDefault();
			EditingGroupCopy.GroupName = name;
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
			(AddNewGroupCommand as AsyncCommand)?.RaiseCanExecuteChanged();
			(DuplicateGroupCommand as AsyncCommand)?.RaiseCanExecuteChanged();
			(DeleteGroupCommand as AsyncCommand)?.RaiseCanExecuteChanged();
			(RenameGroupCommand as AsyncCommand)?.RaiseCanExecuteChanged();
			(ImportGroupCommand as AsyncCommand)?.RaiseCanExecuteChanged();
			(ExportGroupCommand as AsyncCommand)?.RaiseCanExecuteChanged();
			(CopyLinkCodeCommand as AsyncCommand<UpstreamGroup>)?.RaiseCanExecuteChanged();
			(AddNewSourceCommand as AsyncCommand)?.RaiseCanExecuteChanged();
			(RemoveSelectedSourceCommand as RelayCommand)?.RaiseCanExecuteChanged();
			(MoveSelectedSourceUpCommand as RelayCommand)?.RaiseCanExecuteChanged();
			(MoveSelectedSourceDownCommand as RelayCommand)?.RaiseCanExecuteChanged();
			(AddFallbackAddressCommand as AsyncCommand)?.RaiseCanExecuteChanged();
			(DeleteFallbackAddressCommand as RelayCommand<string>)?.RaiseCanExecuteChanged();
			(DeleteAllFallbackAddressesCommand as AsyncCommand)?.RaiseCanExecuteChanged();
			(MoveFallbackAddressUpCommand as RelayCommand<string>)?.RaiseCanExecuteChanged();
			(MoveFallbackAddressDownCommand as RelayCommand<string>)?.RaiseCanExecuteChanged();
			(PasteResolverLinkCodeCommand as AsyncCommand)?.RaiseCanExecuteChanged();
			(UnlinkResolverCommand as RelayCommand)?.RaiseCanExecuteChanged();
			(SaveChangesCommand as AsyncCommand)?.RaiseCanExecuteChanged();
			(DiscardChangesCommand as RelayCommand)?.RaiseCanExecuteChanged();
		}
		#endregion

		#region Group Management
		#region Add New Group
		private async Task ExecuteAddNewGroupAsync()
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

			var newName = await _dialogService.ShowTextInputAsync("新建上游组", "请输入新上游组的名称：", "新上游组");
			if (newName != null)
			{
				if (!string.IsNullOrWhiteSpace(newName)) EnterCreationMode(newName);
				else await _dialogService.ShowInfoAsync("创建失败", "上游组名称不能为空！");
			}
			_isBusy = false;
			UpdateCommandStates();
		}
		#endregion

		#region Duplicate Group
		private async Task ExecuteDuplicateGroupAsync()
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

			var groupToClone = GroupSelector.SelectedItem;
			var suggestedName = $"{groupToClone.GroupName} - 副本";
			var newName = await _dialogService.ShowTextInputAsync("创建副本", "请输入新上游组的名称：", suggestedName);

			if (newName != null && !string.IsNullOrWhiteSpace(newName))
			{
				var newGroup = groupToClone.Clone();
				newGroup.GroupName = newName;
				newGroup.IsBuiltIn = false;
				newGroup.Id = Guid.NewGuid();

				_groupService.AllConfigs.Add(newGroup);
				await _groupService.SaveChangesAsync(newGroup);
				SwitchToGroup(newGroup);
			}
			else if (newName != null) await _dialogService.ShowInfoAsync("创建失败", "上游组名称不能为空！");

			_isBusy = false;
			UpdateCommandStates();
		}

		private bool CanExecuteDuplicateGroup() => GroupSelector.SelectedItem != null && !_isBusy;
		#endregion

		#region Delete Group
		private async Task ExecuteDeleteGroupAsync()
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

			var groupToDelete = GroupSelector.SelectedItem;
			var confirmResult = await _dialogService.ShowConfirmationAsync("确认删除", $"您确定要删除 “{groupToDelete.GroupName}” 吗？\n此操作不可恢复！", "删除");

			if (confirmResult)
			{
				UpstreamGroup nextSelection = null;
				if (AllGroups.Count > 1)
				{
					int currentIndex = AllGroups.IndexOf(groupToDelete);
					nextSelection = currentIndex == AllGroups.Count - 1 ? AllGroups[currentIndex - 1] : AllGroups[currentIndex + 1];
				}
				_groupService.DeleteConfig(groupToDelete);
				SwitchToGroup(nextSelection);
			}

			_isBusy = false;
			UpdateCommandStates();
		}
		#endregion

		#region Rename Group
		private async Task ExecuteRenameGroupAsync()
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

				var originalGroup = GroupSelector.SelectedItem;
				var newName = await _dialogService.ShowTextInputAsync("重命名上游组", $"为 “{originalGroup.GroupName}” 输入新名称：", originalGroup.GroupName);
				if (newName != null && !string.IsNullOrWhiteSpace(newName))
				{
					if (newName != originalGroup.GroupName)
					{
						originalGroup.GroupName = newName;
						await _groupService.SaveChangesAsync(originalGroup);
						ResetToSelectedGroup();
					}
				}
				else if (newName != null) await _dialogService.ShowInfoAsync("重命名失败", "上游组名称不能为空！");
			}
			finally
			{
				_isBusy = false;
				UpdateCommandStates();
			}
		}

		#endregion

		#region Import & Export Group
		private async Task ExecuteImportGroupAsync()
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
					Filter = "上游组配置文件 (*.sug)|*.sug",
					Title = "选择要导入的上游组配置文件",
					RestoreDirectory = true
				};

				if (openFileDialog.ShowDialog() == true)
				{
					var importedGroup = _groupService.ImportConfig(openFileDialog.FileName);
					if (importedGroup != null) SwitchToGroup(importedGroup);
					else await _dialogService.ShowInfoAsync("错误", "上游组配置导入失败。");
				}
			}
			finally
			{
				_isBusy = false;
				UpdateCommandStates();
			}
		}

		private async Task ExecuteExportGroupAsync()
		{
			if (_currentState == EditingState.Creating)
			{
				var confirmResult = await _dialogService.ShowConfirmationAsync("保存并导出", "此上游组尚未保存，必须先保存才能导出。\n是否立即保存并继续导出？", "保存并导出");
				if (!confirmResult) return;
				await ExecuteSaveChangesAsync();
				if (_currentState != EditingState.None) return;
			}
			else if (_currentState == EditingState.Editing)
			{
				var choice = await _dialogService.ShowExportConfirmationAsync(EditingGroupCopy.GroupName);
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

			var groupToExport = GroupSelector.SelectedItem;
			if (groupToExport == null)
			{
				await _dialogService.ShowInfoAsync("错误", "没有可导出的上游组。");
				return;
			}

			var saveFileDialog = new SaveFileDialog
			{
				Filter = "上游组配置文件 (*.sug)|*.sug",
				Title = "选择上游组导出位置",
				FileName = $"{groupToExport.GroupName}.sug",
				RestoreDirectory = true
			};
			if (saveFileDialog.ShowDialog() == true)
				_groupService.ExportConfig(groupToExport, saveFileDialog.FileName);
		}

		private bool CanExecuteExport() => !_isBusy && (_currentState == EditingState.Creating || (GroupSelector.SelectedItem != null && !GroupSelector.SelectedItem.IsBuiltIn && !_isBusy));
		#endregion
		#endregion

		#region Editing Area Operations
		#region Change Listening & Validation
		private void StartListeningToChanges(UpstreamGroup group)
		{
			group.PropertyChanged += OnEditingCopyPropertyChanged;
			if (group.ServerSources != null)
			{
				group.ServerSources.CollectionChanged += OnSourcesCollectionChanged;
				foreach (var source in group.ServerSources) ListenToSource(source);
			}
		}

		private void StopListeningToChanges(UpstreamGroup group)
		{
			group.PropertyChanged -= OnEditingCopyPropertyChanged;
			if (group.ServerSources != null)
			{
				group.ServerSources.CollectionChanged -= OnSourcesCollectionChanged;
				foreach (var source in group.ServerSources) StopListeningToSource(source);
			}
		}

		private void ListenToSource(UpstreamSource source)
		{
			source.PropertyChanged += OnEditingCopyPropertyChanged;
			if (source.FallbackIpAddresses != null)
				source.FallbackIpAddresses.CollectionChanged += OnChildCollectionChanged;
		}

		private void StopListeningToSource(UpstreamSource source)
		{
			source.PropertyChanged -= OnEditingCopyPropertyChanged;
			if (source.FallbackIpAddresses != null)
				source.FallbackIpAddresses.CollectionChanged -= OnChildCollectionChanged;
		}

		private void OnSourcesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.NewItems != null) foreach (UpstreamSource item in e.NewItems) ListenToSource(item);
			if (e.OldItems != null) foreach (UpstreamSource item in e.OldItems) StopListeningToSource(item);
			if (CurrentState == EditingState.None) TransitionToState(EditingState.Editing);
			ValidateEditingCopy();
		}

		private void OnChildCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			if (CurrentState == EditingState.None) TransitionToState(EditingState.Editing);
			ValidateEditingCopy();
		}

		private void OnEditingCopyPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (CurrentState == EditingState.None) TransitionToState(EditingState.Editing);

			if (sender is UpstreamSource source && e.PropertyName == nameof(UpstreamSource.ResolverId))
				if (source == SelectedSource) UpdateAssociatedResolverName();

			ValidateEditingCopy();
		}

		private void ValidateEditingCopy()
		{
			if (EditingGroupCopy == null) ValidationErrors = ValidationWarnings = null;
			else
			{
				var result = _groupValidator.Validate(EditingGroupCopy);
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
					var newGroup = EditingGroupCopy;
					_groupService.AllConfigs.Add(newGroup);
					await _groupService.SaveChangesAsync(newGroup);
					SwitchToGroup(newGroup);
				}
				else if (_currentState == EditingState.Editing)
				{
					var originalGroup = GroupSelector.SelectedItem;
					originalGroup.UpdateFrom(EditingGroupCopy);
					await _groupService.SaveChangesAsync(originalGroup);
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
			if (_currentState == EditingState.Creating) SwitchToGroup(AllGroups.FirstOrDefault());
			else ResetToSelectedGroup();
		}

		private bool CanExecuteSave() => CanExecuteWhenDirty() && !HasValidationErrors;

		private bool CanExecuteWhenDirty() => _currentState != EditingState.None && !_isBusy;

		private async Task<SaveChangesResult> PromptToSaveChangesAndContinueAsync()
		{
			var message = _currentState == EditingState.Creating ? "您新建的上游组尚未保存，要保存吗？" : $"您对上游组 “{EditingGroupCopy.GroupName}” 的修改尚未保存。要保存吗？";
			var result = await _dialogService.ShowSaveChangesDialogAsync("未保存的更改", message);
			switch (result)
			{
				case SaveChangesResult.Save:
					await ExecuteSaveChangesAsync();
					return _currentState == EditingState.None ? SaveChangesResult.Save : SaveChangesResult.Cancel;
				case SaveChangesResult.Discard:
					ExecuteDiscardChanges();
					return SaveChangesResult.Discard;
				default:
					return SaveChangesResult.Cancel;
			}
		}
		#endregion

		#region Source Management
		private void ExecuteAddNewSource()
		{
			var newSource = _sourceFactory.CreateDefault();
			EditingGroupCopy.ServerSources.Add(newSource);
			SelectedSource = newSource;
		}

		private void ExecuteRemoveSelectedSource()
		{
			if (SelectedSource == null) return;

			int index = EditingGroupCopy.ServerSources.IndexOf(SelectedSource);
			EditingGroupCopy.ServerSources.Remove(SelectedSource);

			SelectedSource = EditingGroupCopy.ServerSources.Any() ? EditingGroupCopy.ServerSources[Math.Max(0, index - 1)] : null;
		}

		private void ExecuteMoveSelectedSourceUp()
		{
			if (SelectedSource == null) return;
			int index = EditingGroupCopy.ServerSources.IndexOf(SelectedSource);
			if (index > 0) EditingGroupCopy.ServerSources.Move(index, index - 1);
		}

		private void ExecuteMoveSelectedSourceDown()
		{
			if (SelectedSource == null) return;
			int index = EditingGroupCopy.ServerSources.IndexOf(SelectedSource);
			if (index < EditingGroupCopy.ServerSources.Count - 1)
				EditingGroupCopy.ServerSources.Move(index, index + 1);
		}

		private bool CanExecuteAddNewSource() => !_isBusy && EditingGroupCopy != null;

		private bool CanExecuteOnSelectedSource() => SelectedSource != null && !_isBusy;
		#endregion

		#region Resolver Link Management
		private async Task ExecutePasteResolverLinkCodeAsync()
		{
			if (SelectedSource is not { SourceType: IpAddressSourceType.Dynamic }) return;

			var linkCode = Clipboard.GetText();
			if (string.IsNullOrWhiteSpace(linkCode) || !Guid.TryParse(Base64Utils.DecodeString(linkCode), out Guid resolverId)) return;

			var resolver = _resolverService.AllConfigs.FirstOrDefault(r => r.Id == resolverId);
			if (resolver != null) SelectedSource.ResolverId = resolver.Id;
			else await _dialogService.ShowInfoAsync("关联失败", "未找到对应关联码的解析器配置。");
		}

		private void ExecuteUnlinkResolver()
		{
			if (SelectedSource != null) SelectedSource.ResolverId = null;
		}

		private void UpdateAssociatedResolverName()
		{
			if (SelectedSource is { SourceType: IpAddressSourceType.Dynamic, ResolverId: not null })
			{
				var resolver = _resolverService.AllConfigs.FirstOrDefault(r => r.Id == SelectedSource.ResolverId.Value);
				AssociatedResolverName = resolver?.ConfigName ?? "关联已失效";
			}
			else AssociatedResolverName = string.Empty;
		}
		#endregion

		#region Fallback Address Management
		private async Task ExecuteAddFallbackAddressAsync()
		{
			if (SelectedSource == null) return;
			var newIp = await _dialogService.ShowTextInputAsync("添加回落地址", "请输入 IP 地址：");
			if (newIp == null) return;

			string trimmed = newIp.Trim();
			if (string.IsNullOrWhiteSpace(trimmed))
				await _dialogService.ShowInfoAsync("添加失败", "回落地址不能为空！");
			else if (!NetworkUtils.IsValidIP(trimmed))
				await _dialogService.ShowInfoAsync("添加失败", $"“{trimmed}” 不是合法的 IP 地址！");
			else if (SelectedSource.FallbackIpAddresses.Contains(trimmed))
				await _dialogService.ShowInfoAsync("添加失败", $"回落地址 “{trimmed}” 已存在！");
			else SelectedSource.FallbackIpAddresses.Add(trimmed);
		}

		private void ExecuteDeleteFallbackAddress(string address)
		{
			if (SelectedSource != null && address != null && SelectedSource.FallbackIpAddresses.Contains(address))
				SelectedSource.FallbackIpAddresses.Remove(address);
		}

		private async Task ExecuteDeleteAllFallbackAddressesAsync()
		{
			if (SelectedSource is { FallbackIpAddresses.Count: > 0 })
			{
				var confirmResult = await _dialogService.ShowConfirmationAsync("确认删除", "您确定要删除所有回落地址吗？", "删除");
				if (confirmResult) SelectedSource.FallbackIpAddresses.Clear();
			}
		}

		private void ExecuteMoveFallbackAddressUp(string address)
		{
			if (SelectedSource?.FallbackIpAddresses.Contains(address) == true)
			{
				int index = SelectedSource.FallbackIpAddresses.IndexOf(address);
				if (index > 0) SelectedSource.FallbackIpAddresses.Move(index, index - 1);
			}
		}

		private void ExecuteMoveFallbackAddressDown(string address)
		{
			if (SelectedSource?.FallbackIpAddresses.Contains(address) == true)
			{
				int index = SelectedSource.FallbackIpAddresses.IndexOf(address);
				if (index < SelectedSource.FallbackIpAddresses.Count - 1)
					SelectedSource.FallbackIpAddresses.Move(index, index + 1);
			}
		}

		private bool CanExecuteMoveSelectedSourceUp() => CanExecuteOnSelectedSource() && EditingGroupCopy.ServerSources.IndexOf(SelectedSource) > 0;

		private bool CanExecuteMoveSelectedSourceDown() => CanExecuteOnSelectedSource() && EditingGroupCopy.ServerSources.IndexOf(SelectedSource) < EditingGroupCopy.ServerSources.Count - 1;

		private bool CanExecuteDeleteAllFallbackAddresses() => CanExecuteOnSelectedSource() && SelectedSource.FallbackIpAddresses.Any();
		#endregion
		#endregion

		#region External Event Handlers
		private async void HandleResolverRemoved(Guid removedResolverId)
		{
			List<Task> tasks = [];

			Application.Current.Dispatcher.Invoke(() =>
			{
				if (EditingGroupCopy != null)
					StopListeningToChanges(EditingGroupCopy);

				try
				{
					var modifiedGroups = new List<UpstreamGroup>();

					if (EditingGroupCopy != null)
					{
						foreach (var source in EditingGroupCopy.ServerSources.Where(s => s.ResolverId == removedResolverId))
							source.ResolverId = null;

						if (SelectedSource?.ResolverId == removedResolverId)
							SelectedSource.ResolverId = null;
					}

					foreach (var group in _groupService.AllConfigs)
					{
						bool wasModified = false;

						foreach (var source in group.ServerSources)
						{
							if (source.ResolverId == removedResolverId)
							{
								source.ResolverId = null;
								wasModified = true;
							}
						}

						if (wasModified)
							modifiedGroups.Add(group);
					}

					if (modifiedGroups.Any())
						foreach (var groupToSave in modifiedGroups)
							tasks.Add(_groupService.SaveChangesAsync(groupToSave));
				}
				finally
				{
					if (EditingGroupCopy != null)
					{
						StartListeningToChanges(EditingGroupCopy);
						ValidateEditingCopy();
					}
				}
			});

			if (tasks.Any())
				await Task.WhenAll(tasks);
		}

		private void HandleResolverRenamed(Guid resolverId, string newName)
		{
			if (SelectedSource?.ResolverId == resolverId)
				UpdateAssociatedResolverName();
		}
		#endregion

		#region Other Commands & Helpers
		#region Copy Link Code
		private async Task ExecuteCopyLinkCode(UpstreamGroup group)
		{
			if (group is null || !_canExecuteCopy) return;

			try
			{
				_canExecuteCopy = false;
				(CopyLinkCodeCommand as AsyncCommand<UpstreamGroup>)?.RaiseCanExecuteChanged();

				var linkCode = Base64Utils.EncodeString(group.Id.ToString());
				for (int i = 0; i < 5; i++)
				{
					try
					{
						Clipboard.SetText(linkCode);
						break;
					}
					catch (System.Runtime.InteropServices.COMException) { await Task.Delay(50); }
				}
				await Task.Delay(500);
			}
			finally
			{
				_canExecuteCopy = true;
				(CopyLinkCodeCommand as AsyncCommand<UpstreamGroup>)?.RaiseCanExecuteChanged();
			}
		}

		private bool CanExecuteCopyLinkCode(UpstreamGroup group) => group != null && !_isBusy && _canExecuteCopy;
		#endregion

		#region General CanExecute Predicates
		private bool CanExecuteWhenNotBusy() => !_isBusy;

		private bool CanExecuteOnEditableGroup() => GroupSelector.SelectedItem != null && !GroupSelector.SelectedItem.IsBuiltIn && !_isBusy ;
		#endregion
		#endregion

		#region Disposal
		public void Dispose()
		{
			_resolverService.ConfigRemoved -= HandleResolverRemoved;
			_resolverService.ConfigRenamed -= HandleResolverRenamed;
			if (_editingGroupCopy != null)
				StopListeningToChanges(_editingGroupCopy);
			GC.SuppressFinalize(this);
		}
		#endregion
	}
}