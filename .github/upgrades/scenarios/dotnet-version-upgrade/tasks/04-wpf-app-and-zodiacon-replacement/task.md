# 04-wpf-app-and-zodiacon-replacement: Upgrade EtwExplorer to net11.0-windows and replace Zodiacon.WPF

Retarget the WPF application to `net11.0-windows` with `UseWPF`, and resolve the one incompatible package inline: Zodiacon.WPF 1.3.0 is net462-only with no modern release. Its used surface is small — `IUIServices`/`UIServicesDefaults` (`FileDialogService.GetFileForOpen/GetFileForSave`, `MessageBoxService.ShowMessage/SetOwner`, `DialogService.CreateDialog<TVM,TView>`), `DialogViewModelBase` (ctor taking `Window`, `ShowDialog()`, `OnOK()`), and four XAML helpers (`z:FocusProperties.FocusOnLoad`, `z:InputHelper.AttachInputBindings`, `z:Image` markup extension, `z:TabControlProperties.IsCached`). Implement equivalent types in a new `EtwExplorer/Infrastructure` (or similar) folder and repoint `using Zodiacon.WPF` and the `xmlns:z` namespace to them; write these fresh rather than copying the Apache-2.0 source.

Check MahApps.Metro 2.4.11 (supports net6.0-windows+) and Prism.Core 9.0.537 (netstandard2.0/net8.0) resolve cleanly; drop the explicit `Microsoft.Xaml.Behaviors.Wpf` reference (transitive via MahApps, not used in XAML). `Properties/Settings.settings` + `Settings.Designer.cs` rely on `System.Configuration.ApplicationSettingsBase`, which exists in `Microsoft.WindowsDesktop.App` — verify they still compile. Remove the `System.Web`/`System.Web.Extensions` framework references (unused on inspection). Validate the app runs: open an XML manifest, open a registered provider, and confirm tabs render with MahApps theming.

**Done when**: EtwExplorer builds on `net11.0-windows` with zero errors and zero warnings, has no reference to Zodiacon.WPF, the solution builds end-to-end, and the application launches and exercises the open-manifest / open-registered-provider flows.

## Research Findings (confirmed against repo after task 02/03)

### Current csproj state
SDK-style, `net48`, `WinExe`, `UseWPF`, `ApplicationIcon app.ico`, `<Reference Include="System.Configuration" />`, ProjectReference → EtwManifestParsing (now net11.0-windows → currently unresolvable), packages: MahApps.Metro 2.4.11, TraceEvent 4.0.0, Prism.Core 9.0.537, Zodiacon.WPF 1.3.0. `Microsoft.Xaml.Behaviors.Wpf`, `System.Web`, `System.Web.Extensions` were already pruned in task 02 — nothing to do for those.

### Zodiacon.WPF usage (exhaustive, via grep)
| Member | Where | Required behaviour |
|---|---|---|
| `UIServicesDefaults` (`IUIServices`) | `App.xaml.cs:19`, `MainViewModel.cs:20,39,48` | Bundles `FileDialogService`, `MessageBoxService`, `DialogService`. |
| `IFileDialogService.GetFileForOpen(filter, title)` / `GetFileForSave(filter)` | `MainViewModel.cs:71,188` | Returns chosen path or `null`. |
| `IMessageBoxService.ShowMessage(text, caption[, MessageBoxButton, MessageBoxImage])` + `SetOwner(Window)` | `App.xaml.cs:23`, `MainViewModel.cs:102,120,183,199` | Owner-parented `MessageBox`. |
| `IDialogService.CreateDialog<TVM, TWindow>()` | `MainViewModel.cs:89` | New `TWindow`, `TVM(Window)` ctor, sets `DataContext`, `Owner = MainWindow`; returns VM. |
| `DialogViewModelBase(Window)` with `ShowDialog() : bool?`, `virtual OnOK()`, `OKCommand`, `CancelCommand` | `EtwProviderSelectionViewModel.cs:17,27,85`; XAML `EtwProviderSelectionDialog.xaml:38-39` bind `OKCommand`/`CancelCommand` | Derives from Prism `BindableBase` (VM uses `SetProperty`). `OnOK` override returns early without closing when no selection. |
| `z:FocusProperties.FocusOnLoad` | `EventsTabView.xaml:20`, `EtwProviderSelectionDialog.xaml:22` | Focus the TextBox on `Loaded`. |
| `z:InputHelper.AttachInputBindings` | `MainView.xaml:20` (top `MenuItem File`) | For each child `MenuItem` with `InputGestureText` + `Command`, register a `KeyBinding` on the hosting `Window` (Ctrl+O, Ctrl+S, Ctrl+F4). |
| `{z:Image path, Width=, Height=}` | `MainView.xaml:22` (`MenuItem.Icon`) | Markup extension producing an `Image` with pack-URI `BitmapImage`. |
| `z:TabControlProperties.IsCached` | `MainView.xaml:42` | Keep each tab's content element alive when switching tabs (tabs are VMs rendered via implicit `DataTemplate`s in `Resources/Templates.xaml`; default TabControl recreates content per switch, losing scroll/filter state). Implement by swapping `PART_SelectedContentHost` with a host panel of per-item cached `ContentPresenter`s; if the template part is not found, fall back to default behaviour. |

Namespace: `xmlns:z="http://zodiacon.wpf/2016"` in 3 XAML files → will be replaced by `clr-namespace:EtwExplorer.Infrastructure` (explicit, no `XmlnsDefinition` needed).

### Settings / Resources boilerplate
- `Properties\Settings.settings` is empty (`<Settings />`); `Settings.Default` is never referenced. `Properties\Resources.resx` holds only the template header; `Properties.Resources` is never referenced. Both are unused VS-template boilerplate → **delete** (`Settings.settings`, `Settings.Designer.cs`, `Resources.resx`, `Resources.Designer.cs`) and drop `<Reference Include="System.Configuration" />`. This removes the only `System.Configuration` dependency.
- `App.config` contains only `<startup>` → **delete** (meaningless on .NET 11; `runtimeconfig.json` replaces it).
- `App.xaml.cs` has unused `using System.Configuration; using System.Data;` → remove (would otherwise be the only System.Configuration touchpoint; the namespace exists in WindowsDesktop so it compiles either way, but it's dead code tied to this change).

### Packages on net11.0-windows
- MahApps.Metro 2.4.11 — `net6.0-windows` asset; brings `ControlzEx` + `Microsoft.Xaml.Behaviors.Wpf` transitively. Styles used: `Controls.xaml`, `Fonts.xaml`, `Themes/Light.Cobalt.xaml`, `MetroWindow`, `TextBoxHelper`, `ControlsHelper` — all exist in 2.4.x.
- Prism.Core 9.0.537 — `net8.0`/`netstandard2.0` assets; `BindableBase`, `DelegateCommand` used.
- TraceEvent 4.0.0 — netstandard2.0 (validated in task 03).

### Plan
1. csproj: `net11.0-windows`; remove `System.Configuration` reference and Zodiacon.WPF package.
2. Delete `App.config`, `Properties\Settings.*`, `Properties\Resources.*`.
3. Add `EtwExplorer\Infrastructure\`: `IUIServices.cs`/`UIServicesDefaults.cs`, `FileDialogService.cs`, `MessageBoxService.cs`, `DialogService.cs`, `DialogViewModelBase.cs`, `FocusProperties.cs`, `InputHelper.cs`, `ImageExtension.cs`, `TabControlProperties.cs`.
4. Repoint `using Zodiacon.WPF` (3 files) and `xmlns:z` (3 XAML files).
5. Build solution with VS MSBuild (`/tl:off`) → 0 warnings; launch, exercise flows headlessly where possible (UI automation limited — verify launch, window title, and that the open-provider dialog types construct).
