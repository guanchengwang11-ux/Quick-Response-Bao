namespace QuickResponseBao.Core.Services;

public static class QuickResponseRules
{
    public const int MinimumSummaryLength = 2;
    public const int MaximumSummaryLength = 150;
    public const int MaximumContentLength = 300;

    public static bool IsSummaryValid(string? value) =>
        value?.Trim().Length is >= MinimumSummaryLength and <= MaximumSummaryLength;

    public static bool IsContentValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= MaximumContentLength;
}
