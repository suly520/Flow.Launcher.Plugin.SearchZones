using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Flow.Launcher.Plugin.SearchZones.Models;
using Microsoft.Win32;

namespace Flow.Launcher.Plugin.SearchZones.Services;

public class EverythingSearchProvider : ISearchProvider
{
    private const int EVERYTHING_OK = 0;
    private const int EVERYTHING_REQUEST_FILE_NAME = 0x00000001;
    private const int EVERYTHING_REQUEST_PATH = 0x00000002;

    private readonly PluginSettings _settings;
    private bool _dllLoaded;

    public EverythingSearchProvider(PluginSettings settings)
    {
        _settings = settings;
    }

    private bool EnsureDllLoaded()
    {
        if (_dllLoaded) return true;

        var candidates = new List<string>();

        // 1. User-configured path (highest priority)
        if (!string.IsNullOrWhiteSpace(_settings.EverythingDllPath))
            candidates.Add(_settings.EverythingDllPath);

        // 2. Common install locations
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Everything", "Everything64.dll"));
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Everything", "Everything64.dll"));

        // 3. Registry lookup
        foreach (var keyPath in new[]
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Everything",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Everything"
        })
        {
            using var key = Registry.LocalMachine.OpenSubKey(keyPath);
            if (key?.GetValue("InstallLocation") is string installDir)
                candidates.Add(Path.Combine(installDir, "Everything64.dll"));
        }

        // 4. Check running Everything process executable location
        foreach (var proc in Process.GetProcessesByName("Everything").Concat(Process.GetProcessesByName("Everything64")))
        {
            try
            {
                var dir = Path.GetDirectoryName(proc.MainModule?.FileName);
                if (dir != null)
                    candidates.Add(Path.Combine(dir, "Everything64.dll"));
            }
            catch { }
        }

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                SetDllDirectory(Path.GetDirectoryName(path));
                _dllLoaded = true;
                return true;
            }
        }

        return false;
    }

    /// <summary>True wenn Everything-Prozess läuft (für Statusanzeige).</summary>
    public bool IsAvailable =>
        Process.GetProcessesByName("Everything").Length > 0 ||
        Process.GetProcessesByName("Everything64").Length > 0;

    /// <summary>True wenn DLL geladen und Dienst antwortet (für tatsächliche Suche).</summary>
    public bool IsServiceRunning
    {
        get
        {
            try
            {
                if (!EnsureDllLoaded()) return false;
                return Everything_GetMajorVersion() > 0;
            }
            catch
            {
                return false;
            }
        }
    }

    public Task<List<SearchResult>> SearchAsync(
        string query,
        List<string> folders,
        List<string> excludePatterns,
        List<string> fileTypeFilter,
        bool searchSubdirectories,
        CancellationToken token)
    {
        var results = new List<SearchResult>();
        if (folders.Count == 0 || string.IsNullOrWhiteSpace(query))
            return Task.FromResult(results);

        var searchQuery = BuildQuery(query, folders, excludePatterns, fileTypeFilter);

        Everything_SetSearchW(searchQuery);
        Everything_SetRequestFlags(EVERYTHING_REQUEST_FILE_NAME | EVERYTHING_REQUEST_PATH);
        Everything_SetMax(200);
        Everything_QueryW(true);

        token.ThrowIfCancellationRequested();

        var errorCode = Everything_GetLastError();
        if (errorCode != EVERYTHING_OK)
            return Task.FromResult(results);

        var count = Everything_GetNumResults();
        var pathBuffer = new StringBuilder(260);
        var nameBuffer = new StringBuilder(260);

        for (uint i = 0; i < count; i++)
        {
            token.ThrowIfCancellationRequested();

            Everything_GetResultFullPathNameW(i, pathBuffer, 260);
            var fullPath = pathBuffer.ToString();

            if (IsExcluded(fullPath, excludePatterns))
                continue;

            var fileName = Path.GetFileName(fullPath);
            var isFolder = Directory.Exists(fullPath);
            results.Add(new SearchResult(fullPath, fileName, isFolder));
        }

        return Task.FromResult(results);
    }

    private static string BuildQuery(string query, List<string> folders, List<string> excludePatterns, List<string> fileTypeFilter)
    {
        var sb = new StringBuilder();

        // Folder scoping
        if (folders.Count == 1)
        {
            sb.Append($"\"{folders[0]}\" ");
        }
        else
        {
            sb.Append('<');
            sb.Append(string.Join('|', folders.Select(f => $"\"{f}\"")));
            sb.Append("> ");
        }

        // File type filter
        if (fileTypeFilter.Count > 0)
        {
            var extParts = fileTypeFilter.Select(f => f.TrimStart('*'));
            sb.Append($"ext:{string.Join(';', extParts)} ");
        }

        sb.Append(query);

        return sb.ToString();
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

    #region P/Invoke

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetDllDirectory(string? lpPathName);

    [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
    private static extern uint Everything_SetSearchW(string lpSearchString);

    [DllImport("Everything64.dll")]
    private static extern void Everything_SetRequestFlags(uint dwRequestFlags);

    [DllImport("Everything64.dll")]
    private static extern void Everything_SetMax(uint dwMax);

    [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
    private static extern bool Everything_QueryW(bool bWait);

    [DllImport("Everything64.dll")]
    private static extern uint Everything_GetLastError();

    [DllImport("Everything64.dll")]
    private static extern uint Everything_GetNumResults();

    [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
    private static extern void Everything_GetResultFullPathNameW(uint nIndex, StringBuilder lpString, uint nMaxCount);

    [DllImport("Everything64.dll")]
    private static extern uint Everything_GetMajorVersion();

    #endregion
}
