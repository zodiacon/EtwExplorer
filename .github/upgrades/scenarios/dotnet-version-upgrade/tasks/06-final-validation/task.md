# 06-final-validation: Full solution validation and deferred recommendations

Perform a clean restore + Release and Debug builds of the whole solution from the command line and from Visual Studio, run the app through its core flows (open XML manifest, open registered provider including the WMI fallback path, keyword-only provider, save XML), and confirm no .NET Framework artefacts remain (`packages/`, `packages.config`, `app.config` redirects, `ToolsVersion`, `TargetFrameworkVersion`). Update `README.md` to state the .NET 11 requirement and build instructions.

Document deferred recommendations: Central Package Management can now be added cleanly since all projects are SDK-style on a single TFM (no VersionOverride friction); consider `PublishSingleFile`/`SelfContained` publishing profiles; consider WinForms/WPF feature adoption scenarios post-upgrade.

**Done when**: Clean `dotnet build -c Release` and Debug both succeed with zero warnings, the manual smoke test passes, README reflects .NET 11, and the deferred recommendations are recorded.

## Research

Skill loaded: `building-projects` (IDE build tool preferred → `run_build`; CLI fallback `dotnet build`; Step 4 validation: warnings, output path matches TFM).

### Current repo state (confirmed)
- Solution `EtwExplorer.sln` → 2 SDK-style projects, both `net11.0-windows`:
  - `EtwManifestParsing.csproj`: TraceEvent 4.0.0, System.Management 11.0.0.
  - `EtwExplorer.csproj`: WPF (`UseWPF`), MahApps.Metro 2.4.11, TraceEvent 4.0.0, Prism.Core 9.0.537, `ApplicationIcon=app.ico`, `Resource Include="Icons\*.ico"`.
- `Directory.Build.props`: Version/Copyright/Authors/Product; Nullable + ImplicitUsings enabled; `WarningsAsErrors` includes `nullable`.
- No `global.json`, no `packages.config`, no `App.config`/`app.config`, no `ToolsVersion`/`TargetFrameworkVersion`/`bindingRedirect`/Zodiacon references in any csproj/props/sln (grep clean).
- Leftover: untracked, git-ignored `packages/` directory (28 package folders incl. `Zodiacon.WPF.1.3.0`) from the packages.config-era restore. Not referenced by anything; safe to delete locally.
- README.md is 7 lines (description + screenshot). No build/requirements section.
- Commits on `upgrade-dotnet-10`: 7c77beb, 2bd2e7e, 0fb4787, 6f3313d, 43c0db5. `master` has not moved (rev-list count 0).

### Plan
1. Delete local `packages/` leftover.
2. Clean `bin`/`obj`; `dotnet build -c Debug` and `dotnet build -c Release` → expect 0 warnings/0 errors; verify outputs under `bin\{Debug,Release}\net11.0-windows\`.
3. IDE build via `run_build` (solution) to confirm VS agrees.
4. Smoke: UIA script `session-state\files\uia-smoke.ps1` against the **Release** exe (open registered provider flow); headless library checks for XML (Kernel-Process) and WMI (MSNT_SystemTrace) paths already pass in task 05 — re-run against Release build. Save-XML and open-XML flows: exercise `ManifestParser.Parse(File.ReadAllText(...))` round-trip headlessly (save `Manifest.Xml` to temp, re-parse, compare counts) since file dialogs can't be driven from this session.
5. README: add "Requirements" (Windows, .NET 11 SDK / Desktop Runtime, VS 2026 optional) and "Building" (`dotnet build`, `dotnet run --project EtwExplorer`) sections; keep existing content.
6. Record deferred recommendations in progress-details.md (CPM, PublishSingleFile/SelfContained, WPF feature adoption).

