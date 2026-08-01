using QuickResponseBao.Core.Models;

namespace QuickResponseBao.Core.Services;

public sealed class TextHighlightService
{
    public IReadOnlyList<TextSegment> Split(string text, string query, bool caseSensitive = false)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(query))
            return [new TextSegment(text, false)];

        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var result = new List<TextSegment>();
        var cursor = 0;
        while (cursor < text.Length)
        {
            var index = text.IndexOf(query, cursor, comparison);
            if (index < 0)
            {
                result.Add(new TextSegment(text[cursor..], false));
                break;
            }
            if (index > cursor) result.Add(new TextSegment(text[cursor..index], false));
            result.Add(new TextSegment(text.Substring(index, query.Length), true));
            cursor = index + query.Length;
        }
        return result;
    }
}
