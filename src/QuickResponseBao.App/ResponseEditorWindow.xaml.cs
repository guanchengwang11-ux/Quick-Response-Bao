using System.Windows;
using System.Windows.Controls;
using QuickResponseBao.App.Services;
using QuickResponseBao.Core.Models;
using QuickResponseBao.Core.Services;

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
        RefreshContentFeedback();
    }
    public QuickResponse Response { get; }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var summaryValid = QuickResponseRules.IsSummaryValid(SummaryText.Text);
        var contentValid = QuickResponseRules.IsContentValid(ContentText.Text);
        SummaryError.Visibility = summaryValid ? Visibility.Collapsed : Visibility.Visible;
        ContentError.Visibility = contentValid ? Visibility.Collapsed : Visibility.Visible;
        ContentError.Text = GetContentValidationMessage();
        if (!summaryValid || !contentValid) return;
        Response.Summary = SummaryText.Text.Trim(); Response.Content = ContentText.Text;
        Response.Keywords = KeywordNormalizer.Parse(KeywordsText.Text);
        Response.Category = StorageCategory(CategoryText.Text);
        Response.Language = (LanguageBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "English";
        Response.IsEnabled = EnabledBox.IsChecked == true; DialogResult = true;
    }

    private void ContentText_TextChanged(object sender, TextChangedEventArgs e) => RefreshContentFeedback();

    private void RefreshContentFeedback()
    {
        if (ContentCounter is null || ContentError is null) return;
        var metrics = QuickResponseRules.GetContentMetrics(ContentText.Text);
        ContentCounter.Text = BuildContentCounter(metrics);
        var errorCode = QuickResponseRules.GetContentValidationErrorCode(ContentText.Text);
        var tooLong = !string.IsNullOrEmpty(errorCode);
        var approachingLimit = metrics.WordCount >= QuickResponseRules.MaximumContentWordCount * 0.9
            || metrics.CjkCharacterCount >= QuickResponseRules.MaximumContentCjkCharacterCount * 0.9;
        ContentCounter.SetResourceReference(TextBlock.ForegroundProperty, tooLong ? "ErrorBrush" : approachingLimit ? "WarningBrush" : "MutedTextBrush");
        if (tooLong)
        {
            ContentError.Text = GetContentValidationMessage();
            ContentError.Visibility = Visibility.Visible;
        }
        else if (!string.IsNullOrWhiteSpace(ContentText.Text)) ContentError.Visibility = Visibility.Collapsed;
    }

    private static string BuildContentCounter(ContentMetrics metrics)
    {
        var words = string.Format(LocalizationService.Get("ContentWordCounter"), metrics.WordCount, QuickResponseRules.MaximumContentWordCount);
        var cjk = string.Format(LocalizationService.Get("ContentCjkCounter"), metrics.CjkCharacterCount, QuickResponseRules.MaximumContentCjkCharacterCount);
        return metrics.WordCount > 0 && metrics.CjkCharacterCount > 0 ? $"{words}{Environment.NewLine}{cjk}"
            : metrics.CjkCharacterCount > 0 ? cjk : words;
    }

    private string GetContentValidationMessage()
    {
        if (string.IsNullOrWhiteSpace(ContentText.Text)) return LocalizationService.Get("ValidationContent");
        return QuickResponseRules.GetContentValidationErrorCode(ContentText.Text) switch
        {
            "ContentWordLimitExceeded" => LocalizationService.Get("ValidationContentWordLimit"),
            "ContentCjkLimitExceeded" => LocalizationService.Get("ValidationContentCjkLimit"),
            "ContentLengthLimitExceeded" => LocalizationService.Get("ValidationContentLengthLimit"),
            _ => LocalizationService.Get("ValidationContent")
        };
    }

    private static string DisplayCategory(string value) => value.Equals("General", StringComparison.OrdinalIgnoreCase)
        ? LocalizationService.Get("GeneralCategory") : value.Equals("Uncategorized", StringComparison.OrdinalIgnoreCase)
            ? LocalizationService.Get("UncategorizedCategory") : value;
    private static string StorageCategory(string value) => string.IsNullOrWhiteSpace(value) || value.Trim().Equals(LocalizationService.Get("GeneralCategory"), StringComparison.OrdinalIgnoreCase)
        ? "General" : value.Trim().Equals(LocalizationService.Get("UncategorizedCategory"), StringComparison.OrdinalIgnoreCase)
            ? "Uncategorized" : value.Trim();
}
