using Flow.Launcher.Plugin.SearchZones.Models;

namespace Flow.Launcher.Plugin.SearchZones.Services;

public class TemplateManager
{
    private readonly PluginSettings _settings;
    private readonly IPublicAPI _api;
    private readonly string _pluginId;

    public TemplateManager(PluginSettings settings, IPublicAPI api, string pluginId)
    {
        _settings = settings;
        _api = api;
        _pluginId = pluginId;
    }

    public List<SearchTemplate> GetAllTemplates() => _settings.Templates;

    public SearchTemplate? GetTemplateByShortcut(string shortcut)
    {
        return _settings.Templates.FirstOrDefault(
            t => t.Shortcut.Equals(shortcut, StringComparison.OrdinalIgnoreCase));
    }

    public bool AddTemplate(SearchTemplate template, out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(template.Shortcut))
        {
            error = "Shortcut darf nicht leer sein.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(template.Name))
        {
            error = "Name darf nicht leer sein.";
            return false;
        }

        if (GetTemplateByShortcut(template.Shortcut) != null)
        {
            error = $"Shortcut '{template.Shortcut}' wird bereits verwendet.";
            return false;
        }

        _settings.Templates.Add(template);
        _api.SavePluginSettings();
        return true;
    }

    public bool EditTemplate(string id, Action<SearchTemplate> update, out string error)
    {
        error = string.Empty;
        var template = _settings.Templates.FirstOrDefault(t => t.Id == id);
        if (template == null)
        {
            error = "Template nicht gefunden.";
            return false;
        }

        var oldShortcut = template.Shortcut;
        update(template);

        // Validate shortcut uniqueness if changed
        if (!oldShortcut.Equals(template.Shortcut, StringComparison.OrdinalIgnoreCase))
        {
            var conflict = _settings.Templates.FirstOrDefault(
                t => t.Id != id && t.Shortcut.Equals(template.Shortcut, StringComparison.OrdinalIgnoreCase));
            if (conflict != null)
            {
                error = $"Shortcut '{template.Shortcut}' wird bereits verwendet.";
                template.Shortcut = oldShortcut;
                return false;
            }
        }

        _api.SavePluginSettings();
        return true;
    }

    public bool DeleteTemplate(string id)
    {
        var template = _settings.Templates.FirstOrDefault(t => t.Id == id);
        if (template == null)
            return false;

        _settings.Templates.Remove(template);
        _api.SavePluginSettings();
        return true;
    }

    public void RegisterAllKeywords()
    {
        // Remove any template shortcuts that may have been registered as action keywords
        // in a previous version of the plugin. Only "sz" should remain.
        foreach (var template in _settings.Templates)
        {
            if (_api.ActionKeywordAssigned(template.Shortcut))
                _api.RemoveActionKeyword(_pluginId, template.Shortcut);
        }
    }

    /// <summary>
    /// Converts legacy IncludeFolders lists into the new Entries model.
    /// Idempotent — skips templates that already have Entries populated.
    /// Call once during plugin initialisation, before SeedDefaults().
    /// </summary>
    public void MigrateTemplates()
    {
        var migrated = false;
        foreach (var template in _settings.Templates)
        {
            if (template.IncludeFolders.Count > 0 && template.Entries.Count == 0)
            {
                foreach (var folder in template.IncludeFolders)
                {
                    template.Entries.Add(new TemplateEntry
                    {
                        Type = EntryType.Folder,
                        Value = folder,
                        Label = Path.GetFileName(folder.TrimEnd('\\', '/'))
                    });
                }
                template.IncludeFolders.Clear();
                migrated = true;
            }
        }

        if (migrated)
            _api.SavePluginSettings();
    }

    public void SeedDefaults()
    {
        if (!_settings.IsFirstRun)
            return;

        _settings.IsFirstRun = false;

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        static TemplateEntry FolderEntry(string path) => new()
        {
            Type = EntryType.Folder,
            Value = path,
            Label = Path.GetFileName(path.TrimEnd('\\', '/'))
        };

        var defaults = new List<SearchTemplate>
        {
            new()
            {
                Name = "Default Search",
                Shortcut = "ds",
                Description = "Downloads, Desktop, Documents, Pictures, Music, Videos",
                Entries = new List<TemplateEntry>
                {
                    FolderEntry(Path.Combine(userProfile, "Downloads")),
                    FolderEntry(Path.Combine(userProfile, "Desktop")),
                    FolderEntry(Path.Combine(userProfile, "Documents")),
                    FolderEntry(Path.Combine(userProfile, "Pictures")),
                    FolderEntry(Path.Combine(userProfile, "Music")),
                    FolderEntry(Path.Combine(userProfile, "Videos"))
                }
            },
            new()
            {
                Name = "Downloads",
                Shortcut = "dl",
                Description = "Downloads folder",
                Entries = new List<TemplateEntry> { FolderEntry(Path.Combine(userProfile, "Downloads")) }
            },
            new()
            {
                Name = "Documents",
                Shortcut = "doc",
                Description = "Documents folder",
                Entries = new List<TemplateEntry> { FolderEntry(Path.Combine(userProfile, "Documents")) }
            },
            new()
            {
                Name = "Pictures",
                Shortcut = "pic",
                Description = "Pictures folder",
                Entries = new List<TemplateEntry> { FolderEntry(Path.Combine(userProfile, "Pictures")) }
            },
            new()
            {
                Name = "Desktop",
                Shortcut = "desk",
                Description = "Desktop folder",
                Entries = new List<TemplateEntry> { FolderEntry(Path.Combine(userProfile, "Desktop")) }
            }
        };

        foreach (var template in defaults)
        {
            _settings.Templates.Add(template);
        }

        _api.SavePluginSettings();
    }
}
