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
		public EtwKeyword[] Keywords { get; private set; }

		string _filename;
		public string FileName {
			get => _filename;
			set => SetProperty(ref _filename, value);
		}

		string _providerName;
		public string ProviderName {
			get => _providerName;
			set => SetProperty(ref _providerName, value);
		}

		public MainViewModel(IUIServices ui, EtwManifest manifest = null) {
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
			if (Keywords == null) {
				Tabs.Add(new SummaryTabViewModel(Manifest));
				Tabs.Add(new EventsTabViewModel(Manifest));
				Tabs.Add(new KeywordsTabViewModel(Manifest));
				Tabs.Add(new StringsTabViewModel(Manifest));
				Tabs.Add(new XmlTabViewModel(Manifest.Xml));
			}
			else {
				Tabs.Add(new KeywordsTabViewModel(Keywords));
			}
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
			Keywords = null;
			Tabs.Clear();
			FileName = null;
			RaisePropertyChanged(nameof(Manifest));
		}

		public ICommand OpenRegisteredCommand => new DelegateCommand(() => {
			var vm = UI.DialogService.CreateDialog<EtwProviderSelectionViewModel, EtwProviderSelectionDialog>();
			if (true == vm.ShowDialog()) {
				if (vm.CloseCurrentManifest)
					DoClose();
				try {
					var xml = string.Empty;
					try {
						xml = RegisteredTraceEventParser.GetManifestForRegisteredProvider(vm.SelectedProvider.Guid);
					}
					catch (ApplicationException ae) {
						// look for a WMI EventTrace class instead - but throw the original exception otherwise
						try {
							DoOpenWmiEventTrace(vm.SelectedProvider.Guid);
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
					var keywords = TraceEventProviders.GetProviderKeywords(vm.SelectedProvider.Guid).Select(info => new EtwKeyword {
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
						var winvm = new MainViewModel(UI, keywords, vm.SelectedProvider.Name);
						var win = new MainWindow { DataContext = winvm };
						win.Show();
					}
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
			try {
				File.WriteAllText(filename, Manifest.Xml);
			}
			catch (IOException ex) {
				UI.MessageBoxService.ShowMessage(ex.Message, Constants.AppTitle, MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}
	}
}
