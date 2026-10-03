# NeoExplorer v2 plan: selection, file operations, properties, drag and drop

v1 left file operations out of scope (see [PLAN.md](PLAN.md)). v2 adds them, behaving like File Explorer:

1. Drag a selection rectangle (marquee) over items to select them.
2. Clicking empty space clears the selection.
3. A right-click menu on items: Open (with the default app's icon), Open with, Cut, Copy, Rename, Properties, Delete.
4. A Properties window with the General and Details tabs, laid out like File Explorer's, without the "Advanced..." button and without the "Remove Properties and Personal Information" link.
5. Drag and drop of files and folders, inside NeoExplorer and to and from File Explorer and other apps.

No new libraries are needed. Everything uses Windows App SDK / WinRT APIs or shell APIs through hand-written P/Invoke and `[ComImport]`, like `ShellInfo` and `ShellIcons` do today.

## Key decisions

| Need | API | Why |
|---|---|---|
| Copy, move, delete, rename | Shell `IFileOperation` | Gives File Explorer's own progress dialog, "Replace or skip files" dialog, Recycle Bin, and Ctrl+Z undo in Explorer. Reimplementing these with `File.Copy` would be a lot of work and still behave differently. |
| Cut / Copy / Paste | OLE clipboard with `CF_HDROP` plus `Preferred DropEffect` | The formats File Explorer reads and writes, so cut in NeoExplorer and paste in Explorer works, and the other way round. |
| Drag and drop | WinUI `CanDragItems` / `AllowDrop` with `StandardDataFormats.StorageItems` | Works with File Explorer in both directions. The drop itself goes through `IFileOperation`. |
| Default app and its icon | `AssocQueryString` (`ASSOCSTR_EXECUTABLE`, falling back to `ASSOCSTR_APPICONREFERENCE` for Store apps such as Photos) | The icon goes in the "Open" item through `ImageIcon`. |
| Open with | `SHAssocEnumHandlers` (recommended handlers, each with name, icon and `Invoke`), plus "Choose another app" via `SHOpenWithDialog` | Same submenu as File Explorer. |
| Folder changes | `FileSystemWatcher` on the current folder, debounced | Copies, deletes and changes made by other apps show up without pressing F5. |
| Attributes, times, sizes | `FileSystemInfo`, `GetCompressedFileSize`, `GetDiskFreeSpace` (cluster size) | "Size on disk" needs the compressed size rounded up to clusters. |
| Details tab | Shell property system: `SHGetPropertyStoreFromParsingName`, the item's `System.PropList.FullDetails` list, `IPropertyDescription` for names and formatting | The same property groups, names and value formatting as Explorer's Details tab. |

**Shell COM runs on its own STA thread.** `ShellIcons` already documents that shell COM calls on the UI thread can pump messages, re-enter XAML and crash. `IFileOperation.PerformOperations` shows modal dialogs and pumps messages for the whole operation, so a `FileOperations` service runs each operation on a dedicated STA thread, with the main window as owner (`SetOwnerWindow`) so its dialogs stay on top of NeoExplorer.

**Selection is tracked by path.** Today a refresh replaces `Items` and loses the selection. With auto-refresh after every operation, the folder view model keeps the selected paths and restores them after a reload, and selects new items after paste, drop and rename, like Explorer.

**Testable logic goes in `NeoExplorer.Core`** with xUnit tests, as in v1:

- `MarqueeHitTest`: which item indexes a rectangle covers, from the row height (Details) or tile size and column count (icons). This works out the covered items mathematically, so items that are virtualized (scrolled out of view) are included.
- `FileNameValidator`: invalid characters, reserved names (CON, NUL, ...), trailing dots and spaces, length; and which part of the name to preselect when renaming (without the extension).
- `DropEffects`: move on the same drive, copy to another drive, Ctrl forces copy, Shift forces move; rejects dropping a folder into itself or one of its subfolders, or onto the folder an item already sits in.
- `SizeFormatter` additions: "1.23 MB (1,294,336 bytes)" as in Properties.
- `FolderSize`: files, folders, size and size on disk, computed in the background with progress.

## Phases

### 1. Foundations ✅

- `Services/FileOperations.cs`: `IFileOperation` wrapper (copy, move, delete to Recycle Bin, delete permanently, rename) on an STA thread.
- `Services/ShellClipboard.cs`: write and read file lists with the drop effect.
- `Services/ShellAssociations.cs`: default app, its icon, the Open with handlers.
- `FileSystemWatcher` in `FolderViewModel`, refreshing at most every ~300 ms and keeping the selection and scroll position.
- The Core helpers listed above, with tests.

### 2. Selection ✅

- **Clicking empty space:** a `PointerPressed` handler on `ItemsList` and `IconsGrid`, added with `handledEventsToo: true`, checks whether the press landed outside any `ListViewItem` / `GridViewItem`. If it did, the selection is cleared, unless Ctrl or Shift is held.
- **Marquee:** the same press starts a selection rectangle. A `Canvas` overlay above the list draws a translucent accent `Rectangle`. While the pointer is captured:
  - The anchor point is stored in content coordinates (plus the `ScrollViewer` offset), so the rectangle stays anchored when the list scrolls.
  - Near the top or bottom edge the list scrolls automatically.
  - `MarqueeHitTest` gives the covered indexes, and `SelectedItems` is updated. With Ctrl held, the covered items are toggled relative to the selection at the start of the drag, as in Explorer.
  - A small threshold (a few pixels) separates a click from a drag.
- **Where a marquee can start in Details view:** File Explorer starts one from blank space anywhere, including the part of a row to the right of the name. NeoExplorer's rows are selectable across their full width, so in this phase marquees start only from space that has no row (below the last item). Starting from the Date, Type or Size columns can come later if you want it.

### 3. Right-click menu, rename and shortcuts ✅

- Right-clicking an item that isn't selected selects only that item first (Explorer behavior). Items get their own `MenuFlyout`, and the existing View / Sort by / Refresh menu stays for empty space.
- Menu: **Open** (default app's icon; bold, as in Explorer) · **Open with ▸** (files only; recommended apps, a separator, "Choose another app") · **Cut** · **Copy** · **Rename** (one item only) · **Properties** · **Delete**.
- With several items selected: Open opens each one, and Open with and Rename are hidden, as Explorer does.
- **Paste** is added to the empty-space menu, and to the menu of a single folder item. It isn't on your list, but without it Cut and Copy can only paste into File Explorer.
- **Rename in place:** `ItemViewModel.IsRenaming` swaps the name `TextBlock` for a `TextBox` in both templates, with the name preselected without its extension. Enter or clicking elsewhere confirms, Esc cancels. The name is checked with `FileNameValidator`, and the rename itself goes through `FileOperations`, so it can be undone in Explorer.
- Cut items are shown faded (semi-transparent icon) until they are pasted or the clipboard changes.
- Shortcuts: Ctrl+X / C / V, Delete (Recycle Bin), Shift+Delete (permanent delete, after the shell's confirmation), F2 (rename), Alt+Enter (Properties). Ctrl+A already works through `ListView`.
- Search results: every command works on the items themselves. There is no Paste and no drop onto empty space there, because search results don't belong to one folder.

### 4. Drag and drop ✅

- **Dragging out:** `CanDragItems="True"`. `DragItemsStarting` puts the selected items on the package with `SetDataProvider(StorageItems)`, so the `StorageFile` / `StorageFolder` objects are created only when a target asks for them. `RequestedOperation` = Copy | Move.
- **Drop targets:** folder items (the item under the pointer is highlighted) and empty space (the current folder).
  - `DragOver` uses `DropEffects` to choose the operation and to reject invalid targets.
  - It sets `DragUIOverride.Caption` to "Move to *Folder*" or "Copy to *Folder*".
  - It scrolls the list automatically near its edges.
- **Drop:** gets the items, turns them into paths and runs `FileOperations` move or copy. The dropped items are then selected.
- Works the same with items coming from File Explorer, the desktop and other apps.
- **Pressing an item and dragging** starts a drag-and-drop. **Pressing empty space and dragging** starts a marquee (phase 2).
- Later, not in this phase: dropping onto sidebar entries (Quick Access folders and drives), and right-button drag with the "Copy here / Move here" menu.

### 5. Properties window ✅

- A new `PropertiesWindow`: a fixed size of about 380 × 520, not resizable and without minimize or maximize buttons, titled "*Name* Properties". Mica background, like the main window. Tabs use `SelectorBar`. OK, Cancel and Apply buttons are at the bottom, and Apply turns on after a change.
- Opened from the menu or with Alt+Enter, one window per item. In this phase it handles one item only. Explorer's combined "3 items" Properties for a multiple selection can come later.
- **General tab, file**, in the same order and with the same separators as Explorer:
  - The icon, and the name as an editable `TextBox` (OK or Apply renames the file).
  - Type of file.
  - Opens with: the app's icon and name, and a "Change..." button that opens `SHOpenWithDialog` set to save the default.
  - Location, Size, Size on disk.
  - Created, Modified, Accessed.
  - Attributes: Read-only, Hidden. No "Advanced..." button.
- **General tab, folder:** Type ("File folder"), Location, Size, Size on disk and Contains ("12 Files, 3 Folders"), which count up live while `FolderSize` runs, then Created and Attributes. Read-only is three-state, with "(Only applies to files in folder)", and applies to the contents as Explorer does: after a confirmation for this folder only or for subfolders and files too.
- **Details tab:** a Property / Value list grouped under headings (Description, Origin, Image, File, ...), built from the item's own "full details" property list in the shell, so it shows the same properties Explorer does for that file type. Values are read-only in this version. The "Remove Properties and Personal Information" link is left out.
- There is no Security tab.
- Edits that need administrator rights, such as attributes of protected files, show the error from Windows.

## Testing

**Automated tests:** xUnit tests for every Core helper. File-system tests only touch folders made with `Directory.CreateTempSubdirectory` (the existing `FolderReaderTests` pattern) and delete them afterwards.

**Manual and UI tests** in the running app, only inside a sandbox folder created for the purpose, e.g. `%TEMP%\NeoExplorerSandbox\`, filled with test files and folders:

- Only that folder is opened in NeoExplorer, and drags go between it and a File Explorer window that also shows only that folder.
- Delete tests send only sandbox items to the Recycle Bin. The Recycle Bin is not emptied, and nothing in it is restored.
- "Change..." in Opens with, and "Always" in Choose another app, are not pressed, because both change the default apps. Open and Open with are tested only on sandbox files, with harmless apps (e.g. a `.txt` in Notepad).
- Cut and Copy tests replace whatever is on the clipboard at the time.
- The sandbox folder is deleted at the end, with a command scoped to that path.

## Decisions

- The Details tab is built, without the "Remove Properties and Personal Information" link.
- No Security tab.
- Phases in the order above.

## Known differences from File Explorer

- **Size on disk of a folder** adds up each file's size rounded up to whole clusters. Very small files that NTFS keeps inside its file table count as one cluster each, where Explorer counts them as 0 bytes. For a single file, the size on disk is read from the file system and matches Explorer.
- **Marquee in Details view** starts only from space below the last row (see phase 2).
- **Properties** handles one item at a time; there is no combined Properties for a multiple selection yet.
- **Details tab** values are read-only.
- **Alt+Enter** is handled in `PreviewKeyDown`, because with Alt held the list neither handles Enter nor passes it on to keyboard accelerators.
