using System.Text.Json;

namespace QuickResponseBao.Core.Services;

public static class KeywordNormalizer
{
    private static readonly char[] Separators = [';', '；', ',', '，', '\r', '\n'];

    public static List<string> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        if (TryParseArray(value, out var array)) return Normalize(array);
        return Normalize(value.Split(Separators, StringSplitOptions.RemoveEmptyEntries));
    }

    public static List<string> Normalize(IEnumerable<string>? values) => values is null
        ? []
        : values.Select(x => x?.Trim() ?? string.Empty)
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static string CanonicalKey(IEnumerable<string>? values) => string.Join('\0',
        Normalize(values).Select(x => x.ToUpperInvariant()).Order(StringComparer.Ordinal));

    private static bool TryParseArray(string value, out IEnumerable<string> values)
    {
        values = [];
        if (!value.TrimStart().StartsWith('[')) return false;
        try
        {
            values = JsonSerializer.Deserialize<List<string>>(value) ?? [];
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
