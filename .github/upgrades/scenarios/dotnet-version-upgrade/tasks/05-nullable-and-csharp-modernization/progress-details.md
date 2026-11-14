# Progress Details — 05-nullable-and-csharp-modernization

## Summary

Enabled nullable reference types and implicit usings solution-wide, applied a mechanical
C# modernization pass with `dotnet format`, and hand-fixed all 109 distinct nullable
diagnostics (147 total) so both projects build with **0 warnings / 0 errors**. Nullable
warnings are now promoted to errors (`<WarningsAsErrors>nullable</WarningsAsErrors>`) so
the codebase stays clean.

## What changed

### Build configuration
- `Directory.Build.props`: `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`,
  `<WarningsAsErrors>$(WarningsAsErrors);nullable</WarningsAsErrors>`.
- New `.editorconfig` (root): tabs for C#/XAML/MSBuild, UTF‑8 BOM for `.cs`, K&R brace style
  pinned to match the existing code (`csharp_new_line_before_open_brace = none`,
  `else`/`catch`/`finally` on their own line), and the modernization rules used by the format pass.

### Mechanical pass (`dotnet format --severity info`, all 35 `.cs` files)
- IDE0005 removed unused usings (implicit usings now cover `System`, `System.Linq`, `System.Collections.Generic`, …).
- IDE0090 target-typed `new()`; IDE0300/0301/0305/0306 collection expressions (`[]`, `[.. x]`, `[scalar]`).
- IDE0049 predefined type keywords; IDE0083 `is not`; IDE0019/0020 pattern matching; IDE0056/0057 index/range.
- File-scoped namespaces (IDE0161) were intentionally **not** applied: the first attempt also reflowed
  every brace to Allman style, so the tree was reverted and the brace style pinned in `.editorconfig` first.
  Block namespaces were kept to minimise churn.
- `System.IO.EnumerationOptions` vs `System.Management.EnumerationOptions` ambiguity (introduced by implicit
  usings) resolved with a `using EnumerationOptions = System.Management.EnumerationOptions;` alias.

### EtwManifestParsing (library, public surface annotated)
- `EtwManifest`: `Events`/`Keywords`/`Templates` non-null (`= []`); `Tasks` → `EtwTask[]?` (null for MOF providers);
  `StringTable` → `IDictionary<string,string>?`; `ProviderName` → `string` (`""` default); `ProviderSymbol` → `string?`;
  `GetString` → `string?`.
- `EtwEvent`: `Symbol`/`Opcode`/`Template`/`Keyword`/`Task` → `string?` (optional manifest attributes); `Level` non-null.
- `EtwKeyword`: `Name` non-null (`""`), `Message` → `string?`.
- `EtwOpcode`, `EtwTemplateData`: `required`/`init` properties; `Message` → `string?`.
- `EtwTask`/`EtwTemplate`: schema-required attributes read via `Attribute("x")!.Value` so a malformed manifest
  still throws and is surfaced by `ManifestParser.Parse` as `ApplicationException("Failed to parse manifest XML")`
  (same behaviour as before).
- `ManifestParser`:
  - XML path: string-table loop uses `TryAdd` and skips entries lacking id/value (previously achieved by
	swallowing exceptions); `GetMessageString`/`EtwTask.GetString` return `null` for a null message instead of throwing.
  - WMI path: `qd.Value.GetType() == typeof(Int32|String|Boolean)` replaced by `is int`/`is string`/`is bool`
	patterns (sealed types → identical semantics), `.GetType().IsArray` → `is Array`; `ClassName()` helper
	centralises the `__CLASS` read; `ToStringArray(object?) : string[]?`, `OpcodeName(string[]? …)`.
  - A null qualifier value in `ToMofString` now throws `ApplicationException("Unsupported Qualifier Type - null")`
	instead of `NullReferenceException`.

### EtwExplorer (WPF app)
- `MainViewModel` → `sealed`; `Manifest`/`Keywords`/`FileName`/`ProviderName`/`SelectedTab` nullable.
  `AddTabs` pattern-matches on `Manifest` first, then `Keywords`; `SelectedTab` is `null` when there are no tabs.
  `OpenRegisteredCommand` checks `SelectedProvider` once up-front; `DoSave` guards `Manifest`.
- `Provider` → `sealed record Provider(string Name, Guid Guid)` with an `IsLikelyName` helper; providers list is a
  static readonly field initialiser (no static ctor); `SelectedProvider`/`SearchText` nullable; `(Predicate<object>)null` → `null`.
- `StringsTabViewModel.Item` → `sealed record Item(string Name, string Value)`; `Strings` is `[]` when the manifest has
  no string table; dead `_manifest` field and `Debug.Assert` removed.
- `EventsTabViewModel` filter is null-safe (`Matches(string?, string)` using `OrdinalIgnoreCase`) — events with a missing
  symbol/opcode/task no longer NRE the filter.
- `KeywordsTabViewModel` → `sealed`; `SummaryTabViewModel.ProviderSymbol` → `string?`.
- `Constants`: null-safe attribute reads with fallbacks (`"ETW Explorer"`, `Version(0,0)`).
- Infrastructure: `IFileDialogService` returns `string?` and takes `string? title`; `DialogService` throws
  `InvalidOperationException` if `Activator.CreateInstance` returns null; `MessageBoxService._owner`,
  `ImageExtension.Source`, `InputHelper` gesture, `TabControlProperties.OnItemsChanged(object? sender, …)` annotated.

### Not applied (by design)
Primary constructors, `field` keyword, top-level statements, `System.Threading.Lock`, file-scoped namespaces.

## Build

Clean `dotnet build EtwExplorer.sln` (Debug): **0 Warning(s), 0 Error(s)** for both projects, with
`nullable` warnings treated as errors.

## Validation

- Headless library check (pwsh 7 / .NET 11, loading TraceEvent 4.0.0 + System.Management 11.0.0 + `EtwManifestParsing.dll`):
  - `Parse(GetManifestForRegisteredProvider(Kernel-Process))` → 45 events, 11 keywords, 22 tasks, 27 templates, 39 strings ✔ (matches pre-change baseline)
  - `ParseWmiEventTraceClass(MSNT_SystemTrace)` → "Windows Kernel Trace", 515 events, 23 keywords, 261 templates, 320 strings, `Tasks == null` ✔
- UI Automation smoke (`powershell -STA`, `session-state\files\uia-smoke.ps1`):
  launch → "Open Provider..." → filter `Kernel-Process` (2 rows) → select → OK → **5 tabs**, status bar shows
  `Microsoft-Windows-Kernel-Process`; Strings tab DataGrid exposes 39 rows × 2 cols with correct cell values
  (record binding verified); process stays alive.

## Issues resolved
- First `dotnet format` run reflowed braces to Allman and block namespaces → reverted, pinned style in `.editorconfig`, re-ran.
- CS0104 `EnumerationOptions` ambiguity → alias.
- `dotnet format` duplicated the alias line on re-run → de-duplicated.
