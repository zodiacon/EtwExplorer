# 06-final-validation: Full solution validation and deferred recommendations

Perform a clean restore + Release and Debug builds of the whole solution from the command line and from Visual Studio, run the app through its core flows (open XML manifest, open registered provider including the WMI fallback path, keyword-only provider, save XML), and confirm no .NET Framework artefacts remain (`packages/`, `packages.config`, `app.config` redirects, `ToolsVersion`, `TargetFrameworkVersion`). Update `README.md` to state the .NET 11 requirement and build instructions.

Document deferred recommendations: Central Package Management can now be added cleanly since all projects are SDK-style on a single TFM (no VersionOverride friction); consider `PublishSingleFile`/`SelfContained` publishing profiles; consider WinForms/WPF feature adoption scenarios post-upgrade.

**Done when**: Clean `dotnet build -c Release` and Debug both succeed with zero warnings, the manual smoke test passes, README reflects .NET 11, and the deferred recommendations are recorded.
