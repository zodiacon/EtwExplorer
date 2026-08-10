using Microsoft.Diagnostics.Tracing.Session;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace EtwManifestParsing {
	public static class ManifestParser {
		// EventTypeName and DisplayName are *amended* qualifiers: WMI keeps them in
		// the locale namespace (root\wmi\ms_409) and does not return them unless you
		// ask. Without this, Process_V2_TypeGroup1 looks like it has no opcode names
		// at all, when in fact it carries EventTypeName{"Start", "End", "DCStart",
		// "DCEnd", "Defunct"} - which is where TDH gets the names it displays.
		static EnumerationOptions AmendedQualifiers => new EnumerationOptions { UseAmendedQualifiers = true };

		// Generic opcodes from evntrace.h, used only when a class declares no
		// EventTypeName for an opcode. Where evntrace.h gives two names for one
		// value (End/Stop, Dequeue/Resume, Checkpoint/Suspend) the classic ETW
		// spelling is used. 0x0A/0x0B are the Process/Thread convention rather than
		// truly generic, but no other provider assigns them a conflicting meaning.
		static readonly Dictionary<int, string> StandardOpcodeNames = new Dictionary<int, string> {
			{ 0x00, "Info" }, { 0x01, "Start" }, { 0x02, "End" }, { 0x03, "DCStart" },
			{ 0x04, "DCEnd" }, { 0x05, "Extension" }, { 0x06, "Reply" }, { 0x07, "Dequeue" },
			{ 0x08, "Checkpoint" }, { 0x09, "WinEvtSend" }, { 0x0A, "Load" },
			{ 0x0B, "Terminate" }, { 0xF0, "WinEvtReceive" },
		};

		/// <summary>Reads a qualifier that may be declared as a scalar or an array.</summary>
		static string[] ToStringArray(object value) {
			if (value == null)
				return null;
			if (value is string scalar)
				return new[] { scalar };
			if (value is Array array) {
				var values = new List<string>(array.Length);
				foreach (var element in array)
					values.Add(element?.ToString() ?? string.Empty);
				return values.ToArray();
			}
			return null;
		}

		/// <summary>
		/// The versioned spelling of an event name, as a manifest writes it:
		/// "ProcessStart" at version 0, "ProcessStart_V4" at version 4.
		/// Version 0 takes no suffix.
		/// </summary>
		static string VersionedName(string eventName, int version) {
			return version == 0 ? eventName : $"{eventName}_V{version}";
		}

		/// <summary>"Process_V2" -> "Process". Leaves names without the suffix alone.</summary>
		static string StripVersionSuffix(string name) {
			if (string.IsNullOrEmpty(name))
				return name;
			var match = new Regex(@"^(?<Name>.+)_V\d+$").Match(name);
			return match.Success ? match.Groups["Name"].Value : name;
		}

		/// <summary>
		/// The name for one opcode of an event class.
		/// </summary>
		/// <remarks>
		/// EventTypeName is positional against EventType, but a class may declare fewer
		/// names than EventTypes - FileOperation declares one, FileTrace, against 43 IRP
		/// major function codes. That names the group, so the index clamps to the last
		/// name rather than falling through: IRP_MJ_CLOSE is not an "End".
		///
		/// The generic evntrace.h names are therefore only reached by a class that
		/// declares no EventTypeName at all, of which there are two - the Dedup and Vss
		/// WPP classes, where "Start" beats repeating the class name.
		/// </remarks>
		static string OpcodeName(string[] eventTypeNames, int index, int opcode, string templateClass) {
			if (eventTypeNames != null && eventTypeNames.Length > 0) {
				var name = eventTypeNames[Math.Min(index, eventTypeNames.Length - 1)];
				if (!string.IsNullOrEmpty(name))
					return name;
			}
			if (StandardOpcodeNames.TryGetValue(opcode, out var standard))
				return standard;
			return StripVersionSuffix(templateClass);
		}

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

        public static EtwManifest ParseWmiEventTraceClass(Guid provider) {
            // we make a best effort attempt to fit the metadata of this Legacy (MOF) provider into the instrumentation manifest format

            // we need to find the EventTrace class where the Guid class qualifier matches our provider Guid
            // afaik you can't query for qualifiers...just classes and properties.  :-/
            // so we loop through all of the EventTrace classes and compare
            var providerSearcher = new ManagementObjectSearcher("root\\WMI", $"SELECT * FROM meta_class WHERE __superclass = 'EventTrace'", AmendedQualifiers);
            ManagementClass providerClass = null;
            foreach (ManagementClass candidateProviderClass in providerSearcher.Get()) {
                foreach (QualifierData qd in candidateProviderClass.Qualifiers) {
                    if (qd.Name.ToLower() == "guid" && new Guid((string)qd.Value) == provider) {
                        providerClass = candidateProviderClass;
                        break; // found
                    }
                }

                if (providerClass != null)
                    break; // found
            }

            if (providerClass == null) // not found
                throw new ApplicationException($"Provider {provider} has no corresponding EventTrace class in WMI Repository");

            // One query for the provider's entire subtree, then walk it in memory.
            // Both the MOF text and the event extraction below need the same classes.
            var subtree = GetSubtreeBySuperclass((string)providerClass["__CLASS"]);

            var mof = ToMofString(providerClass, subtree);

            var manifest = new EtwManifest(mof)
            {
                ProviderGuid = provider,
                ProviderSymbol = (string)providerClass["__CLASS"]
            };

            var events = new SortedDictionary<string, EtwEvent>();
            var templates = new List<EtwTemplate>();
            var stringTable = new Dictionary<string, string>();

            // the provider name is usually in the Description Qualifier for the EventTrace class (but not always?)
            // and the keywords are properties for the EventTrace class
            // but we can already get both of these easily from Microsoft.Diagnostics.Tracing
            manifest.ProviderName = TraceEventProviders.GetProviderName(provider);
            manifest.Keywords = TraceEventProviders.GetProviderKeywords(provider).Select(info => new EtwKeyword
            {
                Name = info.Name,
                Mask = info.Value,
                Message = info.Description
            }).ToArray();

            // event details are in the grandchildren of the top-level (EventTrace) provider class
            // WMI EventTrace children ~ a versioned category grouping
            // WMI EventTrace grandchildren ~ instrumentation manifest templates
            // note - event version can be set on the category and/or the event
            foreach (var categoryVersionClass in ChildrenOf(subtree, (string)providerClass["__CLASS"]))
            {
                var categoryVersion = 0;
                var category = string.Empty;
                var description = string.Empty;
                var displayName = string.Empty;
                var displayNames = string.Empty;
                foreach (QualifierData qd in categoryVersionClass.Qualifiers) {
                    if (qd.Value.GetType() == typeof(Int32) && qd.Name.ToLower() == "eventversion")
                        categoryVersion = (Int32)qd.Value;
                    else if (qd.Value.GetType() == typeof(String) && qd.Name.ToLower() == "guid")
                        category = (string)qd.Value;
                    else if (qd.Value.GetType() == typeof(String) && qd.Name.ToLower() == "description")
                        description = (string)qd.Value;
                    else if (qd.Value.GetType() == typeof(String) && qd.Name.ToLower() == "displayname")
                        displayName = (string)qd.Value;
                    else if (qd.Value.GetType() == typeof(String) && qd.Name.ToLower() == "displaynames")
                        displayNames = (string)qd.Value;
                }

                if (!string.IsNullOrEmpty(description)) {
                    // MOF doesn't enforce unique strings, but our model does
                    // append spaces to make a unique key
                    var key = !string.IsNullOrEmpty(displayName) ? displayName : (string)categoryVersionClass["__CLASS"];
                    while (stringTable.ContainsKey(key))
                        key += " ";
                    stringTable.Add(key, description);
                }

                // The task half of an event name. DisplayName is the authoritative one
                // and is what TDH reports - it is "OLEDB" for Bid2Etw_OLEDB_1_Trace,
                // which stripping the class name could never produce. One category
                // spells it DisplayNames instead, and 3 of the 191 declare neither, so
                // the stripped class name is just a backstop.
                var taskName = !string.IsNullOrEmpty(displayName) ? displayName
                    : !string.IsNullOrEmpty(displayNames) ? displayNames
                    : StripVersionSuffix((string)categoryVersionClass["__CLASS"]);

                // Names the class GUID rather than the category: every version of a
                // category shares one GUID, so the version is stripped. Not the same as
                // taskName - DisplayName is a display string ("OLEDB") where this
                // identifies the class ("Bid2Etw_OLEDB_1_Trace").
                var classGuidName = StripVersionSuffix((string)categoryVersionClass["__CLASS"]);

                foreach (var templateClass in ChildrenOf(subtree, (string)categoryVersionClass["__CLASS"])) {
                    // EventTypeName qualifier ~ OpCode. It is an array when the class
                    // covers several opcodes, e.g. Process_V2_TypeGroup1 declares
                    // EventType{1, 2, 3, 4} with EventTypeName{"Start", "End",
                    // "DCStart", "DCEnd"}, so it is read positionally below.
                    var template = (string)templateClass["__CLASS"];
                    string[] eventTypeNames = null;
                    var version = categoryVersion;
                    description = string.Empty;
                    foreach (QualifierData qd in templateClass.Qualifiers) {
                        if (qd.Value.GetType() == typeof(Int32) && qd.Name.ToLower() == "eventversion")
                            version = (Int32)qd.Value; // override category version with specific event version
                        else if (qd.Name.ToLower() == "eventtypename")
                            eventTypeNames = ToStringArray(qd.Value);
                        else if (qd.Value.GetType() == typeof(String) && qd.Name.ToLower() == "description")
                            description = (string)qd.Value;
                    }

                    if (!string.IsNullOrEmpty(description)) {
                        // append spaces to make a unique key
                        var key = template;
                        while (stringTable.ContainsKey(key))
                            key += " ";
                        stringTable.Add(key, description);
                    }

                    // EventType -> id(s), in declaration order. This list is NOT sorted:
                    // EventTypeName pairs with it by position, so reordering here would
                    // silently attach the wrong name to an opcode. The events dictionary
                    // sorts for display anyway.
                    var ids = new List<Int32>();
                    foreach (QualifierData qd in templateClass.Qualifiers) {
                        if (qd.Name.ToLower() == "eventtype") {
                            if (qd.Value.GetType() == typeof(Int32))
                                ids.Add((Int32)qd.Value);
                            else if (qd.Value.GetType().IsArray) {
                                foreach (var element in (Array)qd.Value) {
                                    if (element.GetType() == typeof(Int32))
                                        ids.Add((Int32)element);
                                }
                            }
                            break;
                        }
                    }

                    // sort by category, id, version
                    var addedIds = new HashSet<Int32>();
                    for (var i = 0; i < ids.Count; i++)
                    {
                        var id = ids[i];
                        // A class that repeats an EventType would otherwise throw out of
                        // events.Add and lose the whole provider. Skipping the repeat is
                        // what the SortedSet used to do; i still tracks the declaration
                        // position, so the EventTypeName pairing is unaffected.
                        if (!addedIds.Add(id))
                            continue;
                        var opcodeName = OpcodeName(eventTypeNames, i, id, template);
                        // A WMI class name (Process_V2_TypeGroup1) names a group of
                        // opcodes; task plus opcode names one event - Process_Start,
                        // FileIo_MapFile, OLEDB_TextW.
                        var eventName = $"{taskName}_{opcodeName}";
                        events.Add($"{category}{id,6}{version,6}",
                            new EtwEvent
                            {
                                Value = id,
                                // Split as manifests do: Task is the version-free name,
                                // Symbol carries the version, so sorting by Name groups
                                // an event's versions together.
                                Symbol = VersionedName(eventName, version),
                                Task = eventName,
                                Opcode = opcodeName,
                                Version = version,
                                Template = template,
                                Keyword = classGuidName
                            });
                    }

                    // create a template from the properties
                    var templateData = new SortedDictionary<int, EtwTemplateData>();
                    foreach (PropertyData pd in templateClass.Properties) {
                        foreach (QualifierData qd in pd.Qualifiers) {
                            if (qd.Value.GetType() == typeof(Int32) && qd.Name.ToLower() == "wmidataid") {
                                var id = (int)qd.Value;
                                templateData[id] = new EtwTemplateData
                                {
                                    Name = pd.Name,
                                    Type = pd.Type.ToString()
                                };
                                break;
                            }
                        }
                    }

                    templates.Add(new EtwTemplate(template, templateData.Values.ToArray()));
                }
            }

            manifest.Events = events.Values.ToArray();
            manifest.Templates = templates.ToArray();
            manifest.StringTable = stringTable;

            return manifest;
        }

        /// <summary>
        /// Every descendant of <paramref name="rootClass"/>, indexed by superclass.
        /// </summary>
        /// <remarks>
        /// One WMI round trip for the whole subtree; round trips are what this parser
        /// costs, so resolve the hierarchy from the map rather than querying per class.
        ///
        /// ISA is a hierarchy operator, not a prefix match: `ISA 'HeapTrace'` will not
        /// pick up the similarly named HeapTraceProvider that sits above it. It does
        /// return the root class itself, left here under its own superclass where
        /// nothing looks it up.
        /// </remarks>
        static Dictionary<string, List<ManagementClass>> GetSubtreeBySuperclass(string rootClass) {
            var bySuperclass = new Dictionary<string, List<ManagementClass>>(StringComparer.OrdinalIgnoreCase);
            var searcher = new ManagementObjectSearcher("root\\WMI",
                $"SELECT * FROM meta_class WHERE __this ISA '{rootClass}'", AmendedQualifiers);

            foreach (ManagementClass wmiClass in searcher.Get()) {
                var superclass = wmiClass["__SUPERCLASS"] as string;
                if (string.IsNullOrEmpty(superclass))
                    continue;
                if (!bySuperclass.TryGetValue(superclass, out var children))
                    bySuperclass[superclass] = children = new List<ManagementClass>();
                children.Add(wmiClass);
            }

            return bySuperclass;
        }

        /// <summary>The direct subclasses of <paramref name="className"/>, or none.</summary>
        static IEnumerable<ManagementClass> ChildrenOf(Dictionary<string, List<ManagementClass>> subtree, string className) {
            return subtree.TryGetValue(className, out var children) ? children : Enumerable.Empty<ManagementClass>();
        }

        static string ToMofString(ManagementClass wmiClass, Dictionary<string, List<ManagementClass>> subtree)
        {
            var mofString = string.Empty;

            // print the (qualified) class name, and then all of the (qualified) properties
            mofString += ToMofString(wmiClass.Qualifiers, string.Empty, "\n");
            mofString += $"class {wmiClass["__CLASS"]} : {wmiClass["__SUPERCLASS"]}\n";
            mofString += "{\n";

            var orderedProperties = new SortedDictionary<int, string>();
            foreach (PropertyData pd in wmiClass.Properties) {
                if (!pd.IsLocal)
                    continue;
                // sort properties according to the WmiDataId qualifer (if available)
                var propertyString = $"\t{ToMofString(pd.Qualifiers, "\t", " ")}{pd.Type.ToString().ToLower()} {pd.Name};\n";
                var propertyMatch = new Regex(@"wmidataid\((?<WmiDataId>\d+)\)", RegexOptions.IgnoreCase).Match(propertyString);
                if (propertyMatch.Success)
                    orderedProperties[Convert.ToInt32(propertyMatch.Groups["WmiDataId"].Value)] = propertyString;
                else
                    mofString += propertyString;
            }

            foreach (var propertyString in orderedProperties.Values) {
                mofString += propertyString;
            }

            mofString += "};\n\n";

            // event details are in descendant classes of the top-level (EventTrace) provider class
            // so also enumerate all of the children on this class
            foreach (var childClass in ChildrenOf(subtree, (string)wmiClass["__CLASS"])) {
                mofString += ToMofString(childClass, subtree);
            }

            return mofString;
        }

        // based on the mof representation of wbemtest
        internal static string[] qualifierOrder = { "wmidataid(", "dynamic", "description(", "guid(", "displayname(", "displaynames(", "eventtype(", "eventtype{", "eventtypename(", "eventtypename{", "eventversion(", "valuedescriptions{", "definevalues{", "values{", "valuemap{", "valuetype(", "extension(", "activityid", "relatedactivityid", "stringtermination(", "format(", "wmisizeis(", "xmlfragment", "pointer", "read", "max", "locale(" };

        static string ToMofString(QualifierDataCollection qualifiers, string prefix, string suffix)
        {
            var qualifierString = string.Empty;

            var qualifierList = new List<string>();
            var maxQualifierLength = 0;

            foreach (QualifierData qualifier in qualifiers) {
                if (qualifier.Name == "CIMTYPE")
                    continue;

                var strQualifier = qualifier.Name;
                if (qualifier.Value.GetType() == typeof(String))
                    strQualifier += $"(\"{qualifier.Value}\")";
                else if (qualifier.Value.GetType() == typeof(Int32))
                    strQualifier += $"({qualifier.Value})";
                else if (qualifier.Value.GetType() == typeof(Boolean))
                { } // add nothing
                else if (qualifier.Value.GetType().IsArray) {
                    var qualifierValueList = new List<string>();
                    foreach (var element in (Array)qualifier.Value)
                        qualifierValueList.Add(element.GetType() == typeof(String) ? $"\"{element}\"" : $"{element}");
                    strQualifier += $"{{{string.Join(", ", qualifierValueList)}}}";
                }
                else
                    throw new ApplicationException($"Unsupported Qualifier Type - {qualifier.Value.GetType().Name}");

                if (qualifier.PropagatesToInstance)
                    strQualifier += ": ToInstance";

                maxQualifierLength = strQualifier.Length > maxQualifierLength ? strQualifier.Length : maxQualifierLength;
                qualifierList.Add(strQualifier);
            }

            var orderedQualifierList = new List<string>();
            foreach (var nextQualifierInOrder in qualifierOrder) {
                var foundQualifier = string.Empty;
                foreach (var qualifier in qualifierList) {
                    if (qualifier.ToLower().StartsWith(nextQualifierInOrder)) {
                        orderedQualifierList.Add(qualifier);
                        continue;
                    }
                }
            }

            if (qualifierList.Count != orderedQualifierList.Count)
                throw new ApplicationException($"Unknown Qualifier Type(s) - {String.Join(",", qualifierList.ToArray())}");

            if (orderedQualifierList.Count > 0) {
                var wrapQualifiers = maxQualifierLength > 80;
                var header = "[";
                var delimiter = wrapQualifiers ? $",\n{prefix} " : ", ";
                var footer = wrapQualifiers ? $"\n{prefix}]{suffix}" : $"]{suffix}";

                qualifierString = $"{header}{string.Join(delimiter, orderedQualifierList)}{footer}";
            }

            return qualifierString;
        }
    }
}
