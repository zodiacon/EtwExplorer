using EtwManifestParsing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace EtwExplorer.ViewModels {
	sealed class EventsTabViewModel : TabViewModelBase {
		public override string Icon => "/icons/events.ico";

		public override string Header => "Events";

		readonly EtwManifest _manifest;
		public EventsTabViewModel(EtwManifest manifest) {
			_manifest = manifest;
			Events = _manifest.Events.Select(evt => new EtwEventViewModel(evt, _manifest)).ToArray();
		}

		public EtwEventViewModel[] Events { get; }

		string _searchText;
		public string SearchText {
			get => _searchText;
			set {
				if (SetProperty(ref _searchText, value)) {
					var cvs = CollectionViewSource.GetDefaultView(Events);
					if (string.IsNullOrWhiteSpace(_searchText))
						cvs.Filter = null;
					else {
						string text = _searchText.ToLower();
						cvs.Filter = obj => {
							var evt = ((EtwEventViewModel)obj).Event;
							return evt.Symbol.ToLower().Contains(text) || evt.Opcode.ToLower().Contains(text) || evt.Task.ToLower().Contains(text);
						};
					}
				}
			}
		}
	}
}
