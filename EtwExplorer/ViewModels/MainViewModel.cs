using EtwExplorer.Views;
using EtwManifestParsing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Zodiacon.WPF;

namespace EtwExplorer.ViewModels {
	class MainViewModel : BindableBase {
		public readonly IUIServices UI;
		ObservableCollection<TabViewModelBase> _tabs = new ObservableCollection<TabViewModelBase>();
		public IList<TabViewModelBase> Tabs => _tabs;

		public EtwManifest Manifest { get; private set; }

		string _filename;
		public string FileName {
			get => _filename;
			set => SetProperty(ref _filename, value);
		}

		public MainViewModel(IUIServices ui, EtwManifest manifest = null) {
			UI = ui;
			Manifest = manifest;
			if (manifest != null)
				AddTabs();
		}

		private void AddTabs() {
			Tabs.Add(new SummaryTabViewModel(Manifest));
			Tabs.Add(new EventsTabViewModel(Manifest));
			Tabs.Add(new StringsTabViewModel(Manifest));
			Tabs.Add(new XmlTabViewModel(Manifest.Xml));

			SelectedTab = Tabs[0];
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
			Tabs.Clear();
			FileName = null;
			RaisePropertyChanged(nameof(Manifest));
		}

		public ICommand OpenRegisteredCommand => new DelegateCommand(() => {
			var vm = UI.DialogService.CreateDialog<EtwProviderSelectionViewModel, EtwProviderSelectionDialog>();
			if (true == vm.ShowDialog()) {
				try {
					var xml = RegisteredTraceEventParser.GetManifestForRegisteredProvider(vm.SelectedProvider.Guid);
					if (vm.CloseCurrentManifest)
						DoClose();
					DoOpenXml(xml);
				}
				catch (Exception ex) {
					UI.MessageBoxService.ShowMessage(ex.Message, Constants.AppTitle);
				}
			}
		});

		private void DoOpenXml(string xml) {
			var manifest = ManifestParser.Parse(xml);
			if (Manifest == null) {
				Manifest = manifest;
				RaisePropertyChanged(nameof(Manifest));
				AddTabs();
			}
			else {
				var vm = new MainViewModel(UI, manifest);
				var win = new MainWindow { DataContext = vm };
				win.Show();
			}
		}

		TabViewModelBase _selectedTab;
		public TabViewModelBase SelectedTab {
			get => _selectedTab;
			set => SetProperty(ref _selectedTab, value);
		}

		private void DoOpenFile(string filename) {
			try {
				var manifest = ManifestParser.Parse(File.ReadAllText(filename));
				if (Manifest == null) {
					Manifest = manifest;
					FileName = filename;
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
	}
}
