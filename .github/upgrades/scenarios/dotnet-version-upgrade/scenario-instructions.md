# .NET Version Upgrade

## Preferences
- **Flow Mode**: Automatic
- **Target Framework**: net11.0 (WPF app: net11.0-windows)

## Key Decisions Log
- **2026-11-14 — Target .NET 11 instead of .NET 10**: User asked for the "latest dotnet". Verified via web that .NET 11 went GA on 2026-11-10 (STS, end of support 2028-11-14 — same date as .NET 10 LTS). SDK 11.0.100 and WindowsDesktop runtime 11.0.0 are installed locally, and the repo already references 11.0.0 Microsoft.Extensions/System.Text.Json packages. Assessment was regenerated for net11.0.

## Source Control
- **Source Branch**: master
- **Working Branch**: upgrade-dotnet-10
- **Commit Strategy**: After Each Task
- **Branch Sync**: Auto (Merge)

## Upgrade Options

### Strategy
- Upgrade Strategy: Bottom-Up

### Project Structure
- Project Approach: Class Libraries: In-place
- Package Management: Per-Project (defer CPM to post-migration)

### Compatibility
- Unsupported Packages: Resolve Inline (1 incompatible package: Zodiacon.WPF)

### Modernization
- Assembly Binding Redirects: Remove Binding Redirects
- Nullable Reference Types: Enable Nullable Reference Types

## Strategy
**Selected**: Bottom-Up (Dependency-First)
**Rationale**: 2 .NET Framework 4.8 projects in a 2-tier graph (EtwManifestParsing → EtwExplorer). Framework→Core boundary mandates Bottom-Up for multi-project solutions.

### Execution Constraints
- SDK-style conversion is a separate task from the TFM upgrade; convert both projects while still on net48 and verify the build before touching TFMs.
- Strict tier ordering: EtwManifestParsing (Tier 1) must build on net11.0-windows before EtwExplorer (Tier 2) is retargeted.
- Both projects target `net11.0-windows` — the library depends on WMI (System.Management) and ETW, both Windows-only; a Windows TFM avoids CA1416 noise and matches the WPF consumer.
- Keep the 11.0.0 Microsoft.Extensions/System.Text.Json package line; do NOT downgrade to 10.0.15 as the stale assessor suggested. Prefer dropping BCL polyfill packages entirely (they ship in the framework) and let TraceEvent pull its own transitive deps.
- Zodiacon.WPF is replaced by in-repo implementations (UI services, DialogViewModelBase, XAML helpers) in the EtwExplorer tier task — no stubs, no NU1701.
- Build warning-free after every task; never suppress warnings without approval.
- Nullable + C# modernization is the last code task, after the solution is green on net11.0.

## Build Tool Decisions
- **EtwExplorer.sln / EtwExplorer.csproj**: msbuild.exe (WPF — XAML markup compile needs VS targets; also legacy csproj until task 02). Resolve path via vswhere each session.
- **EtwManifestParsing.csproj**: msbuild.exe while legacy; `dotnet build` once SDK-style.
- Toolchain verified in task 01: .NET SDK 11.0.100, WindowsDesktop 11.0.0 runtime + ref pack, VS 2026 ManagedDesktop workload with .NET 11 toolset — no VS components needed installing.

## User Preferences
### Execution Style
- User requested "modernize this and upgrade to latest dotnet" — proceed autonomously (autopilot), make reasonable assumptions, do not block for confirmation.
- User authorized installing missing Visual Studio components/workloads if needed for the upgrade.
