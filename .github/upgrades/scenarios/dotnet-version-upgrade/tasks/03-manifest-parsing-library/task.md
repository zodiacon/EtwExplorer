# 03-manifest-parsing-library: Upgrade EtwManifestParsing to net11.0-windows

Retarget the Tier 1 class library from `net48` to `net11.0-windows`. It uses `System.Management` (WMI `ManagementObjectSearcher` against `root\WMI` for EventTrace classes), which on modern .NET comes from the `System.Management` NuGet package (11.0.0), and `Microsoft.Diagnostics.Tracing.TraceEvent` 4.0.0 (netstandard2.0, compatible). Windows-only APIs justify the `-windows` TFM and avoid CA1416 platform-compatibility warnings.

Research the `ManifestParser.cs` WMI usage and `EtwManifest` XML handling for any `System.Xml`/`System.Linq` behaviour differences, drop the `app.config` (no runtime content needed), and fix any warnings surfaced by the newer compiler/analyzers. Validate that the consuming EtwExplorer project (still on net48 from task 02) is now expected to fail to reference it — that's the known between-tier state for in-place libraries; proceed to task 04 immediately.

**Done when**: EtwManifestParsing builds with zero errors and zero warnings on `net11.0-windows`, `app.config` and `packages.config` are gone, and `Properties/AssemblyInfo.cs` duplicates are resolved.

## Research Findings
- Already handled in task 02: `app.config`, `packages.config`, `Properties\AssemblyInfo.cs` are gone; shared metadata lives in `Directory.Build.props`. The csproj is SDK-style with a single `PackageReference` (TraceEvent 4.0.0) and a framework `<Reference Include="System.Management" />`.
- Assessment: 0 API issues for this project (only project-format/package/binding issues, all resolved or not applicable on .NET 11).
- Source (6 files, ~1.1k LOC): `ManifestParser.cs` uses `System.Management` (`ManagementObjectSearcher`, `ManagementClass`, `EnumerationOptions`, amended qualifiers) + `Microsoft.Diagnostics.Tracing.Session` (TraceEvent) + `System.Xml.Linq`, `Regex`. No Registry, P/Invoke, Encoding.CodePages, BinaryFormatter, AppDomain or other APIs with Core behavioural differences. `EtwManifest`/`EtwEvent`/`EtwKeyword`/`EtwTask`/`EtwTemplate` are plain models.
- `System.Management` on modern .NET: NuGet package, latest supported for net11.0-windows = **11.0.0** (confirmed via tool). Windows-only ⇒ `net11.0-windows` TFM so `CA1416` does not fire.
- TraceEvent 4.0.0 ships `netstandard2.0` + `lib/net462` assets; netstandard2.0 is selected on net11 — compatible (assessment ✅). It also pulls `Microsoft.Diagnostics.NETCore.Client`, `System.Reflection.Metadata` etc. — on .NET 11 these resolve to in-box or package versions as needed; `Dia2Lib`/`TraceReloggerLib` interop assemblies are shipped in the package.
- Expected between-tier state: EtwExplorer (net48) will fail to reference a net11.0-windows library — accepted; task 04 fixes immediately. For this task validate with `dotnet build EtwManifestParsing.csproj` only.

### Plan
1. `TargetFramework` → `net11.0-windows`.
2. Replace `<Reference Include="System.Management" />` with `<PackageReference Include="System.Management" Version="11.0.0" />`.
3. `dotnet build` the project; fix any new compiler/analyzer warnings.
