using System.ComponentModel;
using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using LocalisationStudio.Core;
using Microsoft.Win32;

namespace LocalisationStudio.Wpf;

public partial class MainWindow : Window
{
    private DataTable table = new();
    private List<string> languages = [];
    private string? projectPath;
    private bool dirty;

    public MainWindow()
    {
        InitializeComponent();
        Display(new TranslationDocument(), null);
    }

    private void Display(TranslationDocument document, string? path)
    {
        document.Validate();
        languages = document.Languages.ToList();
        table = new DataTable();
        table.Columns.Add("ID", typeof(string)).DefaultValue = "";
        for (int i = 0; i < languages.Count; i++)
        {
            var column = table.Columns.Add("L" + i, typeof(string));
            column.Caption = languages[i];
            column.DefaultValue = "";
        }
        foreach (var entry in document.Entries)
            table.Rows.Add(new[] { entry.Id }.Concat(languages.Select(l => document.Value(entry, l))).Cast<object>().ToArray());
        table.RowChanged += (_, _) => Changed();
        table.RowDeleted += (_, _) => Changed();
        TranslationsGrid.ItemsSource = table.DefaultView;
        LanguageSelector.ItemsSource = languages;
        LanguageSelector.SelectedIndex = 0;
        projectPath = path;
        dirty = false;
        RefreshStatus();
    }

    private void Changed() { dirty = true; RefreshStatus(); }

    private void RefreshStatus()
    {
        var name = projectPath is null ? "Sans titre" : Path.GetFileName(projectPath);
        Title = $"{(dirty ? "* " : "")}{name} — Localisation Studio";
    }

    private void Commit()
    {
        if (!TranslationsGrid.CommitEdit(DataGridEditingUnit.Cell, true) ||
            !TranslationsGrid.CommitEdit(DataGridEditingUnit.Row, true))
            throw new InvalidDataException("Terminez ou annulez la cellule en cours de modification.");
    }

    private TranslationDocument Snapshot()
    {
        Commit();
        var document = new TranslationDocument { Languages = languages.ToList() };
        foreach (DataRow row in table.Rows)
        {
            if (row.RowState == DataRowState.Deleted) continue;
            var entry = new TranslationEntry { Id = row[0]?.ToString() ?? "" };
            for (int i = 0; i < languages.Count; i++) entry.Values[languages[i]] = row[i + 1]?.ToString() ?? "";
            document.Entries.Add(entry);
        }
        document.Validate();
        return document;
    }

    private void Grid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        e.Column.Header = table.Columns[e.PropertyName]?.Caption ?? e.PropertyName;
        e.Column.Width = e.PropertyName == "ID" ? new DataGridLength(210) : new DataGridLength(1, DataGridLengthUnitType.Star);
        e.Column.MinWidth = 140;
        if (e.Column is DataGridTextColumn column)
        {
            column.Binding = new Binding(e.PropertyName)
            { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.LostFocus };
            var style = new Style(typeof(TextBox));
            style.Setters.Add(new Setter(TextBox.AcceptsReturnProperty, true));
            style.Setters.Add(new Setter(TextBox.TextWrappingProperty, TextWrapping.Wrap));
            column.EditingElementStyle = style;
            var display = new Style(typeof(TextBlock));
            display.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
            display.Setters.Add(new Setter(TextBlock.MarginProperty, new Thickness(8, 6, 8, 6)));
            column.ElementStyle = display;
        }
    }

    private void Run(Action action)
    {
        try { action(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Opération impossible", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private bool ConfirmDiscard()
    {
        Commit();
        if (!dirty) return true;
        var answer = MessageBox.Show(this, "Enregistrer les modifications avant de continuer ?", "Document modifié",
            MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        return answer == MessageBoxResult.No || answer == MessageBoxResult.Yes && Save(false);
    }

    private void New_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        if (ConfirmDiscard()) Display(new TranslationDocument(), null);
    });

    private void Open_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        if (!ConfirmDiscard()) return;
        var dialog = new OpenFileDialog { Filter = "Traductions|*.json;*.xml;*.csv|JSON|*.json|XML|*.xml|CSV|*.csv", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;
        // Parse completely before replacing the current table.
        var document = DocumentFiles.Load(dialog.FileName);
        bool isProject = Path.GetExtension(dialog.FileName).Equals(".json", StringComparison.OrdinalIgnoreCase);
        Display(document, isProject ? dialog.FileName : null);
        if (!isProject) Changed(); // An imported CSV/XML still needs a JSON project save.
    });

    private void Save_Click(object sender, RoutedEventArgs e) => Run(() => Save(false));
    private void SaveAs_Click(object sender, RoutedEventArgs e) => Run(() => Save(true));

    private bool Save(bool saveAs)
    {
        var document = Snapshot();
        string? path = projectPath;
        if (saveAs || path is null)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Projet JSON|*.json", DefaultExt = ".json", AddExtension = true,
                FileName = path is null ? "traductions.json" : Path.GetFileName(path), OverwritePrompt = true
            };
            if (dialog.ShowDialog(this) != true) return false;
            path = dialog.FileName;
            if (!Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Le projet doit être enregistré avec l’extension .json.");
        }
        DocumentFiles.WriteBatch(new Dictionary<string, string> { [path] = DocumentFiles.Serialize(document, ".json") });
        projectPath = path; dirty = false; RefreshStatus();
        return true;
    }

    private void Export_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        var document = Snapshot();
        var dialog = new SaveFileDialog
        {
            Filter = "CSV|*.csv|JSON|*.json|XML|*.xml|Classe C#|*.cs|Classe C++ (en-tête + source)|*.h",
            FileName = "Localization", AddExtension = true, OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != true) return;
        string extension = new[] { ".csv", ".json", ".xml", ".cs", ".h" }[dialog.FilterIndex - 1];
        string path = dialog.FileName;
        if (!Path.GetExtension(path).Equals(extension, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"L’extension doit être {extension} pour ce format.");
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (extension is ".cs" or ".h")
        {
            if (extension == ".cs") files[path] = CodeExporter.CSharp(document);
            else
            {
                var cpp = CodeExporter.Cpp(document, Path.GetFileName(path));
                files[path] = cpp.Header;
                files[Path.ChangeExtension(path, ".cpp")] = cpp.Source;
            }
            files[Path.ChangeExtension(path, ".localization.json")] = DocumentFiles.Serialize(document, ".json");
            var companions = files.Keys.Where(p => p != path && File.Exists(p)).ToList();
            if (companions.Count > 0 && MessageBox.Show(this,
                "Remplacer aussi les fichiers associés ?\n" + string.Join("\n", companions), "Fichiers existants",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        }
        else files[path] = DocumentFiles.Serialize(document, extension);
        DocumentFiles.WriteBatch(files);
        if (extension is ".cs" or ".h")
        {
            projectPath = Path.ChangeExtension(path, ".localization.json");
            dirty = false;
        }
        else if (extension == ".json") { projectPath = path; dirty = false; }
        RefreshStatus();
        MessageBox.Show(this, "Export terminé :\n" + string.Join("\n", files.Keys), "Export", MessageBoxButton.OK, MessageBoxImage.Information);
    });

    private void AddLanguage_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        Commit();
        string language = LanguageName.Text.Trim();
        // Validate the schema separately so a partially edited row doesn't prevent adding a language.
        new TranslationDocument { Languages = languages.Append(language).ToList() }.Validate();
        languages.Add(language);
        var column = table.Columns.Add("L" + (languages.Count - 1), typeof(string));
        column.Caption = language; column.DefaultValue = "";
        Rebind(); LanguageName.Clear(); Changed();
    });

    private void RemoveLanguage_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        Commit();
        if (LanguageSelector.SelectedItem is not string language) return;
        if (languages.Count == 1) throw new InvalidDataException("Conservez au moins une langue.");
        if (MessageBox.Show(this, $"Supprimer la langue « {language} » et toutes ses traductions ?", "Suppression",
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        int index = languages.IndexOf(language);
        table.Columns.RemoveAt(index + 1);
        languages.RemoveAt(index);
        for (int i = index + 1; i < table.Columns.Count; i++) table.Columns[i].ColumnName = "L" + (i - 1);
        Rebind(); Changed();
    });

    private void Rebind()
    {
        TranslationsGrid.ItemsSource = null;
        TranslationsGrid.ItemsSource = table.DefaultView;
        LanguageSelector.ItemsSource = null;
        LanguageSelector.ItemsSource = languages;
        LanguageSelector.SelectedIndex = languages.Count - 1;
    }

    private void AddRow_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        Commit();
        int number = 1;
        var ids = table.Rows.Cast<DataRow>().Where(r => r.RowState != DataRowState.Deleted)
            .Select(r => r[0]?.ToString()).ToHashSet(StringComparer.Ordinal);
        while (ids.Contains("NEW_KEY_" + number)) number++;
        var row = table.NewRow(); row[0] = "NEW_KEY_" + number; table.Rows.Add(row);
        var view = table.DefaultView.Cast<DataRowView>().First(v => ReferenceEquals(v.Row, row));
        TranslationsGrid.SelectedItem = view;
        TranslationsGrid.ScrollIntoView(view);
        TranslationsGrid.CurrentCell = new DataGridCellInfo(view, TranslationsGrid.Columns[0]);
        TranslationsGrid.Focus(); TranslationsGrid.BeginEdit();
    });

    private void DeleteRows_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        Commit();
        var rows = TranslationsGrid.SelectedItems.OfType<DataRowView>().ToList();
        if (rows.Count == 0) return;
        if (MessageBox.Show(this, $"Supprimer {rows.Count} ligne(s) ?", "Suppression", MessageBoxButton.YesNo,
            MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        foreach (var row in rows) row.Delete();
    });

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        try { e.Cancel = !ConfirmDiscard(); }
        catch (Exception ex)
        {
            e.Cancel = true;
            MessageBox.Show(this, ex.Message, "Fermeture annulée", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
