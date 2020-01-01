using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace EtwManifestParsing {
	public sealed class EtwTemplateData {
		public string Name { get; set; }
		public string Type { get; set; }
	}

	public sealed class EtwTemplate {
		public string Id { get; }
		public EtwTemplateData[] Items { get; }

		internal EtwTemplate(XElement element) {
			Id = (string)element.Attribute("tid");
			Items = element.DescendantNodes().OfType<XElement>().Select(node => new EtwTemplateData {
				Name = (string)node.Attribute("name"),
				Type = ((string)node.Attribute("inType")).Substring(4)
			}).ToArray();
		}

		internal EtwTemplate(string id, EtwTemplateData[] items)
		{
			Id = id;
			Items = items;
		}

		public override string ToString() => $"{Id} {Items.Length} template items";
	}
}
