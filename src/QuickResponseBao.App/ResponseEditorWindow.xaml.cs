using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QuickResponseBao.App.Services;
using QuickResponseBao.Core.Models;
using QuickResponseBao.Core.Services;
using FluentWindow = Wpf.Ui.Controls.FluentWindow;

namespace QuickResponseBao.App;

public partial class ResponseEditorWindow : FluentWindow
{
    private static readonly char[] KeywordSeparators = [',', ';', '，', '；', '\r', '\n'];
    private readonly ObservableCollection<string> _keywords = [];

    public ResponseEditorWindow(QuickResponse? source = null)
    {
        InitializeComponent();
        Response = source ?? new QuickResponse();
        if (source is not null)
        {
            SetResourceReference(TitleProperty, "EditResponseTitle");
            EditorHeading.SetResourceReference(TextBlock.TextProperty, "EditResponseTitle");
        }
        SummaryText.Text = Response.Summary;
        ContentText.Text = Response.Content;
        foreach (var keyword in KeywordNormalizer.Normalize(Response.Keywords)) _keywords.Add(keyword);
        KeywordItems.ItemsSource = _keywords;
        CategoryBox.Text = DisplayCategory(Response.Category);
        EnabledBox.IsChecked = Response.IsEnabled;
        LanguageBox.SelectedIndex = Response.Language == "简体中文" ? 1 : 0;
        Loaded += LoadCategoriesAsync;
        RefreshSummaryFeedback();
        RefreshContentFeedback();
    }

    public QuickResponse Response { get; }

    private async void LoadCategoriesAsync(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current is not App app) return;
        var selected = CategoryBox.Text;
        var categories = await app.CategoryRepository.GetCategoriesAsync();
        CategoryBox.ItemsSource = categories.Select(x => DisplayCategory(x.Name)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        CategoryBox.Text = selected;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        AddPendingKeywords();
        var summaryValid = QuickResponseRules.IsSummaryValid(SummaryText.Text);
        var contentValid = QuickResponseRules.IsContentValid(ContentText.Text);
        SummaryError.Visibility = summaryValid ? Visibility.Collapsed : Visibility.Visible;
        ContentError.Visibility = contentValid ? Visibility.Collapsed : Visibility.Visible;
        ContentError.Text = GetContentValidationMessage();
        if (!summaryValid || !contentValid) return;

        SaveButton.IsEnabled = false;
        Response.Summary = SummaryText.Text.Trim();
        Response.Content = ContentText.Text;
        Response.Keywords = KeywordNormalizer.Normalize(_keywords);
        Response.Category = StorageCategory(CategoryBox.Text);
        Response.Language = (LanguageBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "English";
        Response.IsEnabled = EnabledBox.IsChecked == true;
        DialogResult = true;
    }

    private void SummaryText_TextChanged(object sender, TextChangedEventArgs e) => RefreshSummaryFeedback();
    private void ContentText_TextChanged(object sender, TextChangedEventArgs e) => RefreshContentFeedback();

    private void RefreshSummaryFeedback()
    {
        if (SummaryCounter is null) return;
        SummaryCounter.Text = $"{SummaryText.Text.Length} / 150";
        if (QuickResponseRules.IsSummaryValid(SummaryText.Text)) SummaryError.Visibility = Visibility.Collapsed;
    }

    private void RefreshContentFeedback()
    {
        if (ContentCounter is null || ContentError is null) return;
        var metrics = QuickResponseRules.GetContentMetrics(ContentText.Text);
        ContentCounter.Text = BuildContentCounter(metrics);
        var errorCode = QuickResponseRules.GetContentValidationErrorCode(ContentText.Text);
        var tooLong = !string.IsNullOrEmpty(errorCode);
        var approachingLimit = metrics.WordCount >= QuickResponseRules.MaximumContentWordCount * 0.9
            || metrics.CjkCharacterCount >= QuickResponseRules.MaximumContentCjkCharacterCount * 0.9;
        ContentCounter.SetResourceReference(TextBlock.ForegroundProperty, tooLong ? "QrbErrorBrush" : approachingLimit ? "QrbWarningBrush" : "QrbTextSecondaryBrush");
        if (tooLong)
        {
            ContentError.Text = GetContentValidationMessage();
            ContentError.Visibility = Visibility.Visible;
        }
        else if (!string.IsNullOrWhiteSpace(ContentText.Text)) ContentError.Visibility = Visibility.Collapsed;
    }

    private void KeywordInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        AddPendingKeywords();
    }

    private void KeywordInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (KeywordInput.Text.IndexOfAny(KeywordSeparators) >= 0) AddPendingKeywords();
    }

    private void AddPendingKeywords()
    {
        if (KeywordInput is null || string.IsNullOrWhiteSpace(KeywordInput.Text)) return;
        foreach (var keyword in KeywordNormalizer.Parse(KeywordInput.Text))
            if (!_keywords.Contains(keyword, StringComparer.OrdinalIgnoreCase)) _keywords.Add(keyword);
        KeywordInput.Clear();
    }

    private void RemoveKeyword_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is string keyword) _keywords.Remove(keyword);
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
