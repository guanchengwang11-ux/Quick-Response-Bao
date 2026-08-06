using QuickResponseBao.Core.Interfaces;
using QuickResponseBao.Core.Models;
using QuickResponseBao.Core.Services;

namespace QuickResponseBao.Infrastructure.ImportExport;

public sealed class QuickResponseImportCoordinator(IQuickResponseRepository repository)
{
    public async Task<DetailedImportResult> PersistAsync(ExcelImportOutcome parsed, CancellationToken token = default)
    {
        var existing = await repository.GetAllAsync(token);
        var existingWithSort = existing.Select(x => QuickResponseBusinessKey.Create(x, true)).ToHashSet();
        var existingWithoutSort = existing.Select(x => QuickResponseBusinessKey.Create(x, false)).ToHashSet();
        var seenWithSort = new Dictionary<QuickResponseBusinessKey, int>();
        var seenWithoutSort = new Dictionary<QuickResponseBusinessKey, int>();
        var failures = parsed.Result.Failures.ToList();
        var skippedDetails = parsed.Result.SkippedDetails?.ToList() ?? [];
        var duplicates = parsed.Result.DuplicateSkipped;
        var succeeded = 0;

        foreach (var item in parsed.Items)
        {
            token.ThrowIfCancellationRequested();
            var key = QuickResponseBusinessKey.Create(item.Response, item.IncludesSortOrder);
            var existingKeys = item.IncludesSortOrder ? existingWithSort : existingWithoutSort;
            var seen = item.IncludesSortOrder ? seenWithSort : seenWithoutSort;
            if (existingKeys.Contains(key))
            {
                duplicates++; skippedDetails.Add(new ImportFailure(item.RowNumber, "ImportDuplicateExisting")); continue;
            }
            if (seen.TryGetValue(key, out var referenceRow))
            {
                duplicates++; skippedDetails.Add(new ImportFailure(item.RowNumber, "ImportDuplicateCurrent", referenceRow)); continue;
            }
            try
            {
                await repository.UpsertAsync(item.Response, token); succeeded++;
                seenWithSort.TryAdd(QuickResponseBusinessKey.Create(item.Response, true), item.RowNumber);
                seenWithoutSort.TryAdd(QuickResponseBusinessKey.Create(item.Response, false), item.RowNumber);
            }
            catch (Exception ex)
            {
                failures.Add(new ImportFailure(item.RowNumber, ex.Message));
            }
        }

        var otherSkipped = parsed.Result.OtherSkipped > 0 ? parsed.Result.OtherSkipped : parsed.Result.Skipped;
        return new DetailedImportResult(parsed.Result.Total, succeeded, failures.Count, duplicates + otherSkipped,
            failures, duplicates, otherSkipped, skippedDetails);
    }
}
