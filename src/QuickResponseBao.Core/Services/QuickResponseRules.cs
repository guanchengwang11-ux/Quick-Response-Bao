using System.Text;

namespace QuickResponseBao.Core.Services;

public static class QuickResponseRules
{
    public const int MinimumSummaryLength = 2;
    public const int MaximumSummaryLength = 150;
    public const int MaximumContentWordCount = 600;
    public const int MaximumContentCjkCharacterCount = 3000;

    public static bool IsSummaryValid(string? value) =>
        value?.Trim().Length is >= MinimumSummaryLength and <= MaximumSummaryLength;

    public static bool IsContentValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && GetContentMetrics(value).IsValid;

    public static ContentMetrics GetContentMetrics(string? value)
    {
        var wordCount = 0;
        var cjkCharacterCount = 0;
        var tokenHasWordCharacter = false;

        foreach (var rune in (value ?? string.Empty).EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune))
            {
                if (tokenHasWordCharacter) wordCount++;
                tokenHasWordCharacter = false;
                continue;
            }

            if (IsCjk(rune)) cjkCharacterCount++;
            else if (Rune.IsLetterOrDigit(rune)) tokenHasWordCharacter = true;
        }

        if (tokenHasWordCharacter) wordCount++;
        return new ContentMetrics(wordCount, cjkCharacterCount);
    }

    public static string GetContentValidationErrorCode(string? value)
    {
        var metrics = GetContentMetrics(value);
        return (metrics.WordCount > MaximumContentWordCount, metrics.CjkCharacterCount > MaximumContentCjkCharacterCount) switch
        {
            (true, true) => "ContentLengthLimitExceeded",
            (true, false) => "ContentWordLimitExceeded",
            (false, true) => "ContentCjkLimitExceeded",
            _ => string.Empty
        };
    }

    private static bool IsCjk(Rune rune)
    {
        var value = rune.Value;
        return value is >= 0x3400 and <= 0x4DBF
            or >= 0x4E00 and <= 0x9FFF
            or >= 0xF900 and <= 0xFAFF
            or >= 0x20000 and <= 0x2FA1F
            or >= 0x3040 and <= 0x30FF
            or >= 0x31F0 and <= 0x31FF
            or >= 0x3100 and <= 0x312F
            or >= 0x1100 and <= 0x11FF
            or >= 0x3130 and <= 0x318F
            or >= 0xAC00 and <= 0xD7AF
            or >= 0xFF66 and <= 0xFF9D;
    }
}

public readonly record struct ContentMetrics(int WordCount, int CjkCharacterCount)
{
    public bool IsValid => WordCount <= QuickResponseRules.MaximumContentWordCount
        && CjkCharacterCount <= QuickResponseRules.MaximumContentCjkCharacterCount;
}
