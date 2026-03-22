using System.Data;
using System.Data.OleDb;

namespace Flow.Launcher.Plugin.SearchZones.Services;

public class WindowsIndexSearchProvider : ISearchProvider
{
    private const string ConnectionString = "Provider=Search.CollatorDSO;Extended Properties='Application=Windows'";

    public bool IsAvailable
    {
        get
        {
            try
            {
                using var conn = new OleDbConnection(ConnectionString);
                conn.Open();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public async Task<List<SearchResult>> SearchAsync(
        string query,
        List<string> folders,
        List<string> excludePatterns,
        List<string> fileTypeFilter,
        bool searchSubdirectories,
        CancellationToken token)
    {
        var results = new List<SearchResult>();
        if (folders.Count == 0 || string.IsNullOrWhiteSpace(query))
            return results;

        var sql = BuildSql(query, folders, fileTypeFilter, searchSubdirectories);

        try
        {
            using var conn = new OleDbConnection(ConnectionString);
            await conn.OpenAsync(token);

            using var cmd = new OleDbCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync(token) as OleDbDataReader;

            if (reader == null)
                return results;

            while (await reader.ReadAsync(token) && results.Count < 200)
            {
                var fullPath = reader.GetString(0);

                if (IsExcluded(fullPath, excludePatterns))
                    continue;

                var fileName = Path.GetFileName(fullPath);
                var isFolder = reader.GetString(1) == "Directory";
                results.Add(new SearchResult(fullPath, fileName, isFolder));
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Windows Index not available or query failed
        }

        return results;
    }

    private static string BuildSql(string query, List<string> folders, List<string> fileTypeFilter, bool searchSubdirectories)
    {
        var whereClauses = new List<string>();

        // Folder scope
        var folderConditions = folders.Select(f =>
            searchSubdirectories
                ? $"SCOPE = 'file:{EscapeSql(f)}'"
                : $"DIRECTORY = 'file:{EscapeSql(f)}'");
        whereClauses.Add($"({string.Join(" OR ", folderConditions)})");

        // Search term - use CONTAINS for full-text or LIKE for simple matching
        var escapedQuery = EscapeSql(query);
        whereClauses.Add($"System.FileName LIKE '%{escapedQuery}%'");

        // File type filter
        if (fileTypeFilter.Count > 0)
        {
            var extConditions = fileTypeFilter.Select(f =>
            {
                var ext = f.TrimStart('*');
                return $"System.FileName LIKE '%{EscapeSql(ext)}'";
            });
            whereClauses.Add($"({string.Join(" OR ", extConditions)})");
        }

        return $"SELECT System.ItemPathDisplay, System.ItemType FROM SystemIndex WHERE {string.Join(" AND ", whereClauses)}";
    }

    private static string EscapeSql(string value)
    {
        return value.Replace("'", "''");
    }

    private static bool IsExcluded(string path, List<string> excludePatterns)
    {
        foreach (var pattern in excludePatterns)
        {
            if (path.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
