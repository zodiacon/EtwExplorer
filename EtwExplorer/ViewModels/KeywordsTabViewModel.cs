using EtwManifestParsing;

namespace EtwExplorer.ViewModels {
	sealed class KeywordsTabViewModel : TabViewModelBase {
		public override string Icon => "/icons/keywords.ico";

		public override string Header => "Keywords";

		public EtwKeyword[] Keywords { get; }

		public KeywordsTabViewModel(EtwManifest manifest) {
			Keywords = manifest.Keywords;
		}

		public KeywordsTabViewModel(EtwKeyword[] keywords) {
			Keywords = keywords;
		}
	}
}
