using System.Text.Json.Serialization;

namespace Flow.Launcher.Plugin.SearchZones.Models;

public class SearchTemplate
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Shortcut { get; set; } = string.Empty;

    /// <summary>
    /// Legacy field retained only for JSON migration. Populated from old settings files, 
    /// then converted to Entries by TemplateManager.MigrateTemplates() and cleared.
    /// </summary>
    public List<string> IncludeFolders { get; set; } = new();

    public List<string> ExcludePatterns { get; set; } = new();
    public List<string> FileTypeFilter { get; set; } = new();
    public bool SearchSubdirectories { get; set; } = true;
    public string Description { get; set; } = string.Empty;

    public List<TemplateEntry> Entries { get; set; } = new();

    // ── Convenience views ────────────────────────────────────────────────────

    [JsonIgnore]
    public IEnumerable<TemplateEntry> FolderEntries =>
        Entries.Where(e => e.Type == EntryType.Folder);

    [JsonIgnore]
    public IEnumerable<TemplateEntry> PluginCommandEntries =>
        Entries.Where(e => e.Type == EntryType.PluginCommand);

    [JsonIgnore]
    public IEnumerable<TemplateEntry> QuickCommandEntries =>
        Entries.Where(e => e.Type == EntryType.QuickCommand);

    /// <summary>
    /// Short summary for display in the settings header and FL results subtitle.
    /// </summary>
    [JsonIgnore]
    public string EntriesSummary
    {
        get
        {
            var parts = new List<string>();
            var folders = Entries.Count(e => e.Type == EntryType.Folder);
            var plugins = Entries.Count(e => e.Type == EntryType.PluginCommand);
            var quick = Entries.Count(e => e.Type == EntryType.QuickCommand);
            if (folders > 0) parts.Add($"{folders} folder{(folders == 1 ? "" : "s")}");
            if (plugins > 0) parts.Add($"{plugins} command{(plugins == 1 ? "" : "s")}");
            if (quick > 0) parts.Add($"{quick} quick");
            return parts.Count > 0 ? string.Join(", ", parts) : "(keine Einträge)";
        }
    }

    /// <summary>Kept for backwards-compat display; use EntriesSummary for new code.</summary>
    [JsonIgnore]
    public string FoldersSummary => EntriesSummary;

    public List<string> GetExpandedFolders()
    {
        return FolderEntries
            .Select(e => Environment.ExpandEnvironmentVariables(e.Value))
            .Where(Directory.Exists)
            .ToList();
    }
}
