# 03-manifest-parsing-library — Progress Details

## What changed
- `EtwManifestParsing\EtwManifestParsing.csproj`
  - `<TargetFramework>` `net48` → `net11.0-windows` (Windows-only TFM; library depends on WMI + ETW).
  - Framework `<Reference Include="System.Management" />` replaced by `<PackageReference Include="System.Management" Version="11.0.0" />` (latest supported for net11.0-windows, verified via `get_supported_package_version`).
  - `Microsoft.Diagnostics.Tracing.TraceEvent 4.0.0` unchanged — its `netstandard2.0` asset is selected on .NET 11.
- No source code changes were needed: the assessment reported 0 API issues, and a manual scan found no Registry / P/Invoke / code-page / BinaryFormatter / AppDomain usage.

## Build
`dotnet build EtwManifestParsing\EtwManifestParsing.csproj` → **Build succeeded, 0 Warning(s), 0 Error(s)**.
Output: `bin\Debug\net11.0-windows\EtwManifestParsing.dll` (+ `Dia2Lib.dll`, `TraceReloggerLib.dll` from TraceEvent).

## Runtime validation (headless, PowerShell 7 on .NET 11.0.0)
| Path | Input | net11.0-windows result | net48 baseline |
|------|-------|------------------------|----------------|
| `ManifestParser.Parse` (XML manifest from `RegisteredTraceEventParser`) | Microsoft-Windows-Kernel-Process | 45 events / 11 keywords / 22 tasks | 45 / 11 / 22 (task 02) |
| `ManifestParser.ParseWmiEventTraceClass` (System.Management, `root\WMI`) | `MSNT_SystemTrace` `{9e814aad-3204-11d2-9a82-006008a86939}` | "Windows Kernel Trace", 515 events | "Windows Kernel Trace", 515 events |

Both code paths produce identical results on the new runtime.

## Known / expected state
- `EtwExplorer` (still `net48`) can no longer reference the `net11.0-windows` library, so the **solution-level** build is temporarily broken. This is the planned between-tier state for the bottom-up strategy and is resolved by task 04, which runs immediately next.

## Issues resolved
- None in code. (Initial headless test attempt failed only because the test harness didn't load TraceEvent from the NuGet cache — harness issue, not a library issue.)
