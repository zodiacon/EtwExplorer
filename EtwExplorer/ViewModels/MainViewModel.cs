using EtwExplorer.Infrastructure;
using EtwExplorer.Views;
using EtwManifestParsing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace EtwExplorer.ViewModels {
	sealed class MainViewModel : BindableBase {
		public readonly IUIServices UI;
		ObservableCollection<TabViewModelBase> _tabs = new();
		public IList<TabViewModelBase> Tabs => _tabs;

		public EtwManifest? Manifest { get; private set; }
		public EtwKeyword[]? Keywords { get; private set; }

		string? _filename;
		public string? FileName {
			get => _filename;
			set => SetProperty(ref _filename, value);
		}

		string? _providerName;
		public string? ProviderName {
			get => _providerName;
			set => SetProperty(ref _providerName, value);
		}

		public MainViewModel(IUIServices ui, EtwManifest? manifest = null) {
			UI = ui;
			Manifest = manifest;
			if (manifest != null) {
				AddTabs();
				ProviderName = manifest.ProviderName;
			}
		}

		public MainViewModel(IUIServices ui, EtwKeyword[] keywords, string providerName) {
			UI = ui;
			ProviderName = providerName;
			Keywords = keywords;
			AddTabs();
		}

		private void AddTabs() {
			if (Manifest is { } manifest) {
				Tabs.Add(new SummaryTabViewModel(manifest));
				Tabs.Add(new EventsTabViewModel(manifest));
				Tabs.Add(new KeywordsTabViewModel(manifest));
				Tabs.Add(new StringsTabViewModel(manifest));
				Tabs.Add(new XmlTabViewModel(manifest.Xml));
			}
			else if (Keywords is { } keywords) {
				Tabs.Add(new KeywordsTabViewModel(keywords));
			}
			SelectedTab = Tabs.Count > 0 ? Tabs[0] : null;
		}

		public ICommand ExitCommand => new DelegateCommand(() => Application.Current.Shutdown());
		public ICommand OpenXmlCommand => new DelegateCommand(() => {
			var filename = UI.FileDialogService.GetFileForOpen("Xml Files|*.xml", "Open XML Manifest");
			if (filename == null)
				return;

			DoOpenFile(filename);
		});

		public ICommand CloseCommand => new DelegateCommand(DoClose);

		void DoClose() {
			Manifest = null;
			Keywords = null;
			Tabs.Clear();
			FileName = null;
			RaisePropertyChanged(nameof(Manifest));
		}

		public ICommand OpenRegisteredCommand => new DelegateCommand(() => {
			var vm = UI.DialogService.CreateDialog<EtwProviderSelectionViewModel, EtwProviderSelectionDialog>();
			if (vm.ShowDialog() != true || vm.SelectedProvider is not { } provider)
				return;

			if (vm.CloseCurrentManifest)
				DoClose();
			try {
				var xml = string.Empty;
				try {
					xml = RegisteredTraceEventParser.GetManifestForRegisteredProvider(provider.Guid);
				}
				catch (ApplicationException ae) {
					// look for a WMI EventTrace class instead - but throw the original exception otherwise
					try {
						DoOpenWmiEventTrace(provider.Guid);
						UI.MessageBoxService.ShowMessage($"{ae.Message}\n\nShowing WMI EventTrace class details instead.", Constants.AppTitle, MessageBoxButton.OK, MessageBoxImage.Information);
					}
					catch {
						throw ae;
					}
				}

				if (xml.Length > 0)
					DoOpenXml(xml);

				Keywords = null;
			}
			catch (Exception e) {
				var keywords = TraceEventProviders.GetProviderKeywords(provider.Guid).Select(info => new EtwKeyword {
					Name = info.Name,
					Mask = info.Value,
					Message = info.Description
				}).ToArray();
				UI.MessageBoxService.ShowMessage($"{e.Message}\nShowing keywords only.", Constants.AppTitle, MessageBoxButton.OK, MessageBoxImage.Warning);

				if (vm.CloseCurrentManifest) {
					DoClose();
					Keywords = keywords;
					AddTabs();
				}
				else {
					var winvm = new MainViewModel(UI, keywords, provider.Name);
					var win = new MainWindow { DataContext = winvm };
					win.Show();
				}
			}
		});

		private void DoOpenXml(string xml) {
			DoOpen(ManifestParser.Parse(xml));
		}

		private void DoOpenWmiEventTrace(Guid provider) {
			DoOpen(ManifestParser.ParseWmiEventTraceClass(provider));
		}

		private void DoOpen(EtwManifest manifest) {
			if (Manifest == null) {
				Manifest = manifest;
				RaisePropertyChanged(nameof(Manifest));
				ProviderName = Manifest.ProviderName;
				AddTabs();
			}
			else {
				var vm = new MainViewModel(UI, manifest);
				var win = new MainWindow { DataContext = vm };
				win.Show();
			}
		}

		TabViewModelBase? _selectedTab;
		public TabViewModelBase? SelectedTab {
			get => _selectedTab;
			set => SetProperty(ref _selectedTab, value);
		}

		private void DoOpenFile(string filename) {
			try {
				var manifest = ManifestParser.Parse(File.ReadAllText(filename));
				if (Manifest == null) {
					Manifest = manifest;
					FileName = filename;
					ProviderName = Manifest.ProviderName;
					RaisePropertyChanged(nameof(Manifest));
					AddTabs();
				}
				else {
					var vm = new MainViewModel(UI, manifest);
					vm.FileName = filename;
					var win = new MainWindow { DataContext = vm };
					win.Show();
				}

			}
			catch (Exception ex) {
				UI.MessageBoxService.ShowMessage($"Error: {ex.Message}", Constants.AppTitle);
			}
		}

		public DelegateCommand SaveXmlCommand => new DelegateCommand(() => {
			var filename = UI.FileDialogService.GetFileForSave("XML files|*.xml|All Files|*.*");
			if (filename == null)
				return;
			DoSave(filename);
		}, () => Manifest != null).ObservesProperty(() => Manifest);

		private void DoSave(string filename) {
			if (Manifest is not { } manifest)
				return;
			try {
				File.WriteAllText(filename, manifest.Xml);
			}
			catch (IOException ex) {
				UI.MessageBoxService.ShowMessage(ex.Message, Constants.AppTitle, MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}
	}
}
