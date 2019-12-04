using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace EtwManifestParsing {
	public static class ManifestParser {
		public static EtwManifest Parse(XElement element) {
			var manifest = new EtwManifest(element.ToString());
			try {
				var ns = element.GetDefaultNamespace();

				var stringTable = element.Descendants(ns + "stringTable").FirstOrDefault();
				if (stringTable != null) {
					var strings = stringTable.DescendantNodes().OfType<XElement>().ToArray();
					var table = new Dictionary<string, string>(strings.Length);
					Array.ForEach(strings, node => { try { table.Add((string)node.Attribute("id"), (string)node.Attribute("value")); } catch { } });
					manifest.StringTable = table;
				}

				var providerElement = element.Descendants(ns + "provider").First();
				manifest.ProviderName = (string)providerElement.Attribute("name");
				manifest.ProviderSymbol = (string)providerElement.Attribute("symbol");
				manifest.ProviderGuid = Guid.Parse((string)providerElement.Attribute("guid"));

				var events = from node in element.Descendants(ns + "event")
							 let level = GetString(node.Attribute("level"))
							 select new EtwEvent {
								 Value = (int)node.Attribute("value"),
								 Symbol = (string)node.Attribute("symbol"),
								 Level = level.Substring(level.IndexOf(':') + 1),
								 Opcode = GetString(node.Attribute("opcode")),
								 Version = (int)node.Attribute("version"),
								 Template = (string)node.Attribute("template"),
								 Keyword = (string)node.Attribute("keywords"),
								 Task = (string)node.Attribute("task")
							 };

				manifest.Events = events.ToArray();

				var keywords = element.Descendants(ns + "keyword").Select(node => new EtwKeyword {
					Name = (string)node.Attribute("name"),
					Mask = ulong.Parse(((string)node.Attribute("mask")).Substring(2), System.Globalization.NumberStyles.HexNumber),
					Message = GetMessageString(manifest, (string)node.Attribute("message"))
				});

				manifest.Keywords = keywords.ToArray();

				var templates = element.Descendants(ns + "template").Select(node => new EtwTemplate(node));
				manifest.Templates = templates.ToArray();

				var tasks = element.Descendants(ns + "task").Select(node => new EtwTask(node, manifest));
				manifest.Tasks = tasks.ToArray();

				return manifest;
			}
			catch (Exception ex) {
				throw new ApplicationException("Failed to parse manifest XML", ex);
			}
		}

		private static string GetString(XAttribute attribute) {
			if (attribute == null)
				return string.Empty;
			var value = (string)attribute;
			return value.Substring(value.IndexOf(':') + 1);
		}

		private static string GetMessageString(EtwManifest manifest, string message) {
			if (message.StartsWith("$")) {
				message = message.Substring(9, message.Length - 10);
				return manifest.GetString(message);
			}
			return message;
		}

		public static EtwManifest Parse(string xml) {
			return Parse(XElement.Parse(xml));
		}

	}
}
