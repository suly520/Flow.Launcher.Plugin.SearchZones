# Flow.Launcher.Plugin.SearchZones

A plugin for [Flow Launcher](https://github.com/Flow-Launcher/Flow.Launcher) that lets you create custom **search spaces** — bundles of folder searches, plugin commands, quick links, and shell commands — all accessible under a single shortcut.

---

## Features

| Entry Type | What it does |
|---|---|
| **Folder** | Searches files inside a directory (supports `%ENV_VARS%`, subdirectory toggle, exclude patterns, file-type filters) |
| **Plugin Command** | Launches a Flow Launcher action keyword (e.g. `calc`) or starts an installed program (.exe / .lnk) |
| **Quick Command** | Opens a URL in the browser or runs a shell command |

**Search backend**: Uses [Everything](https://www.voidtools.com/) for instant results when available, falls back to Windows Search Index automatically.

---

## Installation

### From Flow Launcher (recommended)

```
pm install SearchZones
```

### Manual

1. Download the latest release `.zip` from [Releases](https://github.com/suly520/Flow.Launcher.Plugin.SearchZones/releases)
2. Extract to `%APPDATA%\FlowLauncher\Plugins\SearchZones`
3. Restart Flow Launcher

---

## Usage

All commands use the `sz` action keyword:

```
sz <shortcut> <search term>
```

### Examples

| Command | What it does |
|---|---|
| `sz fh react` | Search for "react" in all folders configured under the **fh** search space |
| `sz fh` | Show quick commands, plugin commands, and folder summary for **fh** |
| `sz` | List all search spaces |
| `sz add proj Projekte D:\Projects;D:\Work` | Create a new search space |
| `sz del proj` | Delete a search space |
| `sz edit proj` | Edit a search space inline |
| `sz settings everything <path>` | Configure the Everything DLL path |

### Context Menu

Right-click a file result to:
- Open the file or folder
- Open the parent folder (with file selected)
- Copy the full path
- Copy the filename

---

## Settings UI

Open **Flow Launcher Settings → Plugins → SearchZones** to:

- Add/remove search spaces and entries via the expander UI
- Configure the Everything DLL path (auto-detected if Everything is installed)
- Browse for folders and programs
- Pick Flow Launcher keywords or installed programs from a filterable dropdown
- **Export** all search spaces to a JSON file
- **Import** search spaces from a JSON file (merge or replace)

---

## Everything Integration

SearchZones uses [Everything](https://www.voidtools.com/) for fast file searches when available:

- **Auto-detection**: The plugin automatically finds `Everything64.dll` from common install locations, the Windows Registry, or a running Everything process
- **Manual configuration**: Set the path via `sz settings everything <path>` or in the settings UI
- **Fallback**: If Everything is not available, the plugin transparently falls back to the Windows Search Index

---

## Default Search Spaces

On first run, the plugin seeds these search spaces:

| Shortcut | Name | Folders |
|---|---|---|
| `ds` | Default Search | Downloads, Desktop, Documents, Pictures, Music, Videos |
| `dl` | Downloads | Downloads |
| `doc` | Documents | Documents |
| `pic` | Pictures | Pictures |
| `desk` | Desktop | Desktop |

---

## JSON Import / Export

You can create or edit search spaces by importing a JSON file. The file must be an **array of search space objects**.

### Minimal Example

```json
[
  {
    "Name": "My Project",
    "Shortcut": "mp",
    "Description": "Project files and links",
    "SearchSubdirectories": true,
    "ExcludePatterns": ["node_modules", ".git"],
    "FileTypeFilter": [],
    "Entries": [
      {
        "Type": "Folder",
        "Value": "C:\\Projects\\my-project",
        "Label": "Project Root"
      },
      {
        "Type": "QuickCommand",
        "Value": "https://github.com/my/repo",
        "Label": "GitHub"
      },
      {
        "Type": "PluginCommand",
        "Value": "calc",
        "Label": "Calculator"
      }
    ]
  }
]
```

### Full Example (multiple search spaces)

```json
[
  {
    "Name": "University",
    "Shortcut": "uni",
    "Description": "Lectures, links, and tools",
    "SearchSubdirectories": true,
    "ExcludePatterns": [],
    "FileTypeFilter": [".pdf", ".docx", ".pptx"],
    "Entries": [
      {
        "Type": "Folder",
        "Value": "D:\\University\\Semester6",
        "Label": "Current Semester"
      },
      {
        "Type": "Folder",
        "Value": "%USERPROFILE%\\Documents\\Uni",
        "Label": "Uni Docs"
      },
      {
        "Type": "QuickCommand",
        "Value": "https://moodle.university.edu",
        "Label": "Moodle"
      },
      {
        "Type": "QuickCommand",
        "Value": "https://mail.university.edu",
        "Label": "Webmail"
      },
      {
        "Type": "PluginCommand",
        "Value": "C:\\Program Files\\Notepad++\\notepad++.exe",
        "Label": "Notepad++"
      }
    ]
  },
  {
    "Name": "Downloads",
    "Shortcut": "dl",
    "Description": "Downloads folder",
    "SearchSubdirectories": true,
    "ExcludePatterns": [],
    "FileTypeFilter": [],
    "Entries": [
      {
        "Type": "Folder",
        "Value": "%USERPROFILE%\\Downloads",
        "Label": "Downloads"
      }
    ]
  }
]
```

### Field Reference

**Search space fields:**

| Field | Type | Required | Description |
|---|---|---|---|
| `Name` | string | yes | Display name of the search space |
| `Shortcut` | string | yes | Keyword used after `sz` to activate the search space |
| `Description` | string | no | Optional description shown in settings |
| `SearchSubdirectories` | bool | no | Search subdirectories in folder entries (default: `true`) |
| `ExcludePatterns` | string[] | no | Patterns to exclude from folder search results |
| `FileTypeFilter` | string[] | no | File extensions to include (e.g. `[".pdf", ".docx"]`). Empty = all files |

**Entry fields:**

| Field | Type | Required | Description |
|---|---|---|---|
| `Type` | string | yes | `"Folder"`, `"PluginCommand"`, or `"QuickCommand"` |
| `Value` | string | yes | Path, URL, keyword, or command (see below) |
| `Label` | string | no | Display name. Falls back to `Value` if empty |

**Value by entry type:**

| Type | Value examples |
|---|---|
| `Folder` | `"C:\\MyFiles"`, `"%USERPROFILE%\\Documents"` |
| `PluginCommand` | `"calc"` (FL keyword), `"C:\\Program Files\\app.exe"` (program path) |
| `QuickCommand` | `"https://example.com"` (URL), `"ping google.com"` (shell command) |

### Import Behavior

When importing, you can choose to:
- **Merge** — imported search spaces are added alongside existing ones
- **Replace** — all existing search spaces are removed and replaced with the imported ones

IDs are automatically regenerated on import, so you don't need to include `Id` fields in your JSON.

---

## Building from Source

**Requirements**: .NET 9.0 SDK, Windows

```powershell
# Debug build + deploy to Flow Launcher
.\debug.ps1

# Release build + create zip
.\release.ps1
```

---

## Tech Stack

| Component | Technology |
|---|---|
| Language | C# / .NET 9.0 (Windows) |
| UI | WPF |
| Plugin SDK | [Flow.Launcher.Plugin](https://www.nuget.org/packages/Flow.Launcher.Plugin/) 4.4.0 |
| File search | [Everything SDK](https://www.voidtools.com/support/everything/sdk/) (primary) / Windows Search Index (fallback) |
| CI | AppVeyor |

---

## License

See [LICENSE](LICENSE) for details.