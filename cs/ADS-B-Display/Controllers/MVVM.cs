using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ADS_B_Display
{
    public abstract class NotifyPropertyChangedBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected bool SetProperty<T>(
            ref T storage,
            T value,
            [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged(
            [CallerMemberName] string propertyName = null)
        => PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName)
        );
    }

    public class DelegateCommand : ICommand
    {
        readonly Action<object> mExecute;
        readonly Predicate<object> mCanExecute;

        /// <summary>
        /// Initializes a new instance of the DelegateCommand class.
        /// </summary>
        /// <param name="execute">execute function </param>
        /// <param name="canExecute">can execute function</param>
        public DelegateCommand(
                        Action<object> execute,
                        Predicate<object> canExecute)
        {
            mExecute = execute ?? throw new NullReferenceException("execute can not null");
            mCanExecute = canExecute;
        }

        /// <summary>
        /// Initializes a new instance of the DelegateCommand class.
        /// </summary>
        /// <param name="execute">indicate an execute function</param>
        public DelegateCommand(Action<object> execute)
          : this(execute, null)
        {
        }

        /// <summary>
        /// can executes event handler
        /// </summary>
        public event EventHandler CanExecuteChanged {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        /// <summary>
        /// implement of icommand can execute method
        /// </summary>
        /// <param name="parameter">parameter by default of icomand interface</param>
        /// <returns>can execute or not</returns>
        public bool CanExecute(object parameter)
        {
            return mCanExecute == null
                            ? true
                            : mCanExecute(parameter);
        }

        /// <summary>
        /// implement of icommand interface execute method
        /// </summary>
        /// <param name="parameter">parameter by default of icomand interface</param>
        public void Execute(object parameter)
        {
            mExecute?.Invoke(parameter);
        }

        /// <summary>
        /// CanExecute 값을 즉시 업데이트 하고 싶을 때 호출
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
