using System.Windows;
using System.Windows.Controls;
using QuickResponseBao.Core.Models;

namespace QuickResponseBao.App;

public partial class ResponseEditorWindow : Window
{
    public ResponseEditorWindow(QuickResponse? source = null)
    {
        InitializeComponent();
        Response = source ?? new QuickResponse();
        SummaryText.Text = Response.Summary; ContentText.Text = Response.Content;
        KeywordsText.Text = string.Join("; ", Response.Keywords); CategoryText.Text = Response.Category;
        EnabledBox.IsChecked = Response.IsEnabled;
        LanguageBox.SelectedIndex = Response.Language == "简体中文" ? 1 : 0;
    }
    public QuickResponse Response { get; }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (SummaryText.Text.Trim().Length is < 2 or > 150 || string.IsNullOrWhiteSpace(ContentText.Text))
        { System.Windows.MessageBox.Show("Summary must contain 2-150 characters and content is required.", "Quick Response Bao"); return; }
        Response.Summary = SummaryText.Text.Trim(); Response.Content = ContentText.Text;
        Response.Keywords = KeywordsText.Text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        Response.Category = string.IsNullOrWhiteSpace(CategoryText.Text) ? "General" : CategoryText.Text.Trim();
        Response.Language = (LanguageBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "English";
        Response.IsEnabled = EnabledBox.IsChecked == true; DialogResult = true;
    }
}
