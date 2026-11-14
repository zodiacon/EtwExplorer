# .NET 11 Upgrade Plan — EtwExplorer

## Overview

**Target**: Upgrade EtwExplorer.sln from .NET Framework 4.8 to .NET 11 (`net11.0-windows`), converting both legacy-format projects to SDK-style with PackageReference, replacing the one incompatible package, and modernizing the codebase (nullable reference types, modern C#).
**Scope**: Small solution — 2 projects (WPF app + class library), ~1.6k LOC, 28 packages (most are redundant BCL polyfills), zero API incompatibilities detected.

### Selected Strategy
**Bottom-Up (Dependency-First)** — Upgrade from leaf nodes to root applications, tier by tier.
**Rationale**: 2 .NET Framework projects with a 2-tier dependency graph; Framework→Core boundary requires per-tier validation.

```
Tier 2: [EtwExplorer (WPF app)]
			 ↓
Tier 1: [EtwManifestParsing (class library)]
```

- **Tier 1 — EtwManifestParsing**: no internal project references; depends on TraceEvent + System.Management (WMI). Complete when it builds warning-free on `net11.0-windows`.
- **Tier 2 — EtwExplorer**: depends on EtwManifestParsing; WPF + MahApps.Metro + Prism + Zodiacon.WPF (incompatible). Complete when the app builds warning-free on `net11.0-windows` and launches.

## Upgrade Options

| Option | Selected | Why |
|--------|----------|-----|
| Upgrade Strategy | Bottom-Up | Two .NET Framework projects with a 2-tier dependency graph require tier-by-tier migration across the Framework→Core boundary. |
| Project Approach | Class Libraries: In-place | EtwManifestParsing's only consumer migrates in the same upgrade, so no Framework dependents remain to justify multi-targeting. |
| Package Management | Per-Project (defer CPM to post-migration) | Both projects use `packages.config` in legacy csproj; CPM would add a second migration on top of the PackageReference conversion. |
| Unsupported Packages | Resolve Inline | Only Zodiacon.WPF (net462-only, no newer release) lacks a compatible version; its small used surface can be replaced in the same task. |
| Assembly Binding Redirects | Remove Binding Redirects | All 20 redirect issues are VS auto-generated boilerplate in App.config that SDK-style/.NET Core projects don't need. |
| Nullable Reference Types | Enable Nullable Reference Types | ~1.6k LOC across 2 projects with zero API incompatibilities is small; user asked to modernize. Done as an isolated final task. |

## Tasks

### 01-toolchain-prerequisites: Verify .NET 11 SDK, WPF workload, and repo hygiene

Confirm the .NET 11 SDK (11.0.100) and `Microsoft.WindowsDesktop.App` 11.0 runtime are usable from the Visual Studio MSBuild that builds this solution, that no `global.json` pins an older SDK, and that the Visual Studio instance has the .NET desktop development components needed to build and design WPF on modern .NET. The user has authorized installing missing Visual Studio components if required.

Also tidy repository state that would otherwise pollute the upgrade: the stray `MigrationBackup/` folder left by an earlier NuGet migration, and `.gitignore` coverage for `packages/`, `bin/`, `obj/`, `.vs/`.

**Done when**: `dotnet --list-sdks` shows 11.0.100, the WindowsDesktop 11.0 runtime is present, Visual Studio MSBuild can resolve the `Microsoft.NET.Sdk` for net11.0, and the repo has no untracked migration leftovers.

---

### 02-sdk-style-conversion: Convert both projects to SDK-style on net48

Convert `EtwManifestParsing.csproj` and `EtwExplorer.csproj` from legacy `ToolsVersion=15.0` format to SDK-style (`Microsoft.NET.Sdk` / `Microsoft.NET.Sdk` with `UseWPF`), migrating `packages.config` to `PackageReference`, while keeping the target framework at `net48`. This isolates the structural change from the API-surface change.

During conversion, collapse the 28 direct package references down to the real top-level dependencies — `Microsoft.Diagnostics.Tracing.TraceEvent` (library + app), `MahApps.Metro`, `Prism.Core`, `Zodiacon.WPF` (app) — letting NuGet resolve the rest transitively. Remove the `EnsureNuGetPackageBuildImports` targets, `System.ValueTuple` imports, and `AssemblyInfo.cs` attributes that SDK projects auto-generate (keep `SharedAssemblyInfo.cs` linkage or move its values into MSBuild properties). Remove the auto-generated binding redirects from `App.config` (they conflict with the auto-generated versions per assessment and are not used on .NET Core). Preserve `app.ico`, `Icons/*.ico` resources, `Settings.settings`/`Resources.resx` generators, and XAML `Page`/`ApplicationDefinition` items (SDK globbing handles these with `UseWPF`).

**Done when**: Both csproj files are SDK-style, `packages.config` files are deleted, both still target `net48`, the solution builds with zero errors and zero warnings, and the app launches and opens a manifest.

---

### 03-manifest-parsing-library: Upgrade EtwManifestParsing to net11.0-windows

Retarget the Tier 1 class library from `net48` to `net11.0-windows`. It uses `System.Management` (WMI `ManagementObjectSearcher` against `root\WMI` for EventTrace classes), which on modern .NET comes from the `System.Management` NuGet package (11.0.0), and `Microsoft.Diagnostics.Tracing.TraceEvent` 4.0.0 (netstandard2.0, compatible). Windows-only APIs justify the `-windows` TFM and avoid CA1416 platform-compatibility warnings.

Research the `ManifestParser.cs` WMI usage and `EtwManifest` XML handling for any `System.Xml`/`System.Linq` behaviour differences, drop the `app.config` (no runtime content needed), and fix any warnings surfaced by the newer compiler/analyzers. Validate that the consuming EtwExplorer project (still on net48 from task 02) is now expected to fail to reference it — that's the known between-tier state for in-place libraries; proceed to task 04 immediately.

**Done when**: EtwManifestParsing builds with zero errors and zero warnings on `net11.0-windows`, `app.config` and `packages.config` are gone, and `Properties/AssemblyInfo.cs` duplicates are resolved.

---

### 04-wpf-app-and-zodiacon-replacement: Upgrade EtwExplorer to net11.0-windows and replace Zodiacon.WPF

Retarget the WPF application to `net11.0-windows` with `UseWPF`, and resolve the one incompatible package inline: Zodiacon.WPF 1.3.0 is net462-only with no modern release. Its used surface is small — `IUIServices`/`UIServicesDefaults` (`FileDialogService.GetFileForOpen/GetFileForSave`, `MessageBoxService.ShowMessage/SetOwner`, `DialogService.CreateDialog<TVM,TView>`), `DialogViewModelBase` (ctor taking `Window`, `ShowDialog()`, `OnOK()`), and four XAML helpers (`z:FocusProperties.FocusOnLoad`, `z:InputHelper.AttachInputBindings`, `z:Image` markup extension, `z:TabControlProperties.IsCached`). Implement equivalent types in a new `EtwExplorer/Infrastructure` (or similar) folder and repoint `using Zodiacon.WPF` and the `xmlns:z` namespace to them; write these fresh rather than copying the Apache-2.0 source.

Check MahApps.Metro 2.4.11 (supports net6.0-windows+) and Prism.Core 9.0.537 (netstandard2.0/net8.0) resolve cleanly; drop the explicit `Microsoft.Xaml.Behaviors.Wpf` reference (transitive via MahApps, not used in XAML). `Properties/Settings.settings` + `Settings.Designer.cs` rely on `System.Configuration.ApplicationSettingsBase`, which exists in `Microsoft.WindowsDesktop.App` — verify they still compile. Remove the `System.Web`/`System.Web.Extensions` framework references (unused on inspection). Validate the app runs: open an XML manifest, open a registered provider, and confirm tabs render with MahApps theming.

**Done when**: EtwExplorer builds on `net11.0-windows` with zero errors and zero warnings, has no reference to Zodiacon.WPF, the solution builds end-to-end, and the application launches and exercises the open-manifest / open-registered-provider flows.

---

### 05-nullable-and-csharp-modernization: Enable nullable reference types and modernize C# usage

With the solution green on .NET 11, enable `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>` in both projects and resolve every resulting warning properly (annotate nullable members, add guards, avoid `!` except where justified). Apply low-risk C# 15-era modernizations across the ~1.6k LOC: file-scoped namespaces, `using` cleanup, target-typed `new`, collection expressions, pattern matching, and `sealed` on view models where appropriate — without changing behaviour.

Research with the `modernizing-csharp-version` and `migrating-csharp-nullable-references` skills before editing. Keep MVVM semantics intact (Prism `BindableBase.SetProperty` patterns, `DelegateCommand.ObservesProperty`).

**Done when**: Both projects have nullable and implicit usings enabled, the solution builds with zero errors and zero warnings, and the app still launches and performs the open-manifest flow.

---

### 06-final-validation: Full solution validation and deferred recommendations

Perform a clean restore + Release and Debug builds of the whole solution from the command line and from Visual Studio, run the app through its core flows (open XML manifest, open registered provider including the WMI fallback path, keyword-only provider, save XML), and confirm no .NET Framework artefacts remain (`packages/`, `packages.config`, `app.config` redirects, `ToolsVersion`, `TargetFrameworkVersion`). Update `README.md` to state the .NET 11 requirement and build instructions.

Document deferred recommendations: Central Package Management can now be added cleanly since all projects are SDK-style on a single TFM (no VersionOverride friction); consider `PublishSingleFile`/`SelfContained` publishing profiles; consider WinForms/WPF feature adoption scenarios post-upgrade.

**Done when**: Clean `dotnet build -c Release` and Debug both succeed with zero warnings, the manual smoke test passes, README reflects .NET 11, and the deferred recommendations are recorded.
