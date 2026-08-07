using System.Globalization;
using QuickResponseBao.Core.Models;
using QuickResponseBao.Core.Services;

namespace QuickResponseBao.Infrastructure.ImportExport;

public static class QuickResponseImportParser
{
    private static readonly IReadOnlyDictionary<QuickResponseField, string[]> Aliases =
        new Dictionary<QuickResponseField, string[]>
        {
            [QuickResponseField.Summary] = ["Summary", "摘要"],
            [QuickResponseField.Content] = ["Content", "Response", "话术正文", "正文"],
            [QuickResponseField.Keywords] = ["Keyword", "Keywords", "Key Word", "Key Words", "关键词"],
            [QuickResponseField.Category] = ["Category", "分类"],
            [QuickResponseField.Language] = ["Language", "语言"],
            [QuickResponseField.IsEnabled] = ["IsEnabled", "Enabled", "启用状态", "启用"],
            [QuickResponseField.SortOrder] = ["SortOrder", "Sort Order", "Weight", "排序权重", "排序"]
        };

    public static ImportFieldMapping SuggestMapping(IEnumerable<string> headers)
    {
        var available = headers.ToList();
        var result = new Dictionary<QuickResponseField, string>();
        foreach (var pair in Aliases)
        {
            var normalizedAliases = pair.Value.Select(NormalizeHeader).ToHashSet(StringComparer.Ordinal);
            var match = available.FirstOrDefault(header => normalizedAliases.Contains(NormalizeHeader(header)));
            if (match is not null) result[pair.Key] = match;
        }
        return new ImportFieldMapping(result);
    }

    public static QuickResponse Create(string summary, string content, string keywords, string category,
        string language, string enabled, string sortOrder, bool includesSortOrder)
    {
        summary = summary.Trim();
        if (!QuickResponseRules.IsSummaryValid(summary)) throw new InvalidDataException("ImportSummaryInvalid");
        if (string.IsNullOrWhiteSpace(content)) throw new InvalidDataException("ImportContentRequired");
        var contentValidationError = QuickResponseRules.GetContentValidationErrorCode(content);
        if (!string.IsNullOrEmpty(contentValidationError)) throw new InvalidDataException($"Import{contentValidationError}");
        return new QuickResponse
        {
            Summary = summary, Content = content, Keywords = KeywordNormalizer.Parse(keywords),
            Category = string.IsNullOrWhiteSpace(category) ? "General" : category.Trim(),
            Language = NormalizeLanguage(language), IsEnabled = ParseEnabled(enabled),
            SortOrder = includesSortOrder ? ParseSortOrder(sortOrder) : 0
        };
    }

    public static string NormalizeHeader(string value) => string.Concat(value.Trim()
        .Where(character => !char.IsWhiteSpace(character) && character is not '_' and not '-')).ToUpperInvariant();

    private static string NormalizeLanguage(string value)
    {
        var normalized = QuickResponseBusinessKey.NormalizeLanguage(value);
        return normalized switch { "ENGLISH" => "English", "ZH-CN" => "简体中文", _ => string.IsNullOrWhiteSpace(value) ? "English" : value.Trim() };
    }

    private static bool ParseEnabled(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        if (new[] { "true", "1", "yes", "enabled", "是", "启用" }.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase)) return true;
        if (new[] { "false", "0", "no", "disabled", "否", "停用" }.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase)) return false;
        throw new FormatException("ImportEnabledInvalid");
    }

    private static int ParseSortOrder(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        if (int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) return parsed;
        throw new FormatException("ImportSortOrderInvalid");
    }
}
