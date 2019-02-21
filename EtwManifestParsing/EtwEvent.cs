using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EtwManifestParsing {
	public enum EventLevel {
	}

	public sealed class EtwEvent {
		public int Value { get; internal set; }
		public string Symbol { get; internal set; }
		public int Version { get; internal set; }
		public string Opcode { get; internal set; }
		public string Level { get; internal set; }
		public string Template { get; internal set; }
		public string Keyword { get; internal set; }
		public string Task { get; internal set; }

		public override string ToString() => $"{Symbol}={Value} {Opcode} {Template}";
	}
}
