using EtwManifestParsing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EtwExplorer.ViewModels {
	sealed class SummaryTabViewModel : TabViewModelBase {
		public override string Icon => "/icons/summary.ico";

		public override string Header => "Summary";

		readonly EtwManifest _manifest;
		public SummaryTabViewModel(EtwManifest manifest) {
			_manifest = manifest;
		}

		public string ProviderName => _manifest.ProviderName;
		public Guid ProviderGuid => _manifest.ProviderGuid;
		public string ProviderSymbol => _manifest.ProviderSymbol;
		public int EventCount => _manifest.Events.Length;
		public int KeywordCount => _manifest.Keywords.Length;
		public int? TaskCount => _manifest.Tasks?.Length;
		public int TemplateCount => _manifest.Templates.Length;
	}
}
