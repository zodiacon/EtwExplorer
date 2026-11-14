# 02-sdk-style-conversion — Progress Details

## Summary
Both projects converted from legacy `ToolsVersion=15.0` + `packages.config` format to SDK-style `PackageReference` projects, still targeting `net48`. Solution builds with **0 warnings / 0 errors**; app launches and the manifest-parsing path works.

## What changed
### Project files
- `EtwManifestParsing\EtwManifestParsing.csproj` — converted with `convert_project_to_sdk_style`, then pruned to the single top-level dependency `Microsoft.Diagnostics.Tracing.TraceEvent 4.0.0` (+ framework `System.Management` reference). 20 BCL polyfill / transitive packages removed from the direct list (they still resolve transitively through TraceEvent at the versions it requires).
- `EtwExplorer\EtwExplorer.csproj` — converted, then pruned to `MahApps.Metro 2.4.11`, `Microsoft.Diagnostics.Tracing.TraceEvent 4.0.0`, `Prism.Core 9.0.537`, `Zodiacon.WPF 1.3.0` (+ framework `System.Configuration` for `ApplicationSettingsBase`). `UseWPF`, `WinExe`, `ApplicationIcon`, `Icons\*.ico` + `app.ico` Resources preserved. Removed unused framework refs (`System.Web`, `System.Web.Extensions`, `System.ComponentModel.Composition`, `System.ComponentModel.DataAnnotations`, etc.) and the explicit `Microsoft.Xaml.Behaviors.Wpf`/`ControlzEx`/`Prism.Events`/`Prism.Container.Abstractions` refs (transitive).
- **New** `Directory.Build.props` (repo root) — `Version 0.40.0`, `Copyright`, `Authors`, `Product` shared by both projects; replaces the linked `SharedAssemblyInfo.cs`.

### Assembly info
- Deleted `SharedAssemblyInfo.cs` and `EtwManifestParsing\Properties\AssemblyInfo.cs` (SDK `GenerateAssemblyInfo` now produces Title/Product/Company/Version/Copyright from MSBuild properties).
- `EtwExplorer\Properties\AssemblyInfo.cs` reduced to the WPF-required `[assembly: ThemeInfo(...)]` only.
- Verified output metadata: AssemblyVersion `0.40.0.0`, FileVersion `0.40.0.0`, ProductName `ETW Explorer`, Copyright `© 2019 Pavel Yosifovch`, title `EtwExplorer`. Main window title renders `ETW Explorer v0.40 © 2019 Pavel Yosifovch` as before.

### Config / legacy files
- Deleted `EtwManifestParsing\packages.config`, `EtwExplorer\packages.config`, `EtwManifestParsing\app.config` (contained only redirects).
- `EtwExplorer\App.config` — removed all hand-written `assemblyBinding` redirects (assessment Binding.0006/0007); kept the `<startup>` element. SDK auto-generates 11 redirects into `EtwExplorer.exe.config` at build time.

## Build / validation
| Check | Result |
|-------|--------|
| `MSBuild EtwExplorer.sln /restore /t:Rebuild` (VS 2026 MSBuild) | ✅ Build succeeded, 0 Warning(s), 0 Error(s) |
| Both projects SDK-style, TFM unchanged (`net48`) | ✅ Output in `bin\Debug\net48\` |
| `packages.config` removed | ✅ none remain |
| App launches | ✅ `EtwExplorer.exe` started, main window `ETW Explorer v0.40 © 2019 Pavel Yosifovch` shown, no crash after 6 s |
| Manifest-open path | ✅ Headless: `RegisteredTraceEventParser.GetManifestForRegisteredProvider(Microsoft-Windows-Kernel-Process)` → `ManifestParser.Parse` → 45 events / 11 keywords / 22 tasks |
| Tests | n/a — solution has no test projects |

## Notes / deviations
- The conversion tool did not delete `packages.config` files from disk (they were merely dropped from the project); removed them manually.
- Prism `DelegateCommand`/`BindableBase` usages and MahApps XAML compiled unchanged — no code edits required.
- The SDK appends the git commit hash to `InformationalVersion` (`0.40.0+<sha>`) by default; harmless, left as-is.
- Behavioural note for the user: `PackageReference` dependencies are transitive, so EtwExplorer now sees TraceEvent through the library reference as well as its own direct reference — identical versions, no conflict.
