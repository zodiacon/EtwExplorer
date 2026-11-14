using EtwManifestParsing;
using System.Windows.Data;

namespace EtwExplorer.ViewModels {
	sealed class StringsTabViewModel : TabViewModelBase {
		public sealed record Item(string Name, string Value);

		public override string Icon => "/icons/strings.ico";

		public override string Header => "Strings";

		public StringsTabViewModel(EtwManifest manifest) {
			Strings = manifest.StringTable is { } table
				? [.. table.Select(pair => new Item(pair.Key, pair.Value))]
				: [];
		}

		public Item[] Strings { get; }

		string? _searchText;
		public string? SearchText {
			get => _searchText;
			set {
				if (SetProperty(ref _searchText, value)) {
					var cvs = CollectionViewSource.GetDefaultView(Strings);
					if (string.IsNullOrWhiteSpace(_searchText))
						cvs.Filter = null;
					else {
						string text = _searchText.ToLower();
						cvs.Filter = obj => {
							var item = (Item)obj;
							return item.Name.ToLower().Contains(text) || item.Value.ToLower().Contains(text);
						};
					}
				}
			}
		}

	}
}
