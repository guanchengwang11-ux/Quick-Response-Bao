using QuickResponseBao.Core.Models;
using QuickResponseBao.Core.Services;

namespace QuickResponseBao.App.ViewModels;

public sealed class ResponseLibraryViewModel
{
    private readonly ResponseLibraryFacetIndex _index = new();
    private int _dataVersion = -1;
    private int _requestGeneration;

    public void Synchronize(IEnumerable<QuickResponse> responses, int dataVersion)
    {
        if (_dataVersion == dataVersion) return;
        _index.Rebuild(responses);
        _dataVersion = dataVersion;
        Interlocked.Increment(ref _requestGeneration);
    }

    public async Task<FacetQueryResult> QueryFacetAsync(ResponseFilterField field, ResponseLibraryFilterState state, CancellationToken cancellationToken = default)
    {
        var generation = Volatile.Read(ref _requestGeneration);
        var result = await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var values = _index.GetValues(field, state);
            var matchCount = _index.CountMatches(state);
            return new FacetQueryResult(values, matchCount);
        }, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (generation != Volatile.Read(ref _requestGeneration)) throw new OperationCanceledException("The library snapshot changed.");
        return result;
    }
}

public sealed record FacetQueryResult(IReadOnlyList<FacetValue> Values, int MatchCount);
