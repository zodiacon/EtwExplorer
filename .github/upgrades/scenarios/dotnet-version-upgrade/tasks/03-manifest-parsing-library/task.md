# 03-manifest-parsing-library: Upgrade EtwManifestParsing to net11.0-windows

Retarget the Tier 1 class library from `net48` to `net11.0-windows`. It uses `System.Management` (WMI `ManagementObjectSearcher` against `root\WMI` for EventTrace classes), which on modern .NET comes from the `System.Management` NuGet package (11.0.0), and `Microsoft.Diagnostics.Tracing.TraceEvent` 4.0.0 (netstandard2.0, compatible). Windows-only APIs justify the `-windows` TFM and avoid CA1416 platform-compatibility warnings.

Research the `ManifestParser.cs` WMI usage and `EtwManifest` XML handling for any `System.Xml`/`System.Linq` behaviour differences, drop the `app.config` (no runtime content needed), and fix any warnings surfaced by the newer compiler/analyzers. Validate that the consuming EtwExplorer project (still on net48 from task 02) is now expected to fail to reference it — that's the known between-tier state for in-place libraries; proceed to task 04 immediately.

**Done when**: EtwManifestParsing builds with zero errors and zero warnings on `net11.0-windows`, `app.config` and `packages.config` are gone, and `Properties/AssemblyInfo.cs` duplicates are resolved.
