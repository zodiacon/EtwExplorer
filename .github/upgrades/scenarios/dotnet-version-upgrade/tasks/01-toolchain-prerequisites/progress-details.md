# 01-toolchain-prerequisites — Progress Details

## Summary
Toolchain verified ready for .NET 11 WPF development; no Visual Studio components needed installing. Removed leftover NuGet migration backup from the repository.

## Verification results ("Done when" criteria)
| Criterion | Result |
|-----------|--------|
| `dotnet --list-sdks` shows 11.0.100 | ✅ 11.0.100 present (also 8.0.428, 11.0.100-rc.3); no `global.json` in repo |
| WindowsDesktop 11.0 runtime present | ✅ `Microsoft.WindowsDesktop.App 11.0.0` runtime + `Microsoft.WindowsDesktop.App.Ref\11.0.0` targeting pack |
| VS MSBuild resolves `Microsoft.NET.Sdk` for net11.0 | ✅ Scratch `dotnet new wpf -f net11.0` project built with `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe /restore /t:Build` → `net11.0-windows\Probe.dll` |
| VS has .NET desktop components | ✅ `Workload.ManagedDesktop`, `NetCore.Component.SDK`, `NetCore.Component.Runtime.11.0`, `NetCore.Toolset.11.0.100-rtm`, `WindowsDesktop.SharedFramework.11.0` installed |
| No untracked migration leftovers | ✅ `MigrationBackup/` (3 tracked files) removed via `git rm -r` |

## Files changed
- Deleted: `MigrationBackup/ae6cc1ee/EtwExplorer/EtwExplorer.csproj`
- Deleted: `MigrationBackup/ae6cc1ee/EtwExplorer/NuGetUpgradeLog.html`
- Deleted: `MigrationBackup/ae6cc1ee/EtwExplorer/packages.config`
- `.gitignore`: no change required — already ignores `bin/`, `obj/`, `.vs/`, `**/packages/*`.

## Build / tests
- No source or project files modified → build not required (no-change short circuit). No test projects exist in the solution.

## Notes
- Scratch probe folder (`%TEMP%\wpfprobe`) and earlier Zodiacon.WPF nupkg download (`%TEMP%\zwpf`) were deleted.
- Build tool decisions recorded in `scenario-instructions.md`.
