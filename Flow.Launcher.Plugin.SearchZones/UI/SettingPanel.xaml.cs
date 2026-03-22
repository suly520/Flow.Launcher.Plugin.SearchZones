using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Flow.Launcher.Plugin.SearchZones.Models;
using Flow.Launcher.Plugin.SearchZones.Services;
using Microsoft.Win32;

namespace Flow.Launcher.Plugin.SearchZones.UI;

/// <summary>An item for the Plugin Command dropdown — either an FL keyword or an installed program.</summary>
public record PluginCommandItem(string Value, string DisplayName, string Category)
{
    public override string ToString() => DisplayName;
}

public partial class SettingPanel : UserControl
{
    private readonly PluginSettings _settings;
    private readonly TemplateManager _templateManager;
    private readonly Action _onSave;
    private readonly ObservableCollection<SearchTemplate> _templateItems;

    /// <summary>All plugin command items (FL keywords + installed programs) for the ComboBox dropdown.</summary>
    public IReadOnlyList<PluginCommandItem> PluginCommandItems { get; }

    /// <summary>Filtered view of PluginCommandItems, shared across all ComboBoxes.</summary>
    private readonly ICollectionView _pluginCommandView;

    public SettingPanel(PluginSettings settings, TemplateManager templateManager, Action onSave,
        IReadOnlyList<PluginCommandItem> pluginCommandItems)
    {
        _settings = settings;
        _templateManager = templateManager;
        _onSave = onSave;
        PluginCommandItems = pluginCommandItems;

        _pluginCommandView = CollectionViewSource.GetDefaultView(PluginCommandItems);
        _pluginCommandView.Filter = PluginCommandFilter;

        _templateItems = new ObservableCollection<SearchTemplate>(_settings.Templates);

        InitializeComponent();

        // Expose filtered view so deeply-nested DataTemplate ComboBoxes can reach it via DynamicResource
        Resources["PluginCommandItems"] = _pluginCommandView;

        TemplatesList.ItemsSource = _templateItems;
        EverythingPathBox.Text = _settings.EverythingDllPath;
        UpdateStatus(_settings.EverythingDllPath);
    }

    // ── Scroll fix ────────────────────────────────────────────────────────────

    private void Content_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        // If any ComboBox dropdown is open, don't intercept — let the dropdown scroll
        if (_hookedComboBoxes.Any(cb => cb.IsDropDownOpen)) return;

        // Otherwise bubble the scroll to FL's host ScrollViewer
        var parent = VisualTreeHelper.GetParent(this) as DependencyObject;
        while (parent != null && parent is not ScrollViewer)
            parent = VisualTreeHelper.GetParent(parent);

        if (parent is ScrollViewer sv)
        {
            sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta);
            e.Handled = true;
        }
    }

    private string _pluginCommandFilterText = string.Empty;
    private PluginCommandItem? _lastSelectedPluginItem;

    private bool PluginCommandFilter(object obj)
    {
        if (string.IsNullOrWhiteSpace(_pluginCommandFilterText)) return true;
        if (obj is not PluginCommandItem item) return false;
        return item.DisplayName.Contains(_pluginCommandFilterText, StringComparison.OrdinalIgnoreCase)
            || item.Value.Contains(_pluginCommandFilterText, StringComparison.OrdinalIgnoreCase);
    }

    private void PluginComboBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not ComboBox cb) return;
        cb.IsDropDownOpen = true;

        // Attach filter handler to the internal editable TextBox (once)
        var editBox = cb.Template.FindName("PART_EditableTextBox", cb) as TextBox;
        if (editBox != null && !_hookedComboBoxes.Contains(cb))
        {
            _hookedComboBoxes.Add(cb);
            editBox.TextChanged += PluginComboBox_TextChanged;
            cb.SelectionChanged += PluginComboBox_SelectionChanged;
        }
    }

    private void PluginComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.SelectedItem is PluginCommandItem item)
            _lastSelectedPluginItem = item;
    }

    private readonly HashSet<ComboBox> _hookedComboBoxes = new();

    private void PluginComboBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox tb) return;
        _pluginCommandFilterText = tb.Text.Trim();
        _pluginCommandView.Refresh();

        // Find the parent ComboBox and keep the dropdown open while typing
        var parent = VisualTreeHelper.GetParent(tb) as DependencyObject;
        while (parent != null && parent is not ComboBox)
            parent = VisualTreeHelper.GetParent(parent);
        if (parent is ComboBox cb)
            cb.IsDropDownOpen = true;
    }

    // ── Everything section ────────────────────────────────────────────────────

    private void EverythingPathBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var path = EverythingPathBox.Text.Trim();
        _settings.EverythingDllPath = path;
        _onSave();
        UpdateStatus(path);
    }

    private void BrowseEverythingButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Everything64.dll auswählen",
            Filter = "Everything64.dll|Everything64.dll|DLL-Dateien (*.dll)|*.dll",
            InitialDirectory = File.Exists(_settings.EverythingDllPath)
                ? Path.GetDirectoryName(_settings.EverythingDllPath)
                : @"C:\Program Files\Everything"
        };
        if (dialog.ShowDialog() == true)
            EverythingPathBox.Text = dialog.FileName;
    }

    private static bool IsEverythingRunning() =>
        System.Diagnostics.Process.GetProcessesByName("Everything").Length > 0 ||
        System.Diagnostics.Process.GetProcessesByName("Everything64").Length > 0;

    private void UpdateStatus(string path)
    {
        var running = IsEverythingRunning();

        if (!string.IsNullOrWhiteSpace(path))
        {
            if (!File.Exists(path))
            {
                EverythingStatusText.Text = "✗ Datei nicht gefunden";
                EverythingStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36));
                return;
            }
            EverythingStatusText.Text = running
                ? "✓ Everything aktiv"
                : "✓ DLL gefunden – Everything-Dienst nicht aktiv";
            EverythingStatusText.Foreground = new SolidColorBrush(running
                ? Color.FromRgb(0x4C, 0xAF, 0x50)
                : Color.FromRgb(0xFF, 0x98, 0x00));
            return;
        }

        if (running)
        {
            EverythingStatusText.Text = "✓ Everything läuft – wird automatisch verwendet";
            EverythingStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
        }
        else
        {
            EverythingStatusText.Text = "⚠ Everything nicht aktiv – Windows Index wird als Fallback verwendet";
            EverythingStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00));
        }
    }

    // ── Template list ─────────────────────────────────────────────────────────

    private void AddTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        var template = new SearchTemplate
        {
            Name = "Neuer Suchraum",
            Shortcut = "new" + (_settings.Templates.Count + 1)
        };

        // Ensure unique shortcut
        var shortcut = template.Shortcut;
        var counter = 1;
        while (_templateManager.GetTemplateByShortcut(shortcut) != null)
            shortcut = "new" + (++counter);
        template.Shortcut = shortcut;

        _settings.Templates.Add(template);
        _onSave();
        RefreshList(expandId: template.Id);
    }

    private void DeleteTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var id = btn.Tag as string;
        if (id == null) return;

        var template = _settings.Templates.FirstOrDefault(t => t.Id == id);
        if (template == null) return;

        var confirmed = MessageBox.Show(
            $"Suchraum \"{template.Name}\" ({template.Shortcut}) wirklich löschen?",
            "Suchraum löschen",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmed != MessageBoxResult.Yes) return;

        _templateManager.DeleteTemplate(id);
        _onSave();
        RefreshList();
    }

    // ── Inline field editing ─────────────────────────────────────────────────

    private void TemplateField_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox box) return;
        var id = box.Tag as string;
        if (id == null) return;

        var template = _settings.Templates.FirstOrDefault(t => t.Id == id);
        if (template == null) return;

        // The binding should have already updated the model via TwoWay binding.
        // We just need to validate shortcut uniqueness and persist.
        var conflict = _settings.Templates.FirstOrDefault(
            t => t.Id != id &&
                 t.Shortcut.Equals(template.Shortcut, StringComparison.OrdinalIgnoreCase));

        if (conflict != null)
        {
            MessageBox.Show(
                $"Shortcut '{template.Shortcut}' wird bereits von '{conflict.Name}' verwendet.",
                "Shortcut-Konflikt", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _onSave();
        // Refresh to update the expander header summary
        RefreshList(preserveExpandedId: id);
    }

    private void SubdirsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox cb) return;
        var id = cb.Tag as string;
        if (id == null) return;
        // Binding handles the value change; just persist.
        _onSave();
    }

    // ── Entry removal ─────────────────────────────────────────────────────────

    private void RemoveEntry_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var entryId = btn.Tag as string;
        if (entryId == null) return;

        foreach (var template in _settings.Templates)
        {
            var entry = template.Entries.FirstOrDefault(en => en.Id == entryId);
            if (entry != null)
            {
                template.Entries.Remove(entry);
                _onSave();
                RefreshList(preserveExpandedId: template.Id);
                return;
            }
        }
    }

    // ── Add folder entry ──────────────────────────────────────────────────────

    private void AddFolderEntry_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var templateId = btn.Tag as string;
        if (templateId == null) return;

        var container = FindAncestorGrid(btn);
        if (container == null) return;

        var valueBox = FindNamedChild<TextBox>(container, "NewFolderValueBox");
        var labelBox = FindNamedChild<TextBox>(container, "NewFolderLabelBox");

        var value = valueBox?.Text.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value)) return;

        var template = _settings.Templates.FirstOrDefault(t => t.Id == templateId);
        if (template == null) return;

        template.Entries.Add(new TemplateEntry
        {
            Type = EntryType.Folder,
            Value = value,
            Label = labelBox?.Text.Trim() ?? string.Empty
        });

        if (valueBox != null) valueBox.Clear();
        if (labelBox != null) labelBox.Clear();

        _onSave();
        RefreshList(preserveExpandedId: templateId);
    }

    private void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Ordner auswählen",
            ValidateNames = false,
            CheckFileExists = false,
            CheckPathExists = true,
            FileName = "Ordner auswählen"
        };
        if (dialog.ShowDialog() != true) return;

        var folderPath = Path.GetDirectoryName(dialog.FileName) ?? dialog.FileName;

        if (sender is not Button btn) return;
        var container = FindAncestorGrid(btn);
        if (container == null) return;

        var valueBox = FindNamedChild<TextBox>(container, "NewFolderValueBox");
        if (valueBox != null) valueBox.Text = folderPath;
    }

    // ── Add plugin command entry ──────────────────────────────────────────────

    private void AddPluginCommandEntry_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var templateId = btn.Tag as string;
        if (templateId == null) return;

        var container = FindAncestorGrid(btn);
        if (container == null) return;

        // Value field is an editable ComboBox; Label field is still a TextBox
        var valueCombo = FindNamedChild<ComboBox>(container, "NewPluginValueBox");
        var labelBox = FindNamedChild<TextBox>(container, "NewPluginLabelBox");

        var value = valueCombo?.Text.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value)) return;

        var template = _settings.Templates.FirstOrDefault(t => t.Id == templateId);
        if (template == null) return;

        // If user selected a dropdown item, resolve the actual value from the selected item
        string stored;
        var selected = valueCombo?.SelectedItem as PluginCommandItem
                       ?? (_lastSelectedPluginItem?.DisplayName.Equals(value, StringComparison.OrdinalIgnoreCase) == true
                           ? _lastSelectedPluginItem
                           : null);

        if (selected != null)
        {
            stored = selected.Value;
        }
        else
        {
            // Manual text entry — strip any " — Category" suffix if user typed/pasted it
            stored = value.Contains(" — ") ? value[..value.IndexOf(" — ")].Trim() : value;
        }

        // Auto-set label from program display name if user selected a program and label is empty
        var label = labelBox?.Text.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(label) && selected is { Category: "Program" })
            label = selected.DisplayName;

        template.Entries.Add(new TemplateEntry
        {
            Type = EntryType.PluginCommand,
            Value = stored,
            Label = label
        });

        if (valueCombo != null) valueCombo.Text = string.Empty;
        if (labelBox != null) labelBox.Clear();
        _lastSelectedPluginItem = null;

        _onSave();
        RefreshList(preserveExpandedId: templateId);
    }

    // ── Add quick command entry ───────────────────────────────────────────────

    private void BrowseExeButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Programm auswählen",
            Filter = "Programme (*.exe)|*.exe|Alle Dateien (*.*)|*.*"
        };
        if (dialog.ShowDialog() != true) return;

        if (sender is not Button btn) return;
        var container = FindAncestorGrid(btn);
        if (container == null) return;

        var valueCombo = FindNamedChild<ComboBox>(container, "NewPluginValueBox");
        if (valueCombo != null) valueCombo.Text = dialog.FileName;
    }

    private void AddQuickCommandEntry_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var templateId = btn.Tag as string;
        if (templateId == null) return;

        var container = FindAncestorGrid(btn);
        if (container == null) return;

        var valueBox = FindNamedChild<TextBox>(container, "NewQuickValueBox");
        var labelBox = FindNamedChild<TextBox>(container, "NewQuickLabelBox");

        var value = valueBox?.Text.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value)) return;

        var template = _settings.Templates.FirstOrDefault(t => t.Id == templateId);
        if (template == null) return;

        template.Entries.Add(new TemplateEntry
        {
            Type = EntryType.QuickCommand,
            Value = value,
            Label = labelBox?.Text.Trim() ?? string.Empty
        });

        if (valueBox != null) valueBox.Clear();
        if (labelBox != null) labelBox.Clear();

        _onSave();
        RefreshList(preserveExpandedId: templateId);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void RefreshList(string? expandId = null, string? preserveExpandedId = null)
    {
        // Collect which expanders are currently open so we can restore them
        var expandedIds = new HashSet<string>();
        if (preserveExpandedId != null)
            expandedIds.Add(preserveExpandedId);

        if (TemplatesList.ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
        {
            foreach (var item in _templateItems)
            {
                var container = TemplatesList.ItemContainerGenerator.ContainerFromItem(item) as ContentPresenter;
                if (container == null) continue;
                var expander = FindVisualChild<Expander>(container);
                if (expander?.IsExpanded == true)
                    expandedIds.Add(item.Id);
            }
        }

        if (expandId != null)
            expandedIds.Add(expandId);

        _templateItems.Clear();
        foreach (var t in _settings.Templates)
            _templateItems.Add(t);

        // Restore expanded state after layout
        if (expandedIds.Count > 0)
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
            {
                foreach (var item in _templateItems)
                {
                    if (!expandedIds.Contains(item.Id)) continue;
                    var container = TemplatesList.ItemContainerGenerator.ContainerFromItem(item) as ContentPresenter;
                    if (container == null) continue;
                    var expander = FindVisualChild<Expander>(container);
                    if (expander != null) expander.IsExpanded = true;
                }
            });
        }
    }

    /// <summary>Walks up the visual tree to find the nearest Grid ancestor.</summary>
    private static Grid? FindAncestorGrid(DependencyObject child)
    {
        var current = VisualTreeHelper.GetParent(child);
        while (current != null)
        {
            if (current is Grid g)
                return g;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    /// <summary>Finds a named child of type T within a visual subtree.</summary>
    private static T? FindNamedChild<T>(DependencyObject parent, string name) where T : FrameworkElement
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T fe && fe.Name == name)
                return fe;
            var result = FindNamedChild<T>(child, name);
            if (result != null) return result;
        }
        return null;
    }

    // ── Import / Export ──────────────────────────────────────────────────

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private void ExportJson_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Search Spaces exportieren",
            Filter = "JSON-Dateien (*.json)|*.json",
            FileName = "search-spaces.json"
        };
        if (dialog.ShowDialog() != true) return;

        var exportData = _settings.Templates.Select(t => new
        {
            t.Name,
            t.Shortcut,
            t.Description,
            t.SearchSubdirectories,
            t.ExcludePatterns,
            t.FileTypeFilter,
            Entries = t.Entries.Select(entry => new
            {
                entry.Type,
                entry.Value,
                entry.Label
            })
        });
        var json = JsonSerializer.Serialize(exportData, s_jsonOptions);
        File.WriteAllText(dialog.FileName, json);
        MessageBox.Show($"{_settings.Templates.Count} Suchraum/Suchräume exportiert.",
            "Export", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ImportJson_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Search Spaces importieren",
            Filter = "JSON-Dateien (*.json)|*.json"
        };
        if (dialog.ShowDialog() != true) return;

        List<SearchTemplate> imported;
        try
        {
            var json = File.ReadAllText(dialog.FileName);
            imported = JsonSerializer.Deserialize<List<SearchTemplate>>(json, s_jsonOptions)
                       ?? new List<SearchTemplate>();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Fehler beim Lesen der Datei:\n{ex.Message}",
                "Import-Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (imported.Count == 0)
        {
            MessageBox.Show("Die Datei enthält keine Search Spaces.",
                "Import", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"{imported.Count} Suchraum/Suchräume gefunden.\n\n" +
            "Ja = Zu bestehenden hinzufügen\n" +
            "Nein = Bestehende ersetzen\n" +
            "Abbrechen = Import abbrechen",
            "Search Spaces importieren",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Cancel) return;

        if (result == MessageBoxResult.No)
            _settings.Templates.Clear();

        // Assign new IDs to avoid collisions
        foreach (var t in imported)
        {
            t.Id = Guid.NewGuid().ToString();
            foreach (var entry in t.Entries)
                entry.Id = Guid.NewGuid().ToString();
            _settings.Templates.Add(t);
        }

        _onSave();
        RefreshList();
        MessageBox.Show($"{imported.Count} Suchraum/Suchräume importiert.",
            "Import", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>Finds the first visual child of type T.</summary>
    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed) return typed;
            var result = FindVisualChild<T>(child);
            if (result != null) return result;
        }
        return null;
    }
}
