using System.Windows;
using System.Windows.Input;

namespace EtwExplorer.Infrastructure {
	public abstract class DialogViewModelBase : BindableBase {
		protected DialogViewModelBase(Window dialog) {
			Dialog = dialog;
			OKCommand = new DelegateCommand(OnOK);
			CancelCommand = new DelegateCommand(OnCancel);
		}

		protected Window Dialog { get; }

		public ICommand OKCommand { get; }
		public ICommand CancelCommand { get; }

		public bool? ShowDialog() => Dialog.ShowDialog();

		protected virtual void OnOK() {
			Dialog.DialogResult = true;
		}

		protected virtual void OnCancel() {
			Dialog.DialogResult = false;
		}
	}
}
