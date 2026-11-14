# Progress Details — 06-final-validation

## Summary

Final validation of the upgraded solution: clean Debug and Release builds from the CLI and from
Visual Studio are warning-free, all core application flows pass, no .NET Framework artefacts remain,
and the README now documents the .NET 11 requirement and build steps.

## What changed
- `README.md`: added **Requirements** (.NET 11 SDK / Desktop Runtime, VS 2026 optional) and **Building**
  sections with the project table and a pointer to `Directory.Build.props` / `.editorconfig`.
- Deleted the local, git-ignored `packages/` directory left over from the packages.config-era restore
  (28 package folders incl. `Zodiacon.WPF.1.3.0`); nothing referenced it.
- Scenario artefacts only otherwise (`task.md` research, this file).

## Legacy-artefact audit (clean)
Grep across `*.csproj`, `*.props`, `*.targets`, `*.sln`: no `ToolsVersion`, `TargetFrameworkVersion`,
`bindingRedirect`, `packages.config`, or Zodiacon references. No `App.config`/`app.config`, no `global.json`.
Both projects are SDK-style, `net11.0-windows`.

## Build
| Configuration | Tool | Result |
|---|---|---|
| Debug | `dotnet build EtwExplorer.sln -c Debug` (clean bin/obj) | 0 warnings, 0 errors |
| Release | `dotnet build EtwExplorer.sln -c Release` (clean bin/obj) | 0 warnings, 0 errors |
| Solution | Visual Studio `run_build` | Build successful |

Outputs present under `EtwExplorer\bin\{Debug,Release}\net11.0-windows\` (`EtwExplorer.exe`, `EtwExplorer.dll`,
`EtwManifestParsing.dll`) and `EtwManifestParsing\bin\{Debug,Release}\net11.0-windows\`.

## Smoke tests (Release build)
- **UI Automation** (`session-state\files\uia-smoke.ps1`): launch → "Open Provider..." → filter `Kernel-Process`
  (2 rows) → select → OK → 5 tabs, status bar shows `Microsoft-Windows-Kernel-Process`; window title
  `ETW Explorer v0.40 © 2019 Pavel Yosifovch`; process stays alive.
- **Registered provider (XML path)**: Kernel-Process → 45 events / 11 keywords / 22 tasks.
- **Save XML → Open XML round trip**: `Manifest.Xml` written to a temp file (34,464 bytes), re-parsed via
  `ManifestParser.Parse(File.ReadAllText(...))` → identical counts and provider GUID.
- **WMI fallback (MOF path)**: `ParseWmiEventTraceClass(MSNT_SystemTrace)` → "Windows Kernel Trace", 515 events.
- **Keyword-only path**: `TraceEventProviders.GetProviderKeywords` → 12 keywords (feeds `KeywordsTabViewModel`).
- **Error path**: malformed XML → `ApplicationException("Failed to parse manifest XML")` (shown by the app's message box).

## Deferred recommendations
1. **Central Package Management** — both projects are SDK-style on one TFM and share `Microsoft.Diagnostics.Tracing.TraceEvent 4.0.0`;
   a `Directory.Packages.props` with `ManagePackageVersionsCentrally=true` would remove the duplicated version pin with no
   `VersionOverride` friction.
2. **Publishing profiles** — consider `dotnet publish -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true` (or
   framework-dependent single-file) so end users don't need the Desktop Runtime installed.
3. **WPF feature adoption** — with .NET 11 available, consider the Fluent theme (`ThemeMode`) and modern WPF features as a
   follow-up scenario; MahApps.Metro 2.4.11 remains supported on .NET 8+.
4. **Tests** — the solution has no test project; `EtwManifestParsing` is now headless-testable (the validation above can be
   turned into xUnit/MSTest tests, with the Kernel-Process manifest captured as a fixture).
5. **File-scoped namespaces** — deliberately not applied to limit churn; `dotnet format --diagnostics IDE0161` will do it in one pass
   now that brace style is pinned in `.editorconfig`.
