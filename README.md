# NeoExplorer

A modern, minimal alternative to Windows File Explorer, built with WinUI 3 (Windows App SDK) and .NET 10, and packaged as MSIX.

NeoExplorer aims to look and behave like File Explorer, without the parts most people don't use: there are no tabs, no command bar under the address bar and no status bar.

## Features

- **Navigation bar in the title bar:** back, forward, up, refresh, a breadcrumb path you can click to type a path, and a search box. The window uses a Mica background.
- **Sidebar:** Quick Access (pinned and frequent folders), This PC with its drives, and Network.
- **This PC:** user folders and drives with usage bars.
- **Network:** computers found through network discovery, and their shared folders.
- **Folder views:** Details (Name, Date modified, Type, Size, sortable by clicking a column header) and Medium, Large and Extra large icons with thumbnails.
- **Search:** typing filters the current folder by name. Enter starts a recursive search, and results stream in with a Folder column.
- **File operations:** cut, copy, paste, rename in place, delete to the Recycle Bin or permanently, new folder and new text file. These go through the Windows shell, so you get File Explorer's own progress and conflict dialogs and its undo.
- **Selection:** marquee (rectangle) selection, and clicking empty space clears the selection.
- **Right-click menu:** Open (with the default app's icon), Open with, Cut, Copy, Paste, Rename, Properties and Delete.
- **Drag and drop** inside NeoExplorer and to and from File Explorer, the desktop and other apps.
- **Properties window** with General and Details tabs, laid out like File Explorer's.
- **Clipboard interop with File Explorer:** files you cut in NeoExplorer can be pasted in File Explorer, and the other way round.
- The folder refreshes on its own when files change, and the window size and view mode are remembered.

## Keyboard shortcuts

| Shortcut | Action |
|---|---|
| Alt+← / Alt+→ | Back / forward |
| Alt+↑ | Up one folder |
| F5 | Refresh |
| Ctrl+L, Alt+D | Edit the path |
| Ctrl+F, Ctrl+E | Search |
| Ctrl+Shift+1 / 2 / 3 | Extra large / large / medium icons |
| Ctrl+Shift+6 | Details |
| Ctrl+mouse wheel | Change the view |
| Ctrl+X / Ctrl+C / Ctrl+V | Cut / copy / paste |
| F2 | Rename |
| Delete | Delete to the Recycle Bin |
| Shift+Delete | Delete permanently |
| Alt+Enter | Properties |
| Ctrl+Shift+N | New folder |
| Ctrl+A | Select all |

## Requirements

- Windows 10 version 1809 (build 17763) or later. Development has been done on Windows 11.
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Optional: Visual Studio with the WinUI application development workload.

## Building and running

The app is a packaged WinUI app, so it builds for x64, ARM64 or x86 only, not Any CPU.

```bash
dotnet build NeoExplorer.slnx -p:Platform=x64
```

```bash
dotnet run --project src/NeoExplorer -p:Platform=x64
```

`dotnet run` registers a debug package identity and launches the app with it, through `Microsoft.Windows.SDK.BuildTools.WinApp`. In Visual Studio, choose the **NeoExplorer (Package)** launch profile.

To run the tests:

```bash
dotnet test tests/NeoExplorer.Tests
```

Release builds are published with ReadyToRun and trimming, using the profiles in `src/NeoExplorer/Properties/PublishProfiles`.

## Project structure

```
NeoExplorer.slnx
├─ src/NeoExplorer/          WinUI 3 app
│  ├─ Views/                 XAML pages and controls (folder view, sidebar, This PC, Network, Properties)
│  ├─ ViewModels/            View models, using CommunityToolkit.Mvvm
│  └─ Services/              Windows-only services: shell file operations, clipboard, icons,
│                            associations, Quick Access, drives, known folders, network, settings
├─ src/NeoExplorer.Core/     Plain .NET library with no UI: navigation history, folder reading,
│                            path parsing, sorting, file name validation, drop effects, formatting
└─ tests/NeoExplorer.Tests/  xUnit tests for NeoExplorer.Core
```

Logic that can be tested without Windows UI lives in `NeoExplorer.Core` and has unit tests. The WinUI project is kept thin.

## Design notes

- **No extra libraries.** Apart from the Windows App SDK, CommunityToolkit.Mvvm and xUnit, everything uses .NET, WinRT, and shell APIs through hand-written P/Invoke and `[ComImport]` interfaces.
- **The shell does the file operations.** Copy, move, rename and delete use `IFileOperation`, which shows File Explorer's dialogs, uses the Recycle Bin and supports undo. Clipboard and drag and drop use the same formats File Explorer reads and writes (`CF_HDROP` plus `Preferred DropEffect`).
- **Shell COM calls run on their own STA thread.** Calling shell COM on the UI thread can pump messages and re-enter XAML, so services such as `FileOperations` and `ShellIcons` use a dedicated thread.

## Known differences from File Explorer

- **Size on disk of a folder** rounds each file up to whole clusters. Very small files stored inside the NTFS file table count as one cluster each, where File Explorer counts them as 0 bytes.
- **Marquee selection in Details view** only starts from the empty space below the last row.
- **Properties** works on one item at a time, and Details tab values are read-only. There is no Security tab.
- **The Recycle Bin and other shell locations** that aren't file system folders open in File Explorer.
- **Search** only works in folders, so it's turned off on the This PC and Network pages. It doesn't use the Windows Search index.
