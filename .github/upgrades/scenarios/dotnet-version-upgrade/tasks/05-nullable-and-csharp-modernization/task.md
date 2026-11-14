# 05-nullable-and-csharp-modernization: Enable nullable reference types and modernize C# usage

With the solution green on .NET 11, enable `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>` in both projects and resolve every resulting warning properly (annotate nullable members, add guards, avoid `!` except where justified). Apply low-risk C# 15-era modernizations across the ~1.6k LOC: file-scoped namespaces, `using` cleanup, target-typed `new`, collection expressions, pattern matching, and `sealed` on view models where appropriate — without changing behaviour.

Research with the `modernizing-csharp-version` and `migrating-csharp-nullable-references` skills before editing. Keep MVVM semantics intact (Prism `BindableBase.SetProperty` patterns, `DelegateCommand.ObservesProperty`).

**Done when**: Both projects have nullable and implicit usings enabled, the solution builds with zero errors and zero warnings, and the app still launches and performs the open-manifest flow.

## Research (confirmed against repo)

### Scope
- 35 `.cs` files, ~1.8k LOC; no `.editorconfig`; no `<LangVersion>` (net11.0-windows → C# 15 default); no `<Nullable>`/`<ImplicitUsings>` anywhere.
- Source language level before this task: C# 7.3 idioms (block namespaces, `new T()`, `!= null`, `new[] { }`, `Substring`, explicit `Int32`/`String` BCL names in `ManifestParser.cs`).
- `Directory.Build.props` is the single shared property file → put `Nullable`, `ImplicitUsings`, `WarningsAsErrors=nullable` there.
- No tests in solution; validation = clean build (0 warnings) + headless library check (pwsh 7) + UIA open-provider flow.

### Library (`EtwManifestParsing`, public API — annotate by intent, not by compiler)
| Member | Decision | Reason |
|---|---|---|
| `EtwManifest.Events/Keywords/Templates` | non-null, `= []` default | always assigned by both parsers before the manifest is returned |
| `EtwManifest.Tasks` | `EtwTask[]?` | never set by `ParseWmiEventTraceClass`; `SummaryTabViewModel` already uses `Tasks?.Length` |
| `EtwManifest.StringTable` | `IDictionary<string,string>?` | only set when a `<stringTable>` exists; `GetString` already uses `?.` |
| `EtwManifest.ProviderName` | `string` (`= ""`) | manifest schema requires `provider@name`; WMI path uses `TraceEventProviders.GetProviderName` |
| `EtwManifest.ProviderSymbol` | `string?` | WMI path sets it from `__CLASS`, XML path from the attribute cast (null when absent) |
| `EtwManifest.GetString` | returns `string?` | dictionary lookup |
| `EtwEvent.Symbol/Opcode/Template/Keyword/Task` | `string?` | optional manifest attributes (WMI path always fills them) |
| `EtwEvent.Level` | `string` | computed via `GetString(XAttribute?)` which returns `""` when absent |
| `EtwKeyword.Name` | `string` (`required`-free: init `= ""`) | `keyword@name` required |
| `EtwKeyword.Message`, `EtwOpcode.Message` | `string?` | `message` attribute optional, `GetString` lookup can miss |
| `EtwTemplateData.Name/Type`, `EtwTemplate.Id`, `EtwTask.Name`, `EtwOpcode.Name` | `string` | required attributes; read via `Attribute(..)!.Value` so a malformed manifest still throws (as today) inside the `Parse` try/catch |
| `ManifestParser` private helpers | `ToStringArray(object?) : string[]?`, `OpcodeName(string[]? ...)`, `GetMessageString(.., string?) : string?` | mirror existing null flow |

Guards that *do* change edge behaviour (NRE → graceful), to be listed in progress-details:
- string-table entries with missing `id`/`value` are skipped instead of storing a null value (previously a null `id` threw and was swallowed; a null `value` was stored).
- `GetMessageString` / `EtwTask.GetString` return null for a null `message` instead of throwing.

### App (`EtwExplorer`)
- `MainViewModel`: `Manifest` → `EtwManifest?`, `Keywords` → `EtwKeyword[]?`, `FileName`/`ProviderName` → `string?`, `SelectedTab` → `TabViewModelBase?` (all are genuinely null at startup / after Close). `AddTabs` restructured as `if (Manifest is { } m) … else if (Keywords is { } k) …` (no `!`). `OpenRegisteredCommand` captures `vm.SelectedProvider` after `ShowDialog()==true` via pattern instead of `!`. Make `MainViewModel`/`KeywordsTabViewModel` `sealed` (no derived types).
- `EtwProviderSelectionViewModel`: `Provider` → positional `sealed record Provider(string Name, Guid Guid)` (DataGrid columns are explicit, so no `EqualityContract` leak). Static ctor → field initializer. `SelectedProvider` → `Provider?`, `SearchText` → `string?`; `(Predicate<object>)null` → target-typed `null`.
- `StringsTabViewModel.Item` → `sealed record Item(string Name, string Value)`; `StringTable` may be null → `?? []`. Drop unused `_manifest` field.
- `EventsTabViewModel`/`StringsTabViewModel` filters: `Symbol/Opcode/Task` are `string?` → null-safe `Contains`.
- `Constants`: attribute lookups return nullable → `?.Product ?? "ETW Explorer"`, `Version?.ToString(2)`.
- `Infrastructure`: `IFileDialogService` returns `string?` and takes `string? title`; `MessageBoxService._owner` → `Window?`; `DialogService` → `?? throw` on `Activator.CreateInstance`; `ImageExtension.Source` → `string?`; `InputHelper` gesture → `KeyGesture?`; `NotifyCollectionChangedEventHandler` sender is `object?`.
- Views code-behind / `App.xaml.cs`: only template usings to drop (ImplicitUsings + IDE0005).

### Mechanical pass (dotnet format, per `dotnet-format-rules.md`, range C# 8 → 15)
`.editorconfig` (new, repo root): tabs (dominant style), `csharp_style_namespace_declarations = file_scoped`, `csharp_style_implicit_object_creation_when_type_is_apparent`, `csharp_style_prefer_not_pattern`, `csharp_style_pattern_matching_over_*`, `csharp_prefer_simple_using_statement`, `dotnet_style_prefer_collection_expression`, `csharp_style_prefer_index/range_operator`, `dotnet_style_predefined_type_for_*` (IDE0049), `dotnet_diagnostic.IDE0005` (unused usings).
Command: `dotnet format EtwExplorer.sln --severity info --diagnostics IDE0005 IDE0161 IDE0090 IDE0083 IDE0078 IDE0019 IDE0020 IDE0034 IDE0049 IDE0056 IDE0057 IDE0063 IDE0300 IDE0301 IDE0305 IDE0306`.
Not applied (judgment/opt-out): primary constructors (IDE0290 — view models keep explicit ctors for readability with `BindableBase`), switch expressions (none present), `field` keyword, `with(...)` collection args, top-level statements (WPF `App`), `Lock`.

### Breaking-change check (C# 8→15)
No identifiers named `field`/`extension`/`with(`/`scoped`/`file`/`required` found in code → no Phase-0 fixes needed.

### Order
1. `.editorconfig` + `Directory.Build.props` (Nullable, ImplicitUsings) → clean build → record warning baseline.
2. `dotnet format` mechanical pass → build.
3. Library annotations (dependency order) → build.
4. App annotations + records + sealed → build 0 warnings.
5. Add `<WarningsAsErrors>nullable</WarningsAsErrors>`; final clean Debug build; headless library check; UIA open-provider smoke.
