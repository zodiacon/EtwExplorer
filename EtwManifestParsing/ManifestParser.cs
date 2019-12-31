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
            var providerSearcher = new ManagementObjectSearcher("root\\WMI", $"SELECT * FROM meta_class WHERE __superclass = 'EventTrace'", null);
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

            var mof = ToMofString(providerClass);

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
            var templateNames = new SortedSet<string>();
            var taskSearcher = new ManagementObjectSearcher("root\\WMI", $"SELECT * FROM meta_class WHERE __superclass = '{providerClass["__CLASS"]}'", null);
            foreach (ManagementClass categoryVersionClass in taskSearcher.Get())
            {
                var categoryVersion = 0;
                var category = string.Empty;
                var description = string.Empty;
                var displayName = string.Empty;
                foreach (QualifierData qd in categoryVersionClass.Qualifiers) {
                    if (qd.Value.GetType() == typeof(Int32) && qd.Name.ToLower() == "eventversion")
                        categoryVersion = (Int32)qd.Value;
                    else if (qd.Value.GetType() == typeof(String) && qd.Name.ToLower() == "guid")
                        category = (string)qd.Value;
                    else if (qd.Value.GetType() == typeof(String) && qd.Name.ToLower() == "description")
                        description = (string)qd.Value;
                    else if (qd.Value.GetType() == typeof(String) && qd.Name.ToLower() == "displayname")
                        displayName = (string)qd.Value;
                }

                if (!string.IsNullOrEmpty(description))
                    stringTable.Add(!string.IsNullOrEmpty(displayName) ? displayName : (string)categoryVersionClass["__CLASS"], description);

                var templateSearcher = new ManagementObjectSearcher("root\\WMI", $"SELECT * FROM meta_class WHERE __superclass = '{categoryVersionClass["__CLASS"]}'", null);
                foreach (ManagementClass templateClass in templateSearcher.Get()) {
                    // EventTypeName qualifier ~ OpCode
                    var template = (string)templateClass["__CLASS"];
                    var eventType = string.Empty;
                    var version = categoryVersion;
                    description = string.Empty;
                    foreach (QualifierData qd in templateClass.Qualifiers) {
                        if (qd.Value.GetType() == typeof(Int32) && qd.Name.ToLower() == "eventversion")
                            version = (Int32)qd.Value; // override category version with specific event version
                        else if (qd.Value.GetType() == typeof(String) && qd.Name.ToLower() == "eventtypename")
                            eventType = (string)qd.Value;
                        else if (qd.Value.GetType() == typeof(String) && qd.Name.ToLower() == "description")
                            description = (string)qd.Value;
                    }
                    if (!string.IsNullOrEmpty(description))
                        stringTable.Add(template, description);

                    // EventType -> id(s)
                    var ids = new SortedSet<Int32>();
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
                    foreach (var id in ids)
                    {
                        events.Add($"{category}{id,6}{version,6}",
                            new EtwEvent
                            {
                                Value = id,
                                Symbol = template,
                                Opcode = eventType,
                                Version = version,
                                Template = template,
                                Keyword = string.Empty,
                                Task = category
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

        static string ToMofString(ManagementClass wmiClass)
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
            var providerSearcher = new ManagementObjectSearcher("root\\WMI", $"SELECT * FROM meta_class WHERE __superclass = '{wmiClass["__CLASS"]}'", null);
            foreach (ManagementClass providerClass in providerSearcher.Get()) {
                mofString += ToMofString(providerClass);
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
