using EtwExplorer.Infrastructure;
using Microsoft.Diagnostics.Tracing.Session;
using System.Windows;
using System.Windows.Data;

namespace EtwExplorer.ViewModels {
	sealed record Provider(string Name, Guid Guid) {
		/// <summary>Registered providers with manifest-style names have no spaces in them.</summary>
		public bool IsLikelyName => !Name.Contains(' ');
	}

	sealed class EtwProviderSelectionViewModel : DialogViewModelBase {
		static readonly Provider[] _providers = [.. TraceEventProviders.GetPublishedProviders()
			.Select(g => new Provider(TraceEventProviders.GetProviderName(g), g))];

		public EtwProviderSelectionViewModel(Window dialog) : base(dialog) {
			CollectionViewSource.GetDefaultView(Providers).Filter = null;
			IsLikely = true;
		}

		public IEnumerable<Provider> Providers => _providers;

		Provider? _selectedProvider;
		public Provider? SelectedProvider {
			get => _selectedProvider;
			set => SetProperty(ref _selectedProvider, value);
		}

		string? _searchText;
		public string? SearchText {
			get => _searchText;
			set {
				if (SetProperty(ref _searchText, value)) {
					var cvs = CollectionViewSource.GetDefaultView(Providers);
					if (string.IsNullOrWhiteSpace(_searchText)) {
						cvs.Filter = IsLikely ? obj => ((Provider)obj).IsLikelyName : null;
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
					cvs.Filter = IsLikely ? obj => ((Provider)obj).IsLikelyName : null;

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
