# 04-wpf-app-and-zodiacon-replacement — Progress Details

## What changed
### Project / configuration
- `EtwExplorer\EtwExplorer.csproj`: `net48` → `net11.0-windows`; removed `<Reference Include="System.Configuration" />`; removed `PackageReference Zodiacon.WPF 1.3.0`. Remaining packages (MahApps.Metro 2.4.11, TraceEvent 4.0.0, Prism.Core 9.0.537) resolve cleanly on .NET 11.
- Deleted `EtwExplorer\App.config` (only a `<startup>` element; replaced by `runtimeconfig.json`).
- Deleted unused VS-template boilerplate: `Properties\Settings.settings`, `Properties\Settings.Designer.cs`, `Properties\Resources.resx`, `Properties\Resources.Designer.cs` (both were empty and never referenced; this removed the only `System.Configuration` dependency).

### Zodiacon.WPF replacement — new `EtwExplorer\Infrastructure\` (written fresh)
| File | Replaces |
|---|---|
| `IUIServices.cs` | `IUIServices`, `IFileDialogService`, `IMessageBoxService`, `IDialogService`, `UIServicesDefaults` |
| `FileDialogService.cs` | `GetFileForOpen(filter, title)` / `GetFileForSave(filter)` via `Microsoft.Win32` dialogs |
| `MessageBoxService.cs` | `ShowMessage(text, caption[, button, image])`, `SetOwner(Window)` |
| `DialogService.cs` | `CreateDialog<TViewModel, TWindow>()` — creates window, constructs VM with the window, sets `DataContext`/`Owner` |
| `DialogViewModelBase.cs` | `BindableBase`-derived base with `OKCommand`/`CancelCommand`, `ShowDialog()`, virtual `OnOK()`/`OnCancel()` |
| `FocusProperties.cs` | attached `FocusOnLoad` |
| `InputHelper.cs` | attached `AttachInputBindings` — turns child `MenuItem.InputGestureText` into `KeyBinding`s on the hosting window |
| `ImageExtension.cs` | `{z:Image path, Width, Height}` markup extension |
| `TabControlProperties.cs` | attached `IsCached` — keeps each tab's content alive across selection changes (falls back to default behaviour if `PART_SelectedContentHost` is absent) |

### Consumers repointed
- `App.xaml.cs`, `ViewModels\MainViewModel.cs`, `ViewModels\EtwProviderSelectionViewModel.cs`: `using Zodiacon.WPF` → `using EtwExplorer.Infrastructure` (also dropped dead `System.Configuration`/`System.Data` usings in `App.xaml.cs`).
- `Views\MainView.xaml`, `Views\EventsTabView.xaml`, `Views\EtwProviderSelectionDialog.xaml`: `xmlns:z="http://zodiacon.wpf/2016"` → `xmlns:z="clr-namespace:EtwExplorer.Infrastructure"`. No other XAML edits were needed — the attached-property / extension names were kept identical.

## Build
VS MSBuild, `EtwExplorer.sln /t:Rebuild` → **Build succeeded, 0 Warning(s), 0 Error(s)** (both projects now `net11.0-windows`). `runtimeconfig.json` targets `Microsoft.NETCore.App 11.0.0` + `Microsoft.WindowsDesktop.App 11.0.0`.

## Runtime validation
1. **Launch**: app starts on .NET 11, window title `ETW Explorer v0.40 © 2019 Pavel Yosifovch`, MahApps theming applied.
2. **Open registered provider flow (UI Automation)**: toolbar "Open Provider..." → `Select ETW Provider` dialog opens (`DialogService`/`DialogViewModelBase`) → typed `Kernel-Process` into the search box → 2 filtered rows → selected row → OK → dialog closes (`OnOK` → `DialogResult`) → main window shows 5 tabs (Summary, Events, Keywords, Strings, Xml) and status bar `Microsoft-Windows-Kernel-Process` → switching tabs works, process stays alive.
3. **In-process harness** (throwaway console project referencing the built EtwExplorer assembly; deleted afterwards) — 12/12 checks passed:
   - `InputHelper`: 3 `KeyBinding`s registered on the window (Ctrl+O / Ctrl+S / Ctrl+F4), gesture-less and command-less items skipped, bindings execute the menu commands.
   - `FocusOnLoad`: TextBox receives focus after `Loaded`.
   - `ImageExtension`: produces a 16×16 `Image` with a decoded `BitmapImage` for the pack URI.
   - `TabControlProperties.IsCached`: template host swapped for the cache panel; content instance for a tab is reused with its state preserved after switching away and back; cached content is dropped on item removal and on `Clear()`.
4. Open-XML / Save-XML use the standard `Microsoft.Win32` file dialogs (not driveable from this session); the post-dialog code path (`DoOpenFile`/`DoOpenXml`) is the same one exercised by the registered-provider flow.

## Issues encountered
- Keyboard-driven checks (Ctrl+O via SendKeys) couldn't be performed externally because the test session cannot bring the app window to the foreground; verified `InputHelper` in-process instead.
- UIA `FindAll` on the desktop root failed in this environment; attached via `AutomationElement.FromHandle` on the process's top-level windows instead.
