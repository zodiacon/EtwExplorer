using System.Windows;

namespace EtwExplorer.Infrastructure {
	sealed class DialogService : IDialogService {
		public TViewModel CreateDialog<TViewModel, TWindow>()
			where TViewModel : DialogViewModelBase
			where TWindow : Window, new() {
			var window = new TWindow();
			var viewModel = (TViewModel?)Activator.CreateInstance(typeof(TViewModel), window)
				?? throw new InvalidOperationException($"Could not create {typeof(TViewModel).Name}");
			window.DataContext = viewModel;
			window.Owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current?.MainWindow;
			if (window.Owner != null)
				window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
			return viewModel;
		}
	}
}
