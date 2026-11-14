using EtwManifestParsing;
using System.Windows.Data;

namespace EtwExplorer.ViewModels {
	sealed class EventsTabViewModel : TabViewModelBase {
		public override string Icon => "/icons/events.ico";

		public override string Header => "Events";

		readonly EtwManifest _manifest;
		public EventsTabViewModel(EtwManifest manifest) {
			_manifest = manifest;
			Events = [.. _manifest.Events.Select(evt => new EtwEventViewModel(evt, _manifest))];
		}

		public EtwEventViewModel[] Events { get; }

		string? _searchText;
		public string? SearchText {
			get => _searchText;
			set {
				if (SetProperty(ref _searchText, value)) {
					var cvs = CollectionViewSource.GetDefaultView(Events);
					if (string.IsNullOrWhiteSpace(_searchText))
						cvs.Filter = null;
					else {
						string text = _searchText;
						cvs.Filter = obj => {
							var evt = ((EtwEventViewModel)obj).Event;
							return Matches(evt.Symbol, text) || Matches(evt.Opcode, text) || Matches(evt.Task, text);
						};
					}
				}
			}
		}

		// symbol/opcode/task are optional manifest attributes; a missing one simply doesn't match
		static bool Matches(string? value, string text) => value?.Contains(text, StringComparison.OrdinalIgnoreCase) == true;
	}
}
