using System;
using System.Collections.Generic;

namespace EtwManifestParsing {
	public class EtwManifest {
		internal EtwManifest(string xml) {
			Xml = xml;
		}

		public string Xml { get; }
		public EtwEvent[] Events { get; internal set; }
		public EtwKeyword[] Keywords { get; internal set; }
		public EtwTemplate[] Templates { get; internal set; }

		public EtwTask[] Tasks { get; internal set; }

		public IDictionary<string, string> StringTable { get; internal set; }
		public string ProviderName { get; internal set; }
		public Guid ProviderGuid { get; internal set; }
		public string ProviderSymbol { get; internal set; }

		public string GetString(string messageId) {
			string value = null;
			StringTable?.TryGetValue(messageId, out value);
			return value;
		}
	}
}