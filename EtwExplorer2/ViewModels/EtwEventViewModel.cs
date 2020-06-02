using EtwManifestParsing;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
