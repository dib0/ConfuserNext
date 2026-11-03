using System;
using System.Windows.Input;

namespace ConfuserNext {
	public class RelayCommand : ICommand {
		readonly Func<bool> canExecute;
		readonly Action execute;

		public RelayCommand(Action execute, Func<bool> canExecute = null) {
			this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
			this.canExecute = canExecute;
		}

		public event EventHandler CanExecuteChanged {
			add { CommandManager.RequerySuggested += value; }
			remove { CommandManager.RequerySuggested -= value; }
		}

		public bool CanExecute(object parameter) {
			return canExecute == null || canExecute();
		}

		public void Execute(object parameter) {
			execute();
		}
	}

	public class RelayCommand<T> : ICommand {
		readonly Func<T, bool> canExecute;
		readonly Action<T> execute;

		public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null) {
			this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
			this.canExecute = canExecute;
		}

		public event EventHandler CanExecuteChanged {
			add { CommandManager.RequerySuggested += value; }
			remove { CommandManager.RequerySuggested -= value; }
		}

		public bool CanExecute(object parameter) {
			return canExecute == null || canExecute((T)parameter);
		}

		public void Execute(object parameter) {
			execute((T)parameter);
		}
	}
}
