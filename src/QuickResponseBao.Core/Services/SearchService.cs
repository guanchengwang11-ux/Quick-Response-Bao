using QuickResponseBao.Core.Models;

namespace QuickResponseBao.Core.Services;

public sealed class SearchService
{
    public IReadOnlyList<SearchResult> Search(
        IEnumerable<QuickResponse> source,
        string query,
        SearchOptions? options = null)
    {
        options ??= new SearchOptions();
        if (string.IsNullOrWhiteSpace(query)) return [];
        query = query.TrimStart();
        var comparison = options.CaseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        return source.Where(x => x.IsEnabled)
            .Select(x => new SearchResult(x, GetRank(x, query, options, comparison)))
            .Where(x => x.MatchRank < int.MaxValue)
            .OrderBy(x => x.MatchRank)
            .ThenByDescending(x => x.Response.SortOrder)
            .ThenByDescending(x => options.SortByUsage ? x.Response.UsageCount : 0)
            .ThenByDescending(x => x.Response.LastUsedAt)
            .Take(Math.Clamp(options.MaximumResults, 3, 30))
            .ToList();
    }

    private static int GetRank(QuickResponse item, string query, SearchOptions options, StringComparison comparison)
    {
        if (options.MatchSummary && item.Summary.Equals(query, comparison)) return 1;
        if (options.MatchSummary && item.Summary.StartsWith(query, comparison)) return 2;
        if (options.MatchKeywords && item.Keywords.Any(x => x.Equals(query, comparison))) return 3;
        if (options.MatchContent && item.Content.StartsWith(query, comparison)) return 4;
        if (options.MatchSummary && item.Summary.Contains(query, comparison)) return 5;
        if (options.MatchKeywords && item.Keywords.Any(x => x.Contains(query, comparison))) return 6;
        if (options.MatchContent && item.Content.Contains(query, comparison)) return 7;
        if (options.MatchCategory && item.Category.Contains(query, comparison)) return 8;
        return int.MaxValue;
    }
}
