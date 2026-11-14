# 01-toolchain-prerequisites: Verify .NET 11 SDK, WPF workload, and repo hygiene

Confirm the .NET 11 SDK (11.0.100) and `Microsoft.WindowsDesktop.App` 11.0 runtime are usable from the Visual Studio MSBuild that builds this solution, that no `global.json` pins an older SDK, and that the Visual Studio instance has the .NET desktop development components needed to build and design WPF on modern .NET. The user has authorized installing missing Visual Studio components if required.

Also tidy repository state that would otherwise pollute the upgrade: the stray `MigrationBackup/` folder left by an earlier NuGet migration, and `.gitignore` coverage for `packages/`, `bin/`, `obj/`, `.vs/`.

**Done when**: `dotnet --list-sdks` shows 11.0.100, the WindowsDesktop 11.0 runtime is present, Visual Studio MSBuild can resolve the `Microsoft.NET.Sdk` for net11.0, and the repo has no untracked migration leftovers.

## Research Findings

### Toolchain (verified)
- `dotnet --list-sdks`: 8.0.428, 11.0.100-rc.3, **11.0.100** — no `global.json` anywhere in the repo, so 11.0.100 is selected.
- Runtimes: `Microsoft.NETCore.App 11.0.0`, `Microsoft.WindowsDesktop.App 11.0.0` (plus 6.0/8.0/10.0 for older apps).
- Reference packs: `C:\Program Files\dotnet\packs\Microsoft.WindowsDesktop.App.Ref\11.0.0` present.
- VS 2026 Community (18): MSBuild at `MSBuild\Current\Bin\MSBuild.exe` (via vswhere). Installed components include `Microsoft.VisualStudio.Workload.ManagedDesktop`, `Microsoft.NetCore.Component.SDK`, `Microsoft.NetCore.Component.Runtime.11.0`, `Microsoft.NetCore.Toolset.11.0.100-rtm`, `Microsoft.WindowsDesktop.SharedFramework.11.0.11.0.0`. **No VS components need installing.**
- Proof: a scratch `dotnet new wpf -f net11.0` project built successfully with VS MSBuild (`/restore /t:Build`) → `bin\Debug\net11.0-windows\Probe.dll`. Scratch folder deleted.
- `VSINSTALLDIR` is not set in the tool shell; use the vswhere-resolved path.

### Repo hygiene
- `MigrationBackup/ae6cc1ee/EtwExplorer/{EtwExplorer.csproj, NuGetUpgradeLog.html, packages.config}` — 3 files **tracked in git** (leftover of an earlier packages.config→PackageReference attempt that was rolled back). Remove with `git rm -r`.
- `.gitignore` already covers `[Bb]in/`, `[Oo]bj/`, `.vs/`, `**/packages/*` (standard VS template). No tracked bin/obj/.suo/.user files found. No change required.
- Working tree clean apart from the untracked `.github/` scenario folder (will be committed with this task).

### Build tool decision
- Legacy csproj (pre-conversion) → `msbuild.exe` only. After SDK conversion the WPF app still needs VS MSBuild (XAML/WinFX targets); the class library can use `dotnet build`. Recorded in scenario-instructions.md.
