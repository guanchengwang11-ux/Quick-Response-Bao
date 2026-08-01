namespace QuickResponseBao.Core.Models;

public sealed record SearchOptions(
    bool MatchSummary = true,
    bool MatchContent = true,
    bool MatchKeywords = true,
    bool MatchCategory = true,
    bool CaseSensitive = false,
    bool SortByUsage = true,
    int MaximumResults = 10);

public sealed record SearchResult(QuickResponse Response, int MatchRank);

public sealed record TextSegment(string Text, bool IsMatch);
