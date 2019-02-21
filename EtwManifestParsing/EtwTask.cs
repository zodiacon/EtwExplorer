using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace EtwManifestParsing {
	public sealed class EtwOpcode {
		public string Name { get; internal set; }
		public string Message { get; internal set; }
		public int Value { get; internal set; }
	}

	public sealed class EtwTask {
		public int Value { get; }
		public string Name { get; }

		public EtwOpcode[] Opcodes { get; }

		internal EtwTask(XElement element, EtwManifest manifest) {
			Name = (string)element.Attribute("name");
			Value = (int)element.Attribute("value");

			Opcodes = element.Descendants("opcode").Select(node => new EtwOpcode {
				Name = (string)node.Attribute("name"),
				Message = GetString((string)node.Attribute("message"), manifest),
				Value = (int)node.Attribute("value")
			}).ToArray();
		}

		private string GetString(string name, EtwManifest manifest) {
			return manifest.GetString(name.Substring(9, name.Length - 10));
		}

		public override string ToString() => $"{Name} {Value} {Opcodes.Length} opcodes";
	}
}
