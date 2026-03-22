using Flow.Launcher.Plugin.SearchZones.Models;

namespace Flow.Launcher.Plugin.SearchZones.Services;

public class SearchService
{
    private readonly EverythingSearchProvider _everything;
    private readonly WindowsIndexSearchProvider _windowsIndex = new();

    public SearchService(PluginSettings settings)
    {
        _everything = new EverythingSearchProvider(settings);
    }

    /// <summary>True wenn Everything DLL gefunden – für Statusanzeige.</summary>
    public bool IsEverythingAvailable => _everything.IsAvailable;

    public async Task<List<SearchResult>> SearchAsync(
        string query,
        List<string> folders,
        List<string> excludePatterns,
        List<string> fileTypeFilter,
        bool searchSubdirectories,
        CancellationToken token)
    {
        ISearchProvider provider = _everything.IsServiceRunning
            ? _everything
            : _windowsIndex;

        return await provider.SearchAsync(query, folders, excludePatterns, fileTypeFilter, searchSubdirectories, token);
    }
}
