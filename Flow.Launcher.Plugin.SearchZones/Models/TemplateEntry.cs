using System.Text.Json.Serialization;

namespace Flow.Launcher.Plugin.SearchZones.Models;

public enum EntryType
{
    Folder,
    PluginCommand,
    QuickCommand
}

public class TemplateEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public EntryType Type { get; set; }

    /// <summary>
    /// For Folder: a directory path (may contain %env% variables).
    /// For PluginCommand: a single action keyword (e.g. "calc") or a program path.
    /// For QuickCommand: a URL (https://…) or a shell command string.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Optional display label shown in Flow Launcher results.
    /// Falls back to Value when empty.
    /// </summary>
    public string Label { get; set; } = string.Empty;

    [JsonIgnore]
    public string DisplayLabel => string.IsNullOrWhiteSpace(Label) ? Value : Label;

    /// <summary>
    /// For QuickCommand: true when Value starts with http:// or https:// (or any ://), treated as a URL.
    /// For PluginCommand: true when Value is a single word with no path separators (FL action keyword).
    /// </summary>
    [JsonIgnore]
    public bool IsUrl =>
        Type == EntryType.QuickCommand &&
        (Value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
         Value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
         Value.Contains("://"));

    [JsonIgnore]
    public bool IsFlKeyword =>
        Type == EntryType.PluginCommand &&
        !string.IsNullOrWhiteSpace(Value) &&
        !Value.Contains(' ') &&
        !Value.Contains('\\') &&
        !Value.Contains('/');
}
