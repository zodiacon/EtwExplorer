using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EtwManifestParsing {
	public sealed class EtwKeyword {
		public string Name { get; internal set; }
		public string Message { get; internal set; }
		public ulong Mask { get; internal set; }

		public override string ToString() => $"{Name} {Message} 0x{Mask:X}";

	}
}
