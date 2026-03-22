using System.Diagnostics;
using Flow.Launcher.Plugin.SearchZones.Models;
using Flow.Launcher.Plugin.SearchZones.Services;

namespace Flow.Launcher.Plugin.SearchZones;

public class SearchZones : IAsyncPlugin, IContextMenu, ISettingProvider
{
    private PluginInitContext _context = null!;
    private PluginSettings _settings = null!;
    private TemplateManager _templateManager = null!;
    private SearchService _searchService = null!;

    private const string ManagementKeyword = "sz";
    private const string IconPath = "Images/icon.png";

    public async Task InitAsync(PluginInitContext context)
    {
        _context = context;
        _settings = context.API.LoadSettingJsonStorage<PluginSettings>();
        _searchService = new SearchService(_settings);
        _templateManager = new TemplateManager(_settings, context.API, context.CurrentPluginMetadata.ID);

        _templateManager.MigrateTemplates();
        _templateManager.SeedDefaults();
        _templateManager.RegisterAllKeywords();

        await Task.CompletedTask;
    }

    public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
    {
        if (!query.ActionKeyword.Equals(ManagementKeyword, StringComparison.OrdinalIgnoreCase))
            return new List<Result>();

        var template = _templateManager.GetTemplateByShortcut(query.FirstSearch);
        if (template != null)
        {
            return await HandleSearchQuery(template, query.SecondToEndSearch, token);
        }

        return HandleManagementQuery(query);
    }

    #region Search Handling

    private async Task<List<Result>> HandleSearchQuery(SearchTemplate template, string searchTerm, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            var preview = new List<Result>();
            var shortcutAutoComplete = $"{ManagementKeyword} {template.Shortcut} ";

            // Show quick commands first (highest score)
            foreach (var entry in template.QuickCommandEntries)
            {
                var e = entry;
                preview.Add(new Result
                {
                    Title = entry.DisplayLabel,
                    SubTitle = entry.IsUrl
                        ? $"URL öffnen: {entry.Value}"
                        : $"Befehl ausführen: {entry.Value}",
                    IcoPath = IconPath,
                    Score = 600,
                    AutoCompleteText = shortcutAutoComplete,
                    Action = _ =>
                    {
                        try
                        {
                            if (e.IsUrl)
                                Process.Start(new ProcessStartInfo(e.Value) { UseShellExecute = true });
                            else
                                Process.Start(new ProcessStartInfo("cmd.exe", "/c " + e.Value) { UseShellExecute = true });
                        }
                        catch { _context.API.ShowMsgError("Fehler", $"Konnte '{e.Value}' nicht ausführen."); }
                        return true;
                    }
                });
            }

            // Show plugin commands
            foreach (var entry in template.PluginCommandEntries)
            {
                var e = entry;
                preview.Add(new Result
                {
                    Title = entry.DisplayLabel,
                    SubTitle = entry.IsFlKeyword
                        ? $"Flow Launcher Aktion: {entry.Value}"
                        : $"Programm starten: {entry.Value}",
                    IcoPath = entry.IsFlKeyword ? IconPath : (File.Exists(entry.Value) ? entry.Value : IconPath),
                    Score = 300,
                    AutoCompleteText = shortcutAutoComplete,
                    Action = _ =>
                    {
                        try
                        {
                            if (e.IsFlKeyword)
                                _context.API.ChangeQuery(e.Value + " ");
                            else
                                Process.Start(new ProcessStartInfo(e.Value) { UseShellExecute = true });
                        }
                        catch { _context.API.ShowMsgError("Fehler", $"Konnte '{e.Value}' nicht starten."); }
                        return e.IsFlKeyword ? false : true;
                    }
                });
            }

            // Show folders as a single hint (typing a search term will search them)
            if (template.FolderEntries.Any())
            {
                preview.Add(new Result
                {
                    Title = $"📁 {template.FolderEntries.Count()} Ordner durchsuchen",
                    SubTitle = "Suchbegriff eingeben um Dateien zu finden",
                    IcoPath = IconPath,
                    Score = 50,
                    AutoCompleteText = $"{ManagementKeyword} {template.Shortcut} "
                });
            }

            if (preview.Count == 0)
            {
                preview.Add(new Result
                {
                    Title = $"Suche in: {template.Name}",
                    SubTitle = "Keine Einträge konfiguriert",
                    IcoPath = IconPath,
                    AutoCompleteText = $"{ManagementKeyword} {template.Shortcut} "
                });
            }

            return preview;
        }

        var results = new List<Result>();

        // ── Folder entries → file search ──────────────────────────────────────
        var folders = template.GetExpandedFolders();
        if (folders.Count > 0)
        {
            var searchResults = await _searchService.SearchAsync(
                searchTerm, folders, template.ExcludePatterns,
                template.FileTypeFilter, template.SearchSubdirectories, token);

            results.AddRange(searchResults.Select(r => new Result
            {
                Title = r.FileName,
                SubTitle = r.FullPath,
                IcoPath = r.FullPath,
                ContextData = r.FullPath,
                Score = 500,
                Action = _ =>
                {
                    try
                    {
                        if (r.IsFolder)
                            Process.Start("explorer.exe", $"\"{r.FullPath}\"");
                        else
                            Process.Start(new ProcessStartInfo(r.FullPath) { UseShellExecute = true });
                    }
                    catch
                    {
                        _context.API.ShowMsgError("Fehler", $"Konnte '{r.FullPath}' nicht öffnen.");
                    }
                    return true;
                }
            }));
        }

        // ── Plugin command entries ────────────────────────────────────────────
        foreach (var entry in template.PluginCommandEntries)
        {
            if (!string.IsNullOrWhiteSpace(searchTerm) &&
                !entry.DisplayLabel.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) &&
                !entry.Value.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                continue;

            var e = entry; // capture
            results.Add(new Result
            {
                Title = entry.DisplayLabel,
                SubTitle = entry.IsFlKeyword
                    ? $"Flow Launcher Aktion: {entry.Value}"
                    : $"Programm starten: {entry.Value}",
                IcoPath = entry.IsFlKeyword ? IconPath : (File.Exists(entry.Value) ? entry.Value : IconPath),
                Score = 300,
                Action = _ =>
                {
                    try
                    {
                        if (e.IsFlKeyword)
                            _context.API.ChangeQuery(e.Value + " ");
                        else
                            Process.Start(new ProcessStartInfo(e.Value) { UseShellExecute = true });
                    }
                    catch
                    {
                        _context.API.ShowMsgError("Fehler", $"Konnte '{e.Value}' nicht starten.");
                    }
                    return e.IsFlKeyword ? false : true;
                }
            });
        }

        // ── Quick command entries ─────────────────────────────────────────────
        foreach (var entry in template.QuickCommandEntries)
        {
            if (!string.IsNullOrWhiteSpace(searchTerm) &&
                !entry.DisplayLabel.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) &&
                !entry.Value.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                continue;

            var e = entry; // capture
            results.Add(new Result
            {
                Title = entry.DisplayLabel,
                SubTitle = entry.IsUrl
                    ? $"URL öffnen: {entry.Value}"
                    : $"Befehl ausführen: {entry.Value}",
                IcoPath = IconPath,
                Score = 600,
                AutoCompleteText = $"{ManagementKeyword} {template.Shortcut} ",
                Action = _ =>
                {
                    try
                    {
                        if (e.IsUrl)
                            Process.Start(new ProcessStartInfo(e.Value) { UseShellExecute = true });
                        else
                            Process.Start(new ProcessStartInfo("cmd.exe", "/c " + e.Value) { UseShellExecute = true });
                    }
                    catch
                    {
                        _context.API.ShowMsgError("Fehler", $"Konnte '{e.Value}' nicht ausführen.");
                    }
                    return true;
                }
            });
        }

        if (results.Count == 0)
        {
            if (template.Entries.Count == 0)
                return new List<Result> { new() { Title = "Keine Einträge konfiguriert", SubTitle = "Bitte bearbeite diesen Suchraum in den Einstellungen", IcoPath = IconPath } };

            return new List<Result> { new() { Title = $"Keine Treffer für '{searchTerm}'", SubTitle = template.EntriesSummary, IcoPath = IconPath } };
        }

        return results;
    }

    #endregion

    #region Management Commands

    private List<Result> HandleManagementQuery(Query query)
    {
        var search = query.FirstSearch;

        // var debugPath = Path.Combine(Path.GetTempPath(), "sz_debug.log");

        // var text = $"""
        //   ActionKeyword: "{query.ActionKeyword}",
        //   Search: "{query.Search}",
        //   SearchTermLength: {query.SearchTerms.Length},
        //   SearchTerms: ["{string.Join("\", \"", query.SearchTerms)}"],
        //   FirstSearch: "{query.FirstSearch}",
        //   SecondSearch: "{query.SecondSearch}",
        //   SecondToEndSearch: "{query.SecondToEndSearch}",
        //   ThirdSearch: "{query.ThirdSearch}"
        // """;

        // File.AppendAllText(debugPath, $"[{DateTime.Now:HH:mm:ss}] query={{\n{text}\n}}\n"); 

        if (string.IsNullOrEmpty(search))
            return ShowAllTemplates();

        switch (search.ToLowerInvariant())
        {
            case "add":
                return HandleAddCommand(query.SearchTerms[1..]);//first element is add
            case "del":
                return HandleDeleteCommand(query.SecondSearch);
            case "edit":
                return HandleEditCommand(query.SecondSearch);
            case "settings":
                return HandleSettingsCommand(query.SecondSearch);
            default:
                return ShowAllTemplates();
        }

    }

    private static int TemplateScore(SearchTemplate t, string? filter)
    {
        if (string.IsNullOrEmpty(filter)) return 500;
        if (t.Shortcut.Equals(filter, StringComparison.OrdinalIgnoreCase)) return 1000;
        if (t.Shortcut.StartsWith(filter, StringComparison.OrdinalIgnoreCase)) return 900;
        if (t.Name.StartsWith(filter, StringComparison.OrdinalIgnoreCase)) return 700;
        if (t.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)) return 600;
        return 500;
    }

    private List<Result> ShowAllTemplates(string? filter = null)
    {
        var templates = _templateManager.GetAllTemplates();

        if (!string.IsNullOrEmpty(filter))
        {
            templates = templates.Where(t =>
                t.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                t.Shortcut.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var results = templates.Select(t => new Result
        {
            Title = $"{t.Shortcut} — {t.Name}",
            SubTitle = $"{t.EntriesSummary}{(string.IsNullOrEmpty(t.Description) ? "" : " | " + t.Description)}",
            IcoPath = IconPath,
            AutoCompleteText = $"{ManagementKeyword} {t.Shortcut} ",
            Score = TemplateScore(t, filter),
            Action = _ =>
            {
                _context.API.ChangeQuery($"{ManagementKeyword} edit {t.Shortcut} ");
                return false;
            }
        }).ToList();

        // Add helper entries
        results.Insert(0, new Result
        {
            Title = "Neuen Suchraum erstellen",
            SubTitle = "sz add <shortcut> <name> <ordner1;ordner2;...>",
            IcoPath = IconPath,
            AutoCompleteText = $"{ManagementKeyword} add ",
            // Only show on top when there is no filter, otherwise sink below templates
            Score = string.IsNullOrEmpty(filter) ? 1000 : 1,
            Action = _ =>
            {
                _context.API.ChangeQuery($"{ManagementKeyword} add ");
                return false;
            }
        });

        if (!_searchService.IsEverythingAvailable)
        {
            results.Add(new Result
            {
                Title = "⚠ Everything nicht gefunden — Windows Index aktiv",
                SubTitle = string.IsNullOrEmpty(_settings.EverythingDllPath)
                    ? "Pfad konfigurieren: sz settings everything <pfad-zur-Everything64.dll>"
                    : $"Konfigurierter Pfad nicht gefunden: {_settings.EverythingDllPath}",
                IcoPath = IconPath,
                AutoCompleteText = $"{ManagementKeyword} settings everything ",
                Score = -1,
                Action = _ =>
                {
                    _context.API.ChangeQuery($"{ManagementKeyword} settings everything ");
                    return false;
                }
            });
        }
        else
        {
            results.Add(new Result
            {
                Title = "✓ Everything aktiv",
                SubTitle = string.IsNullOrEmpty(_settings.EverythingDllPath)
                    ? "Automatisch erkannt"
                    : $"Konfiguriert: {_settings.EverythingDllPath}",
                IcoPath = IconPath,
                Score = -1
            });
        }

        return results;
    }

    private List<Result> HandleSettingsCommand(string args)
    {
        if (args.StartsWith("everything", StringComparison.OrdinalIgnoreCase))
        {
            var path = args.Length > 10 ? args[10..].Trim() : string.Empty;

            if (string.IsNullOrEmpty(path))
            {
                return new List<Result>
                {
                    new()
                    {
                        Title = "Everything DLL Pfad konfigurieren",
                        SubTitle = string.IsNullOrEmpty(_settings.EverythingDllPath)
                            ? "Aktuell: automatische Erkennung"
                            : $"Aktuell: {_settings.EverythingDllPath}",
                        IcoPath = IconPath
                    },
                    new()
                    {
                        Title = "Pfad zurücksetzen (automatische Erkennung)",
                        SubTitle = "Entfernt den manuell konfigurierten Pfad",
                        IcoPath = IconPath,
                        Action = _ =>
                        {
                            _settings.EverythingDllPath = string.Empty;
                            _context.API.SavePluginSettings();
                            _searchService = new SearchService(_settings);
                            _context.API.ShowMsg("Einstellungen", "Everything-Pfad zurückgesetzt (automatische Erkennung).");
                            return true;
                        }
                    }
                };
            }

            // Show confirm result for the entered path
            var exists = File.Exists(path);
            return new List<Result>
            {
                new()
                {
                    Title = exists ? $"✓ Pfad setzen: {path}" : $"⚠ Datei nicht gefunden: {path}",
                    SubTitle = exists ? "Enter zum Speichern" : "Pfad prüfen und korrigieren",
                    IcoPath = IconPath,
                    Action = _ =>
                    {
                        if (!exists)
                        {
                            _context.API.ShowMsgError("Fehler", $"Datei nicht gefunden: {path}");
                            return false;
                        }
                        _settings.EverythingDllPath = path;
                        _context.API.SavePluginSettings();
                        _searchService = new SearchService(_settings);
                        _context.API.ShowMsg("Gespeichert", $"Everything-Pfad konfiguriert: {path}");
                        return true;
                    }
                }
            };
        }

        // Show all settings
        return new List<Result>
        {
            new()
            {
                Title = "Everything DLL Pfad",
                SubTitle = string.IsNullOrEmpty(_settings.EverythingDllPath)
                    ? "Automatische Erkennung aktiv — sz settings everything <pfad>"
                    : _settings.EverythingDllPath,
                IcoPath = IconPath,
                AutoCompleteText = $"{ManagementKeyword} settings everything ",
                Action = _ =>
                {
                    _context.API.ChangeQuery($"{ManagementKeyword} settings everything ");
                    return false;
                }
            }
        };
    }

    private List<Result> HandleAddCommand(string[] terms)
    {
        if (terms.Length == 0)
        {
            return new List<Result>
            {
                new()
                {
                    Title = "Suchraum hinzufügen",
                    SubTitle = "Format: sz add <shortcut> <name> <ordner1;ordner2;...>",
                    IcoPath = IconPath
                },
                new()
                {
                    Title = "Beispiel: sz add proj Projekte D:\\Projects;D:\\Work",
                    SubTitle = "Erstellt Suchraum 'Projekte' mit Shortcut 'proj'",
                    IcoPath = IconPath,
                    Action = _ =>
                    {
                        _context.API.ChangeQuery($"{ManagementKeyword} add ");
                        return false;
                    }
                }
            };
        }

        if (terms.Length < 3)
        {
            return new List<Result>
            {
                new()
                {
                    Title = "Unvollständig",
                    SubTitle = "Format: sz add <shortcut> <name> <ordner1;ordner2;...>",
                    IcoPath = IconPath
                }
            };
        }

        var shortcut = terms[0];
        var name = terms[1];
        var foldersRaw = terms[2];
        var folderPaths = foldersRaw.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(f => f.Trim())
            .ToList();

        var template = new SearchTemplate
        {
            Name = name,
            Shortcut = shortcut,
            Entries = folderPaths.Select(f => new Models.TemplateEntry
            {
                Type = Models.EntryType.Folder,
                Value = Environment.ExpandEnvironmentVariables(f),
                Label = Path.GetFileName(f.TrimEnd('\\', '/'))
            }).ToList(),
            Description = $"{name} ({string.Join(", ", folderPaths.Select(f => Path.GetFileName(f.TrimEnd('\\'))))})"
        };

        return new List<Result>
        {
            new()
            {
                Title = $"Suchraum '{name}' mit Shortcut '{shortcut}' erstellen",
                SubTitle = $"Ordner: {string.Join(", ", folderPaths)}",
                IcoPath = IconPath,
                Action = _ =>
                {
                    if (_templateManager.AddTemplate(template, out var error))
                    {
                        _context.API.ShowMsg("Suchraum erstellt", $"Verwende '{shortcut} <suchbegriff>' zum Suchen.");
                    }
                    else
                    {
                        _context.API.ShowMsgError("Fehler", error);
                    }
                    return true;
                }
            }
        };
    }

    private List<Result> HandleDeleteCommand(string nameOrShortcut)
    {
        var templates = _templateManager.GetAllTemplates()
            .Where(t => t.Name.Contains(nameOrShortcut, StringComparison.OrdinalIgnoreCase) ||
                        t.Shortcut.Contains(nameOrShortcut, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (templates.Count == 0)
        {
            return new List<Result>
            {
                new()
                {
                    Title = "Kein Suchraum gefunden",
                    SubTitle = $"Kein Suchraum mit '{nameOrShortcut}' gefunden",
                    IcoPath = IconPath
                }
            };
        }

        return templates.Select(t => new Result
        {
            Title = $"'{t.Name}' ({t.Shortcut}) löschen?",
            SubTitle = "Enter zum Bestätigen",
            IcoPath = IconPath,
            Action = _ =>
            {
                _templateManager.DeleteTemplate(t.Id);
                _context.API.ShowMsg("Gelöscht", $"Suchraum '{t.Name}' wurde entfernt.");
                return true;
            }
        }).ToList();
    }

    private List<Result> HandleEditCommand(string nameOrShortcut)
    {
        var parts = nameOrShortcut.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return _templateManager.GetAllTemplates().Select(t => new Result
            {
                Title = $"{t.Shortcut} — {t.Name}",
                SubTitle = "Klicke zum Bearbeiten",
                IcoPath = IconPath,
                Action = _ =>
                {
                    _context.API.ChangeQuery($"{ManagementKeyword} edit {t.Shortcut} ");
                    return false;
                }
            }).ToList();
        }

        var template = _templateManager.GetTemplateByShortcut(parts[0])
            ?? _templateManager.GetAllTemplates().FirstOrDefault(
                t => t.Name.Equals(parts[0], StringComparison.OrdinalIgnoreCase));

        if (template == null)
        {
            return new List<Result>
            {
                new()
                {
                    Title = "Suchraum nicht gefunden",
                    SubTitle = $"Kein Suchraum '{parts[0]}' gefunden",
                    IcoPath = IconPath
                }
            };
        }

        // If a field name (and optional new value) follow the shortcut, handle inline editing
        if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[1]))
        {
            var fieldInput = parts[1];
            var spaceInField = fieldInput.IndexOf(' ');
            var field = (spaceInField >= 0 ? fieldInput[..spaceInField] : fieldInput).ToLowerInvariant();
            var newValue = spaceInField >= 0 ? fieldInput[(spaceInField + 1)..].Trim() : string.Empty;

            if (!string.IsNullOrEmpty(newValue))
            {
                return field switch
                {
                    "name" => ConfirmEditResult(
                        template,
                        $"Name → '{newValue}'",
                        t => t.Name = newValue),
                    "shortcut" => ConfirmEditResult(
                        template,
                        $"Shortcut → '{newValue.ToLowerInvariant()}'",
                        t => t.Shortcut = newValue.ToLowerInvariant()),
                    "folders" => ConfirmEditResult(
                        template,
                        $"Ordner → {newValue}",
                        t =>
                        {
                            t.Entries.RemoveAll(e => e.Type == Models.EntryType.Folder);
                            t.Entries.AddRange(newValue
                                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                                .Select(f => new Models.TemplateEntry
                                {
                                    Type = Models.EntryType.Folder,
                                    Value = f.Trim(),
                                    Label = Path.GetFileName(f.Trim().TrimEnd('\\', '/'))
                                }));
                        }),
                    "exclude" => ConfirmEditResult(
                        template,
                        $"Ausschlüsse → {newValue}",
                        t => t.ExcludePatterns = newValue
                            .Split(';', StringSplitOptions.RemoveEmptyEntries)
                            .Select(f => f.Trim()).ToList()),
                    "types" => ConfirmEditResult(
                        template,
                        $"Dateitypen → {newValue}",
                        t => t.FileTypeFilter = newValue
                            .Split(';', StringSplitOptions.RemoveEmptyEntries)
                            .Select(f => f.Trim()).ToList()),
                    _ => ShowEditFields(template)
                };
            }
        }

        return ShowEditFields(template);
    }

    private List<Result> ConfirmEditResult(SearchTemplate template, string description, Action<SearchTemplate> update)
    {
        return new List<Result>
        {
            new()
            {
                Title = $"Speichern: {description}",
                SubTitle = "Enter zum Bestätigen",
                IcoPath = IconPath,
                Action = _ =>
                {
                    if (_templateManager.EditTemplate(template.Id, update, out var error))
                        _context.API.ShowMsg("Gespeichert", description);
                    else
                        _context.API.ShowMsgError("Fehler", error);
                    return true;
                }
            }
        };
    }

    private List<Result> ShowEditFields(SearchTemplate template)
    {
        return new List<Result>
        {
            new()
            {
                Title = $"Name: {template.Name}",
                SubTitle = "sz edit <shortcut> name <neuer-name>",
                IcoPath = IconPath,
                AutoCompleteText = $"{ManagementKeyword} edit {template.Shortcut} name ",
                Action = _ =>
                {
                    _context.API.ChangeQuery($"{ManagementKeyword} edit {template.Shortcut} name ");
                    return false;
                }
            },
            new()
            {
                Title = $"Shortcut: {template.Shortcut}",
                SubTitle = "sz edit <shortcut> shortcut <neues-kürzel>",
                IcoPath = IconPath,
                AutoCompleteText = $"{ManagementKeyword} edit {template.Shortcut} shortcut ",
                Action = _ =>
                {
                    _context.API.ChangeQuery($"{ManagementKeyword} edit {template.Shortcut} shortcut ");
                    return false;
                }
            },
            new()
            {
                Title = $"Ordner: {string.Join("; ", template.FolderEntries.Select(e => e.Value))}",
                SubTitle = "sz edit <shortcut> folders <ordner1;ordner2;...>",
                IcoPath = IconPath,
                AutoCompleteText = $"{ManagementKeyword} edit {template.Shortcut} folders ",
                Action = _ =>
                {
                    _context.API.ChangeQuery($"{ManagementKeyword} edit {template.Shortcut} folders ");
                    return false;
                }
            },
            new()
            {
                Title = $"Ausschlüsse: {(template.ExcludePatterns.Count > 0 ? string.Join("; ", template.ExcludePatterns) : "keine")}",
                SubTitle = "sz edit <shortcut> exclude <muster1;muster2;...>",
                IcoPath = IconPath,
                AutoCompleteText = $"{ManagementKeyword} edit {template.Shortcut} exclude ",
                Action = _ =>
                {
                    _context.API.ChangeQuery($"{ManagementKeyword} edit {template.Shortcut} exclude ");
                    return false;
                }
            },
            new()
            {
                Title = $"Dateitypen: {(template.FileTypeFilter.Count > 0 ? string.Join("; ", template.FileTypeFilter) : "alle")}",
                SubTitle = "sz edit <shortcut> types <*.pdf;*.docx;...>",
                IcoPath = IconPath,
                AutoCompleteText = $"{ManagementKeyword} edit {template.Shortcut} types ",
                Action = _ =>
                {
                    _context.API.ChangeQuery($"{ManagementKeyword} edit {template.Shortcut} types ");
                    return false;
                }
            }
        };
    }

    #endregion

    #region Context Menu

    public List<Result> LoadContextMenus(Result selectedResult)
    {
        if (selectedResult.ContextData is not string fullPath)
            return new List<Result>();

        var isFolder = Directory.Exists(fullPath);

        var results = new List<Result>
        {
            new()
            {
                Title = isFolder ? "Ordner öffnen" : "Datei öffnen",
                IcoPath = IconPath,
                Action = _ =>
                {
                    Process.Start(new ProcessStartInfo(fullPath) { UseShellExecute = true });
                    return true;
                }
            },
            new()
            {
                Title = "Übergeordneten Ordner öffnen",
                IcoPath = IconPath,
                Action = _ =>
                {
                    var dir = Path.GetDirectoryName(fullPath);
                    if (dir != null)
                        Process.Start("explorer.exe", $"/select,\"{fullPath}\"");
                    return true;
                }
            },
            new()
            {
                Title = "Pfad kopieren",
                IcoPath = IconPath,
                Action = _ =>
                {
                    _context.API.CopyToClipboard(fullPath);
                    return true;
                }
            },
            new()
            {
                Title = "Dateiname kopieren",
                IcoPath = IconPath,
                Action = _ =>
                {
                    _context.API.CopyToClipboard(Path.GetFileName(fullPath));
                    return true;
                }
            }
        };

        return results;
    }

    #endregion

    public System.Windows.Controls.Control CreateSettingPanel()
    {
        var items = BuildPluginCommandItems();
        return new UI.SettingPanel(_settings, _templateManager, () =>
        {
            _context.API.SavePluginSettings();
            _searchService = new SearchService(_settings);
        }, items);
    }

    private List<UI.PluginCommandItem> BuildPluginCommandItems()
    {
        var items = new List<UI.PluginCommandItem>();

        // FL action keywords
        try
        {
            var keywords = _context.API.GetAllPlugins()
                .SelectMany(p =>
                {
                    var kws = p.Metadata.ActionKeywords is { Count: > 0 }
                        ? p.Metadata.ActionKeywords
                        : new List<string> { p.Metadata.ActionKeyword };
                    return kws
                        .Where(kw => !string.IsNullOrEmpty(kw) && kw != "*")
                        .Select(kw => new UI.PluginCommandItem(kw, p.Metadata.Name, "FL Keyword"));
                })
                .OrderBy(k => k.Value);
            items.AddRange(keywords);
        }
        catch { /* FL API unavailable */ }

        // Installed programs from Start Menu shortcuts
        try
        {
            var startMenuPaths = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu)
            };

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var root in startMenuPaths)
            {
                if (!Directory.Exists(root)) continue;
                foreach (var lnk in Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories))
                {
                    var name = Path.GetFileNameWithoutExtension(lnk);
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    // Skip duplicates and uninstallers
                    if (!seen.Add(name)) continue;
                    if (name.Contains("uninstall", StringComparison.OrdinalIgnoreCase)) continue;

                    items.Add(new UI.PluginCommandItem(lnk, name, "Program"));
                }
            }
        }
        catch { /* Start Menu scan failed */ }

        return items;
    }

}