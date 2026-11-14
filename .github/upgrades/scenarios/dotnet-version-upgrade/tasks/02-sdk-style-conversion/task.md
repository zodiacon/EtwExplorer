# 02-sdk-style-conversion: Convert both projects to SDK-style on net48

Convert `EtwManifestParsing.csproj` and `EtwExplorer.csproj` from legacy `ToolsVersion=15.0` format to SDK-style (`Microsoft.NET.Sdk` / `Microsoft.NET.Sdk` with `UseWPF`), migrating `packages.config` to `PackageReference`, while keeping the target framework at `net48`. This isolates the structural change from the API-surface change.

During conversion, collapse the 28 direct package references down to the real top-level dependencies — `Microsoft.Diagnostics.Tracing.TraceEvent` (library + app), `MahApps.Metro`, `Prism.Core`, `Zodiacon.WPF` (app) — letting NuGet resolve the rest transitively. Remove the `EnsureNuGetPackageBuildImports` targets, `System.ValueTuple` imports, and `AssemblyInfo.cs` attributes that SDK projects auto-generate (keep `SharedAssemblyInfo.cs` linkage or move its values into MSBuild properties). Remove the auto-generated binding redirects from `App.config` (they conflict with the auto-generated versions per assessment and are not used on .NET Core). Preserve `app.ico`, `Icons/*.ico` resources, `Settings.settings`/`Resources.resx` generators, and XAML `Page`/`ApplicationDefinition` items (SDK globbing handles these with `UseWPF`).

**Done when**: Both csproj files are SDK-style, `packages.config` files are deleted, both still target `net48`, the solution builds with zero errors and zero warnings, and the app launches and opens a manifest.

## Research Findings

### Scope inventory
- Projects (topological order from tool): `EtwManifestParsing\EtwManifestParsing.csproj` → `EtwExplorer\EtwExplorer.csproj`.
- Concerns: (1) project format conversion via `convert_project_to_sdk_style` (sequential), (2) package pruning to top-level deps (versions unchanged), (3) assembly-info dedupe, (4) App.config redirect removal, (5) build on net48 with VS MSBuild.
- Assessment (per project): Project.0001 (SDK-style), NuGet.0003 ×8 (polyfills included in framework), NuGet.0002 ×9 (stale "downgrade to 10.0.15" advice — ignore), Binding.0006/0007 ×5 each (App.config redirects conflict with auto-generated), EtwExplorer also NuGet.0001 ×2 (Zodiacon.WPF, Microsoft.Xaml.Behaviors.Wpf — handled in task 04, left as-is here on net48).

### Current project state (confirmed)
- `EtwManifestParsing.csproj`: ToolsVersion 15.0, `TargetFrameworkVersion v4.8`, Library, 21 HintPath package refs + framework refs (`System.Management`, `System.Xml.Linq`, …), `Compile` list incl. `Properties\AssemblyInfo.cs` + linked `..\SharedAssemblyInfo.cs`, `EnsureNuGetPackageBuildImports` target, `System.ValueTuple.targets` import, TraceEvent `.props` import, `app.config`, `packages.config`.
- `EtwExplorer.csproj`: same shape for WPF (ProjectTypeGuids WPF, `OutputType WinExe`, `app.ico`, `Icons\*.ico` Resource items, `Settings.settings`/`Resources.resx` designers, XAML Page/ApplicationDefinition items), 28 package refs incl. MahApps.Metro 2.4.11, ControlzEx 5.0.2, Prism.Core/Events 9.0.537, Prism.Container.Abstractions 9.0.107, Microsoft.Xaml.Behaviors.Wpf 1.1.161, Zodiacon.WPF 1.3.0, TraceEvent 4.0.0.
- `SharedAssemblyInfo.cs` (repo root): `AssemblyVersion("0.40.0.0")`, `AssemblyCopyright("© 2019 Pavel Yosifovch")` — linked into both projects.
- `Properties\AssemblyInfo.cs` in each project: Title/Description/Configuration/Company/Product/Trademark/Culture/ComVisible/Guid (+ `ThemeInfo` in EtwExplorer). SDK `GenerateAssemblyInfo` would duplicate Title/Product/Company/Configuration → CS0579. Plan: delete `Properties\AssemblyInfo.cs` in both; move Title/Product/ComVisible(Guid) into csproj properties (`<AssemblyTitle>`, `<Product>`); keep `SharedAssemblyInfo.cs` as linked compile but set `<GenerateAssemblyInfo>` attributes it conflicts with off: `GenerateAssemblyVersionAttribute=false`, `GenerateAssemblyCopyrightAttribute=false`? Simpler: move `AssemblyVersion`/`Copyright` into a repo-root `Directory.Build.props` (`<Version>0.40.0</Version>`, `<Copyright>`) and delete `SharedAssemblyInfo.cs`. **Decision**: Directory.Build.props — it is the SDK-idiomatic equivalent of the shared file. EtwExplorer keeps a minimal `AssemblyInfo.cs` with only `[assembly: ThemeInfo(...)]` (required by WPF).
- `packages\` folder absent locally — legacy build never restored here; the SDK build will restore from nuget.org.

### Package pruning plan (versions unchanged, nothing new introduced)
- EtwManifestParsing keep: `Microsoft.Diagnostics.Tracing.TraceEvent 4.0.0` only (brings Microsoft.Diagnostics.NETCore.Client, System.Text.Json, etc. transitively). `System.Management` stays a framework `<Reference>` on net48.
- EtwExplorer keep: `Microsoft.Diagnostics.Tracing.TraceEvent 4.0.0`, `MahApps.Metro 2.4.11` (→ ControlzEx, Microsoft.Xaml.Behaviors.Wpf transitively), `Prism.Core 9.0.537` (→ Prism.Events, Prism.Container.Abstractions), `Zodiacon.WPF 1.3.0` (net462 — stays for now; removed in task 04).
- Everything else (System.Buffers/Memory/ValueTuple/Numerics.Vectors/Threading.Tasks.Extensions/Reflection.*/Runtime.CompilerServices.Unsafe, Microsoft.Win32.Registry, Security.*, Microsoft.Bcl.AsyncInterfaces, Microsoft.Extensions.*.Abstractions, System.Collections.Immutable, System.Diagnostics.DiagnosticSource, System.IO.Pipelines, System.Text.*) is a transitive dependency of TraceEvent/MahApps and resolves automatically. If the direct pins were at higher versions than the transitive ones, NuGet picks the transitive minimum — acceptable on net48 (auto binding redirects) and irrelevant on net11 (in-box).
- If pruning causes a build/restore failure, fall back to keeping the explicit refs at their current versions (skill constraint: no version drift).

### Config
- `EtwExplorer\App.config`: only `<startup supportedRuntime v4.8>` + assemblyBinding redirects → delete the redirects (SDK on net48 generates them: `AutoGenerateBindingRedirects` default true for exe); keep startup element until task 04 when the file is deleted.
- `EtwManifestParsing\app.config`: check content; likely only redirects → delete file.

### Build tool
- VS MSBuild (`C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`) with `/restore` — WPF + net48.
