using EtwManifestParsing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace EtwExplorer.ViewModels {
	sealed class StringsTabViewModel : TabViewModelBase {
		public class Item {
			public string Name { get; set; }
			public string Value { get; set; }
		}

		public override string Icon => "/icons/strings.ico";

		public override string Header => "Strings";

		readonly EtwManifest _manifest;
		public StringsTabViewModel(EtwManifest manifest) {
			Debug.Assert(manifest != null);

			_manifest = manifest;
			Strings = _manifest.StringTable.Select(pair => new Item { Name = pair.Key, Value = pair.Value }).ToArray();
		}

		public Item[] Strings { get; }

		string _searchText;
		public string SearchText {
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
