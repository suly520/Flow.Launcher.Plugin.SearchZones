namespace Flow.Launcher.Plugin.SearchZones.Services;

public record SearchResult(string FullPath, string FileName, bool IsFolder);

public interface ISearchProvider
{
    bool IsAvailable { get; }
    Task<List<SearchResult>> SearchAsync(
        string query,
        List<string> folders,
        List<string> excludePatterns,
        List<string> fileTypeFilter,
        bool searchSubdirectories,
        CancellationToken token);
}
