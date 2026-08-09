using QuickResponseBao.Core.Models;

namespace QuickResponseBao.Core.Services;

public sealed record FacetValue(string Value, int Count);

public sealed class ResponseLibraryFacetIndex
{
    private QuickResponse[] _snapshot = [];

    public int Count => _snapshot.Length;
    public void Rebuild(IEnumerable<QuickResponse> responses) => _snapshot = responses.ToArray();

    public IReadOnlyList<FacetValue> GetValues(ResponseFilterField field, ResponseLibraryFilterState state)
    {
        if (field is not (ResponseFilterField.Category or ResponseFilterField.Language or ResponseFilterField.Status)) return [];
        return _snapshot
            .Where(response => state.Matches(response, field))
            .GroupBy(response => GetValue(response, field), StringComparer.OrdinalIgnoreCase)
            .Select(group => new FacetValue(group.Key, group.Count()))
            .OrderBy(value => value.Value, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public int CountMatches(ResponseLibraryFilterState state) => _snapshot.Count(state.Matches);

    private static string GetValue(QuickResponse response, ResponseFilterField field) => field switch
    {
        ResponseFilterField.Category => response.Category,
        ResponseFilterField.Language => response.Language,
        ResponseFilterField.Status => response.IsEnabled ? "enabled" : "disabled",
        _ => string.Empty
    };
}
