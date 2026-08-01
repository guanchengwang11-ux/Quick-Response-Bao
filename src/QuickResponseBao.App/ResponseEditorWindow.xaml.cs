using System.Windows;
using System.Windows.Controls;
using QuickResponseBao.App.Services;
using QuickResponseBao.Core.Models;

namespace QuickResponseBao.App;

public partial class ResponseEditorWindow : Window
{
    public ResponseEditorWindow(QuickResponse? source = null)
    {
        InitializeComponent();
        if (source is not null) Title = LocalizationService.Get("Edit");
        Response = source ?? new QuickResponse();
        SummaryText.Text = Response.Summary; ContentText.Text = Response.Content;
        KeywordsText.Text = string.Join("; ", Response.Keywords); CategoryText.Text = DisplayCategory(Response.Category);
        EnabledBox.IsChecked = Response.IsEnabled;
        LanguageBox.SelectedIndex = Response.Language == "简体中文" ? 1 : 0;
    }
    public QuickResponse Response { get; }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var summaryValid = SummaryText.Text.Trim().Length is >= 2 and <= 150;
        var contentValid = !string.IsNullOrWhiteSpace(ContentText.Text);
        SummaryError.Visibility = summaryValid ? Visibility.Collapsed : Visibility.Visible;
        ContentError.Visibility = contentValid ? Visibility.Collapsed : Visibility.Visible;
        if (!summaryValid || !contentValid) return;
        Response.Summary = SummaryText.Text.Trim(); Response.Content = ContentText.Text;
        Response.Keywords = KeywordsText.Text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        Response.Category = StorageCategory(CategoryText.Text);
        Response.Language = (LanguageBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "English";
        Response.IsEnabled = EnabledBox.IsChecked == true; DialogResult = true;
    }

    private static string DisplayCategory(string value) => value.Equals("General", StringComparison.OrdinalIgnoreCase)
        ? LocalizationService.Get("GeneralCategory") : value.Equals("Uncategorized", StringComparison.OrdinalIgnoreCase)
            ? LocalizationService.Get("UncategorizedCategory") : value;
    private static string StorageCategory(string value) => string.IsNullOrWhiteSpace(value) || value.Trim().Equals(LocalizationService.Get("GeneralCategory"), StringComparison.OrdinalIgnoreCase)
        ? "General" : value.Trim().Equals(LocalizationService.Get("UncategorizedCategory"), StringComparison.OrdinalIgnoreCase)
            ? "Uncategorized" : value.Trim();
}
