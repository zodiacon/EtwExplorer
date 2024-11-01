using Microsoft.Diagnostics.Tracing.Session;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using Zodiacon.WPF;

namespace EtwExplorer.ViewModels {
	sealed class Provider {
		public string Name { get; set; }
		public Guid Guid { get; set; }
	}

	sealed class EtwProviderSelectionViewModel : DialogViewModelBase {
		static Provider[] _providers;

		static EtwProviderSelectionViewModel() {
			_providers = TraceEventProviders.GetPublishedProviders().Select(g => new Provider {
				Name = TraceEventProviders.GetProviderName(g),
				Guid = g
			}).ToArray();
		}

		public EtwProviderSelectionViewModel(Window dialog) : base(dialog) {
			CollectionViewSource.GetDefaultView(Providers).Filter = null;
			IsLikely = true;
		}

		public IEnumerable<Provider> Providers => _providers;

		Provider _selectedProvider;
		public Provider SelectedProvider {
			get => _selectedProvider;
			set => SetProperty(ref _selectedProvider, value);
		}

		string _searchText;
		public string SearchText {
			get => _searchText;
			set {
				if (SetProperty(ref _searchText, value)) {
					var cvs = CollectionViewSource.GetDefaultView(Providers);
					if (string.IsNullOrWhiteSpace(_searchText)) {
						cvs.Filter = IsLikely ? obj => (((Provider)obj).Name.IndexOf(' ') < 0) : (Predicate<object>)null;
					}
					else {
						string text = _searchText.ToLower();
						cvs.Filter = obj => {
							var provider = (Provider)obj;
							return provider.Name.ToLower().Contains(text) || provider.Guid.ToString().ToLower().Contains(text);
						};
					}
				}
			}
		}

		bool _closeCurrentManifest = true;
		public bool CloseCurrentManifest {
			get => _closeCurrentManifest;
			set => SetProperty(ref _closeCurrentManifest, value);
		}

		bool _isLikely;
		public bool IsLikely {
			get => _isLikely;
			set {
				if (SetProperty(ref _isLikely, value)) {
					var cvs = CollectionViewSource.GetDefaultView(Providers);
					if (!string.IsNullOrWhiteSpace(SearchText))
						return;
					cvs.Filter = IsLikely ? obj => (((Provider)obj).Name.IndexOf(' ') < 0) : (Predicate<object>)null;

				}
			}
		}

        protected override void OnOK() {
			if (SelectedProvider == null)
				return;

            base.OnOK();
        }
    }
}
