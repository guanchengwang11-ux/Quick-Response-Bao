using System.Data;
using System.Windows;
using System.Windows.Controls;
using ComboBox = System.Windows.Controls.ComboBox;
using QuickResponseBao.App.Services;
using QuickResponseBao.Core.Models;

namespace QuickResponseBao.App;

public partial class ImportPreviewWindow : Window
{
    private readonly ImportPreview _preview;
    public ImportPreviewWindow(ImportPreview preview, ImportFieldMapping suggested)
    {
        InitializeComponent(); _preview = preview;
        var table = new DataTable(); foreach (var header in preview.Headers) table.Columns.Add(header);
        foreach (var row in preview.Rows) table.Rows.Add(preview.Headers.Select(header => row.Values.TryGetValue(header, out var value) ? value : string.Empty).ToArray());
        PreviewGrid.ItemsSource = table.DefaultView;
        foreach (var box in Boxes()) { box.ItemsSource = new[] { string.Empty }.Concat(preview.Headers); }
        Select(SummaryBox, suggested.Get(QuickResponseField.Summary)); Select(ContentBox, suggested.Get(QuickResponseField.Content));
        Select(KeywordsBox, suggested.Get(QuickResponseField.Keywords)); Select(CategoryBox, suggested.Get(QuickResponseField.Category));
        Select(LanguageBox, suggested.Get(QuickResponseField.Language)); Select(EnabledBox, suggested.Get(QuickResponseField.IsEnabled));
        Select(SortOrderBox, suggested.Get(QuickResponseField.SortOrder));
        SummaryText.Text = $"{LocalizationService.Get("Total")}: {preview.TotalRows}";
    }
    public ImportFieldMapping Mapping { get; private set; } = new(new Dictionary<QuickResponseField, string>());
    private void Import_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SummaryBox.SelectedItem?.ToString()) || string.IsNullOrWhiteSpace(ContentBox.SelectedItem?.ToString()))
        { System.Windows.MessageBox.Show(LocalizationService.Get("RequiredMapping"), LocalizationService.Get("ImportPreview")); return; }
        var pairs = new Dictionary<QuickResponseField, string>(); Add(pairs, QuickResponseField.Summary, SummaryBox); Add(pairs, QuickResponseField.Content, ContentBox);
        Add(pairs, QuickResponseField.Keywords, KeywordsBox); Add(pairs, QuickResponseField.Category, CategoryBox); Add(pairs, QuickResponseField.Language, LanguageBox); Add(pairs, QuickResponseField.IsEnabled, EnabledBox); Add(pairs, QuickResponseField.SortOrder, SortOrderBox);
        Mapping = new ImportFieldMapping(pairs); DialogResult = true;
    }
    private IEnumerable<ComboBox> Boxes() => [SummaryBox, ContentBox, KeywordsBox, CategoryBox, LanguageBox, EnabledBox, SortOrderBox];
    private static void Select(ComboBox box, string? value) => box.SelectedItem = value ?? string.Empty;
    private static void Add(IDictionary<QuickResponseField, string> target, QuickResponseField field, ComboBox box)
    { if (box.SelectedItem?.ToString() is { Length: > 0 } value) target[field] = value; }
}
