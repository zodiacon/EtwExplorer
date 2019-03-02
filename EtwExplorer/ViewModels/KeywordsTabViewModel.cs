using EtwManifestParsing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EtwExplorer.ViewModels
{
	class KeywordsTabViewModel : TabViewModelBase {
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
