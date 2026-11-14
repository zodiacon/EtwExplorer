namespace EtwExplorer.ViewModels {
	abstract class TabViewModelBase : BindableBase {
		public abstract string Icon { get; }
		public abstract string Header { get; }
	}
}
