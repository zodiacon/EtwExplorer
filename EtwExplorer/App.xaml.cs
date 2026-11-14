using EtwExplorer.Infrastructure;
using EtwExplorer.ViewModels;
using System.Windows;

namespace EtwExplorer {
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application {
		protected override void OnStartup(StartupEventArgs e) {
			base.OnStartup(e);

			var ui = new UIServicesDefaults();
			var vm = new MainViewModel(ui);
			var win = new MainWindow { DataContext = vm };
			win.Show();
			ui.MessageBoxService.SetOwner(win);
			ShutdownMode = ShutdownMode.OnLastWindowClose;
		}
	}
}
