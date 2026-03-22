namespace Flow.Launcher.Plugin.SearchZones.Models;

public class PluginSettings
{
    public List<SearchTemplate> Templates { get; set; } = new();
    public bool IsFirstRun { get; set; } = true;
    /// <summary>Leer = automatische Erkennung. Manuell setzen via: sz settings everything &lt;pfad&gt;</summary>
    public string EverythingDllPath { get; set; } = string.Empty;
}
