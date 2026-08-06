namespace QuickResponseBao.Core.Models;

public enum QuickResponseField { Summary, Content, Keywords, Category, Language, IsEnabled, SortOrder }

public sealed record ImportPreview(
    IReadOnlyList<string> Headers,
    IReadOnlyList<ImportPreviewRow> Rows,
    int TotalRows);

public sealed record ImportPreviewRow(int RowNumber, IReadOnlyDictionary<string, string> Values);

public sealed record ImportFieldMapping(IReadOnlyDictionary<QuickResponseField, string> Columns)
{
    public string? Get(QuickResponseField field) => Columns.TryGetValue(field, out var value) ? value : null;
}

public sealed record ImportFailure(int RowNumber, string Reason, int? ReferenceRow = null);

public sealed record DetailedImportResult(
    int Total,
    int Succeeded,
    int Failed,
    int Skipped,
    IReadOnlyList<ImportFailure> Failures,
    int DuplicateSkipped = 0,
    int OtherSkipped = 0,
    IReadOnlyList<ImportFailure>? SkippedDetails = null);

public sealed record ExcelImportItem(int RowNumber, QuickResponse Response, bool IncludesSortOrder = false);

public sealed record ExcelImportOutcome(
    IReadOnlyList<ExcelImportItem> Items,
    DetailedImportResult Result);
