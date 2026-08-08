using QuickResponseBao.Core.Models;
using QuickResponseBao.Core.Services;

namespace QuickResponseBao.App.Services;

public sealed record ImportValidationSummary(int Valid, int Duplicate, int Failed, IReadOnlyList<ImportFailure> Details);

public static class ImportValidationService
{
    public static ImportValidationSummary Analyze(ExcelImportOutcome parsed, IReadOnlyList<QuickResponse> existing)
    {
        var existingWithSort = existing.Select(x => QuickResponseBusinessKey.Create(x, true)).ToHashSet();
        var existingWithoutSort = existing.Select(x => QuickResponseBusinessKey.Create(x, false)).ToHashSet();
        var seenWithSort = new Dictionary<QuickResponseBusinessKey, int>();
        var seenWithoutSort = new Dictionary<QuickResponseBusinessKey, int>();
        var details = parsed.Result.Failures.ToList();
        var duplicates = 0;
        var valid = 0;

        foreach (var item in parsed.Items)
        {
            var key = QuickResponseBusinessKey.Create(item.Response, item.IncludesSortOrder);
            var existingKeys = item.IncludesSortOrder ? existingWithSort : existingWithoutSort;
            var seen = item.IncludesSortOrder ? seenWithSort : seenWithoutSort;
            if (existingKeys.Contains(key))
            {
                duplicates++;
                details.Add(new ImportFailure(item.RowNumber, "ImportDuplicateExisting"));
                continue;
            }
            if (seen.TryGetValue(key, out var referenceRow))
            {
                duplicates++;
                details.Add(new ImportFailure(item.RowNumber, "ImportDuplicateCurrent", referenceRow));
                continue;
            }
            seenWithSort.TryAdd(QuickResponseBusinessKey.Create(item.Response, true), item.RowNumber);
            seenWithoutSort.TryAdd(QuickResponseBusinessKey.Create(item.Response, false), item.RowNumber);
            valid++;
        }

        return new ImportValidationSummary(valid, duplicates, parsed.Result.Failed, details);
    }
}
