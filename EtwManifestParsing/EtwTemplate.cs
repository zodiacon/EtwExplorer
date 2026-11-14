using System.Xml.Linq;

namespace EtwManifestParsing {
	public sealed class EtwTemplateData {
		public required string Name { get; init; }
		public required string Type { get; init; }
	}

	public sealed class EtwTemplate {
		public string Id { get; }
		public EtwTemplateData[] Items { get; }

		internal EtwTemplate(XElement element) {
			// tid/name/inType are required by the manifest schema; a missing one throws and
			// is surfaced by ManifestParser.Parse as "Failed to parse manifest XML".
			Id = element.Attribute("tid")!.Value;
			Items = [.. element.DescendantNodes().OfType<XElement>().Select(node => new EtwTemplateData {
				Name = node.Attribute("name")!.Value,
				Type = node.Attribute("inType")!.Value[4..] // "win:UInt32" -> "UInt32"
			})];
		}

		internal EtwTemplate(string id, EtwTemplateData[] items) {
			Id = id;
			Items = items;
		}

		public override string ToString() => $"{Id} {Items.Length} template items";
	}
}
