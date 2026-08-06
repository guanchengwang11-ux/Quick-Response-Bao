using QuickResponseBao.Core.Models;

namespace QuickResponseBao.Core.Services;

public sealed record QuickResponseBusinessKey(
    string Summary,
    string Content,
    string Keywords,
    string Category,
    string Language,
    bool IsEnabled,
    int? SortOrder)
{
    public static QuickResponseBusinessKey Create(QuickResponse item, bool includeSortOrder) => new(
        item.Summary.Trim(),
        item.Content.Trim(),
        KeywordNormalizer.CanonicalKey(item.Keywords),
        item.Category.Trim().ToUpperInvariant(),
        NormalizeLanguage(item.Language),
        item.IsEnabled,
        includeSortOrder ? item.SortOrder : null);

    public static string NormalizeLanguage(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Equals("English", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("en", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("en-US", StringComparison.OrdinalIgnoreCase)) return "ENGLISH";
        if (normalized.Equals("简体中文", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Chinese", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("zh-CN", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("zh-Hans", StringComparison.OrdinalIgnoreCase)) return "ZH-CN";
        return normalized.ToUpperInvariant();
    }
}
