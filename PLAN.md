# NeoExplorer v1 plan

A modern, minimal alternative to Windows File Explorer, built with WinUI 3 (Windows App SDK) and .NET 10, packaged as MSIX.

## Scope

v1 supports viewing files and basic navigation:

- Navigation bar in the title bar: back, forward, up, refresh, breadcrumb path (click to edit as text), search box.
- Sidebar: Quick Access (pinned and frequent folders) and This PC (drives).
- Folder view: Details (Name, Date modified, Type, Size) and Medium / Large / Extra large icons.

Deliberately **not** included, compared with File Explorer: tabs, the command bar under the navigation bar (New, Sort, View, ...), and the status bar with the item count.

Out of scope for v1: file operations (copy, move, delete, rename), preview/details pane, Network, Linux, pin/unpin. See also [Deferred work](#deferred-work).

## Structure

```
NeoExplorer.slnx
├─ src/NeoExplorer/          WinUI 3 app (views, view models, Windows-only services)
├─ src/NeoExplorer.Core/     Plain .NET library, no UI: navigation history, folder reading, path parsing, formatting
└─ tests/NeoExplorer.Tests/  xUnit tests for NeoExplorer.Core
```

Testable logic lives in `NeoExplorer.Core`; the WinUI project stays thin.

## Dependencies

Only WinUI, Windows App SDK and .NET libraries, plus:

- **CommunityToolkit.Mvvm** (approved) for `ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`.
- **xUnit** (approved) for tests.

Any other library needs approval first.

| Need | API |
|---|---|
| List files | `DirectoryInfo.EnumerateFileSystemInfos` on a background thread, cancellable |
| Icons / thumbnails | `StorageItem.GetThumbnailAsync`, loaded lazily |
| Type column | P/Invoke `SHGetFileInfo` (`SHGFI_TYPENAME`), cached per extension |
| Quick Access | Shell COM `Shell.Application`, namespace `shell:::{679f85cb-0220-4080-b29b-5540cc05aab6}` |
| Drives | `DriveInfo.GetDrives()` |

## UI

- **Title bar:** `ExtendsContentIntoTitleBar` with Mica; the navigation bar *is* the title bar. It is a plain `Grid` rather than the WinUI `TitleBar` control, because `TitleBar` centers its content instead of letting the address bar stretch. Only the strip under the caption buttons (`DragRegion`) is passed to `SetTitleBar`, so the rest of the bar stays clickable.
- **Navigation bar:** buttons, `BreadcrumbBar` that swaps to a `TextBox` for typed paths (Enter navigates, Esc cancels), `AutoSuggestBox` for search. Shortcuts: Alt+←/→, Alt+↑, F5, Ctrl+L, Ctrl+F.
- **Sidebar:** `TreeView` with Quick Access items, a separator, and an expandable This PC node.
- **Main area:** This PC view (drives with usage bars, user folders) or folder view.
- **Details view:** `ListView` with a header row; click a header to sort, click again to reverse. Folders first.
- **Icon views:** `GridView`, same items, different `ItemTemplate` (48 / 96 / 256 px).
- **View switching** (no toolbar): Ctrl+Shift+1/2/3 (icons), Ctrl+Shift+6 (details), Ctrl+mouse wheel, and a right-click menu on empty space (View, Sort by, Refresh).
- **Opening:** double-click / Enter opens a folder, or launches a file with `UseShellExecute = true`.

## Search

- Typing filters the current folder by name immediately.
- Enter starts a cancellable recursive search in the background; results stream into the Details view with an extra Folder column.
- Windows Search index integration comes later.

## Phases

1. **Scaffold** — solution, three projects, Mica window with custom title bar, `dotnet build` / `dotnet test` work. ✅
2. **Core + tests** — `NavigationHistory`, `FolderReader`, `PathParser`, `SizeFormatter`. ✅
3. **Navigation bar** — buttons, breadcrumb, editable path, shortcuts. ✅ (Refresh is wired up but has nothing to reload until phase 4; drive labels such as "Local Disk (C:)" in the breadcrumb come with phase 6.)
4. **Details view** — columns, sorting, icons, open, refresh.
5. **Icon views** — templates, thumbnails, view switching.
6. **Sidebar** — Quick Access via Shell COM, This PC node and page.
7. **Search** — filter, then recursive search.
8. **Polish** — themes, accessibility, error handling (access denied, missing drive), remember window size and view mode.

## Deferred work

- **Recycle Bin:** Quick Access includes the Recycle Bin, which is a shell namespace (`shell:RecycleBinFolder`), not a file system path. In v1, clicking it in the sidebar opens it in **File Explorer** (`explorer.exe shell:RecycleBinFolder`). Showing its contents inside NeoExplorer needs the Shell `IShellFolder` APIs (original location, date deleted, restore) and is planned for a later version.
- Other non-file-system shell locations (Network, Linux / WSL, Control Panel) are not shown in v1.
