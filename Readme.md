Flow.Launcher.Plugin.SearchZones
==================

A plugin for [Flow Launcher](https://github.com/Flow-Launcher/Flow.Launcher) that lets you create custom **search spaces** — bundles of folder searches, plugin commands, quick links, and shell commands — all accessible under a single shortcut.

---

### Usage

    sz <shortcut> <search term>

- `sz fh react` — Search for "react" in all folders configured under the **fh** search space
- `sz fh` — Show all quick commands, plugin commands, and a folder summary for **fh**
- Press **Tab** on a quick command to copy its URL/command into the search bar

---

### Features

| Entry Type | What it does |
|---|---|
| **Folder** | Searches files inside a directory (supports `%ENV_VARS%`, subdirectory toggle, exclude patterns, file-type filters) |
| **Plugin Command** | Launches a Flow Launcher action keyword (e.g. `calc`) or starts an installed program (.exe / .lnk) |
| **Quick Command** | Opens a URL in the browser or runs a shell command |

---

### Settings

Open Flow Launcher Settings → Plugins → SearchZones to:
- Add/remove search spaces and entries via the expander UI
- **Export** all search spaces to a JSON file
- **Import** search spaces from a JSON file (merge or replace)

---

### JSON Format

You can create or edit search spaces by importing a JSON file. The file must be an **array of search space objects**.

#### Minimal Example

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

#### Full Example (multiple search spaces)

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

#### Field Reference

**Search space fields:**

| Field | Type | Required | Description |
|---|---|---|---|
| `Name` | string | yes | Display name of the search space |
| `Shortcut` | string | yes | Keyword used after `sz` to activate the search space |
| `Description` | string | no | Optional description shown in settings |
| `SearchSubdirectories` | bool | no | Search subdirectories in folder entries (default: `true`) |
| `ExcludePatterns` | string[] | no | Glob patterns to exclude from folder search results |
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

#### Import Behavior

When importing, you can choose to:
- **Merge** — imported search spaces are added alongside existing ones
- **Replace** — all existing search spaces are removed and replaced with the imported ones

IDs are automatically regenerated on import, so you don't need to include `Id` fields in your JSON.