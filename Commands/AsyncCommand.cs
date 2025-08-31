using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SNIBypassGUI.Commands
{
    public class AsyncCommand : ICommand, INotifyPropertyChanged
    {
        private readonly Func<Task> _execute;
        private readonly Func<bool>? _canExecute;

        private bool _isExecuting;
        public bool IsExecuting
        {
            get => _isExecuting;
            private set
            {
                if (_isExecuting != value)
                {
                    _isExecuting = value;
                    OnPropertyChanged();
                    RaiseCanExecuteChanged();
                }
            }
        }

        public AsyncCommand(Func<Task> execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => !IsExecuting && (_canExecute?.Invoke() ?? true);

        public async void Execute(object? parameter)
        {
            if (!CanExecute(parameter)) return;

            IsExecuting = true;
            try
            {
                await _execute();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                throw;
            }
            finally
            {
                IsExecuting = false;
            }
        }

        public event EventHandler? CanExecuteChanged;
        public event PropertyChangedEventHandler? PropertyChanged;

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class AsyncCommand<T> : ICommand, INotifyPropertyChanged
    {
        private readonly Func<T?, Task> _execute;
        private readonly Func<T?, bool>? _canExecute;

        private bool _isExecuting;
        public bool IsExecuting
        {
            get => _isExecuting;
            private set
            {
                if (_isExecuting != value)
                {
                    _isExecuting = value;
                    OnPropertyChanged();
                    RaiseCanExecuteChanged();
                }
            }
        }

        public AsyncCommand(Func<T?, Task> execute, Func<T?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
        {
            if (IsExecuting) return false;

            if (parameter == null && default(T) != null) return false;

            if (_canExecute == null) return true;

            try
            {
                return parameter is T t && _canExecute(t);
            }
            catch
            {
                return false;
            }
        }

        public AsyncCommand(Func<T?, Task> execute, Func<bool> canExecute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = _ => canExecute();
        }

        public async void Execute(object? parameter)
        {
            if (!CanExecute(parameter)) return;

            IsExecuting = true;
            try
            {
                if (parameter is T t)
                {
                    await _execute(t);
                }
                else if (parameter == null && default(T) == null)
                {
                    await _execute(default);
                }
                else { }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                throw;
            }
            finally
            {
                IsExecuting = false;
            }
        }

        public event EventHandler? CanExecuteChanged;
        public event PropertyChangedEventHandler? PropertyChanged;

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
