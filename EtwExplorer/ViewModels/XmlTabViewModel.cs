using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EtwExplorer.ViewModels {
	sealed class XmlTabViewModel : TabViewModelBase {
		public override string Icon => "/icons/xml.ico";

		public override string Header => "XML";

		public string Xml { get; }
		public XmlTabViewModel(string xml) {
			Xml = xml;
		}

		
	}
}
