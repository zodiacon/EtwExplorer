using EtwManifestParsing;

namespace EtwExplorer.ViewModels {

	sealed class EtwEventViewModel : BindableBase {
		public EtwEvent Event { get; }
		EtwManifest _manifest;

		public EtwEventViewModel(EtwEvent evt, EtwManifest manifest) {
			Event = evt;
			_manifest = manifest;
		}

		public IEnumerable<EtwTemplateData> EventDetails => _manifest.Templates.First(t => t.Id == Event.Template).Items;

	}
}
