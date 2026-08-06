using ClosedXML.Excel;
using QuickResponseBao.Core.Models;

namespace QuickResponseBao.Infrastructure.ImportExport;

public sealed class ExcelQuickResponseService
{
    public Task<ImportPreview> PreviewAsync(string path, int maximumPreviewRows = 50, CancellationToken token = default) =>
        Task.Run(() => Preview(path, maximumPreviewRows, token), token);

    public ImportFieldMapping SuggestMapping(IEnumerable<string> headers) => QuickResponseImportParser.SuggestMapping(headers);

    public Task<ExcelImportOutcome> ImportAsync(string path, ImportFieldMapping mapping, CancellationToken token = default) =>
        Task.Run(() => Import(path, mapping, token), token);

    public Task ExportAsync(string path, IEnumerable<QuickResponse> source, bool useChineseHeaders, CancellationToken token = default) =>
        Task.Run(() => Export(path, source.ToList(), useChineseHeaders, token), token);

    private static ImportPreview Preview(string path, int maximumPreviewRows, CancellationToken token)
    {
        EnsureXlsx(path); using var workbook = new XLWorkbook(path); var sheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new InvalidDataException("The workbook does not contain a worksheet.");
        var used = sheet.RangeUsed(); if (used is null) return new ImportPreview([], [], 0);
        var firstRow = used.RangeAddress.FirstAddress.RowNumber; var lastRow = used.RangeAddress.LastAddress.RowNumber;
        var firstColumn = used.RangeAddress.FirstAddress.ColumnNumber; var lastColumn = used.RangeAddress.LastAddress.ColumnNumber;
        var headers = Enumerable.Range(firstColumn, lastColumn - firstColumn + 1).Select(column => sheet.Cell(firstRow, column).GetFormattedString().Trim()).ToList();
        if (headers.Any(string.IsNullOrWhiteSpace)) throw new InvalidDataException("Every used column must have a header.");
        if (headers.Select(QuickResponseImportParser.NormalizeHeader).Distinct(StringComparer.Ordinal).Count() != headers.Count)
            throw new InvalidDataException("Column headers must be unique after normalization.");
        var rows = new List<ImportPreviewRow>(); var total = 0;
        for (var row = firstRow + 1; row <= lastRow; row++)
        {
            token.ThrowIfCancellationRequested();
            var values = headers.Select((header, index) => (header, value: sheet.Cell(row, firstColumn + index).GetFormattedString()))
                .ToDictionary(x => x.header, x => x.value, StringComparer.OrdinalIgnoreCase);
            if (values.Values.All(string.IsNullOrWhiteSpace)) continue;
            total++; if (rows.Count < maximumPreviewRows) rows.Add(new ImportPreviewRow(row, values));
        }
        return new ImportPreview(headers, rows, total);
    }

    private static ExcelImportOutcome Import(string path, ImportFieldMapping mapping, CancellationToken token)
    {
        EnsureRequiredMappings(mapping); using var workbook = new XLWorkbook(path); var sheet = workbook.Worksheets.First();
        var used = sheet.RangeUsed(); if (used is null) return new ExcelImportOutcome([], new DetailedImportResult(0, 0, 0, 0, []));
        var headerRow = used.RangeAddress.FirstAddress.RowNumber; var lastRow = used.RangeAddress.LastAddress.RowNumber;
        var headerColumns = used.FirstRow().Cells().Where(x => !string.IsNullOrWhiteSpace(x.GetFormattedString()))
            .ToDictionary(x => x.GetFormattedString().Trim(), x => x.Address.ColumnNumber, StringComparer.OrdinalIgnoreCase);
        foreach (var column in mapping.Columns.Values)
            if (!headerColumns.ContainsKey(column)) throw new InvalidDataException($"Mapped column '{column}' does not exist.");

        string Value(int row, QuickResponseField field) => mapping.Get(field) is { } name && headerColumns.TryGetValue(name, out var column)
            ? sheet.Cell(row, column).GetFormattedString() : string.Empty;
        var items = new List<ExcelImportItem>(); var failures = new List<ImportFailure>(); var skipped = 0; var total = 0;
        for (var row = headerRow + 1; row <= lastRow; row++)
        {
            token.ThrowIfCancellationRequested();
            if (headerColumns.Values.All(column => string.IsNullOrWhiteSpace(sheet.Cell(row, column).GetFormattedString()))) { skipped++; continue; }
            total++;
            try
            {
                var includesSortOrder = mapping.Get(QuickResponseField.SortOrder) is not null;
                items.Add(new ExcelImportItem(row, QuickResponseImportParser.Create(
                    Value(row, QuickResponseField.Summary), Value(row, QuickResponseField.Content), Value(row, QuickResponseField.Keywords),
                    Value(row, QuickResponseField.Category), Value(row, QuickResponseField.Language), Value(row, QuickResponseField.IsEnabled),
                    Value(row, QuickResponseField.SortOrder), includesSortOrder), includesSortOrder));
            }
            catch (Exception ex) when (ex is InvalidDataException or FormatException)
            {
                failures.Add(new ImportFailure(row, ex.Message));
            }
        }
        return new ExcelImportOutcome(items, new DetailedImportResult(total + skipped, items.Count, failures.Count, skipped, failures));
    }

    private static void Export(string path, IReadOnlyList<QuickResponse> items, bool chinese, CancellationToken token)
    {
        EnsureXlsx(path); token.ThrowIfCancellationRequested(); using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(chinese ? "话术" : "Quick Responses");
        var headers = chinese
            ? new[] { "摘要", "话术正文", "关键词", "分类", "语言", "启用状态", "排序权重" }
            : new[] { "Summary", "Content", "Keywords", "Category", "Language", "IsEnabled", "SortOrder" };
        for (var column = 1; column <= headers.Length; column++) sheet.Cell(1, column).Value = headers[column - 1];
        for (var index = 0; index < items.Count; index++)
        {
            token.ThrowIfCancellationRequested(); var row = index + 2; var item = items[index];
            sheet.Cell(row, 1).Value = item.Summary; sheet.Cell(row, 2).Value = item.Content;
            sheet.Cell(row, 3).Value = string.Join("; ", item.Keywords); sheet.Cell(row, 4).Value = item.Category;
            sheet.Cell(row, 5).Value = item.Language; sheet.Cell(row, 6).Value = item.IsEnabled; sheet.Cell(row, 7).Value = item.SortOrder;
        }
        var lastRow = Math.Max(2, items.Count + 1); var range = sheet.Range(1, 1, lastRow, 7);
        var header = sheet.Range(1, 1, 1, 7); header.Style.Fill.BackgroundColor = XLColor.FromHtml("#2563EB");
        header.Style.Font.FontColor = XLColor.White; header.Style.Font.Bold = true; header.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center; header.Style.Alignment.WrapText = true; header.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top; sheet.Range(2, 1, lastRow, 5).Style.Alignment.WrapText = true;
        sheet.SheetView.FreezeRows(1); sheet.Range(1, 1, Math.Max(1, items.Count + 1), 7).SetAutoFilter();
        sheet.Column(1).Width = 28; sheet.Column(2).Width = 72; sheet.Column(3).Width = 34;
        sheet.Column(4).Width = 22; sheet.Column(5).Width = 18; sheet.Column(6).Width = 14; sheet.Column(7).Width = 14;
        sheet.Row(1).Height = 26;
        if (items.Count > 0)
        {
            sheet.Rows(2, items.Count + 1).AdjustToContents(1, 7);
            foreach (var row in sheet.Rows(2, items.Count + 1)) row.Height = Math.Min(row.Height, 90);
        }
        workbook.SaveAs(path);
    }

    private static void EnsureRequiredMappings(ImportFieldMapping mapping)
    {
        if (string.IsNullOrWhiteSpace(mapping.Get(QuickResponseField.Summary)) || string.IsNullOrWhiteSpace(mapping.Get(QuickResponseField.Content)))
            throw new InvalidDataException("Summary and Content columns must be mapped.");
    }

    private static void EnsureXlsx(string path)
    {
        if (!Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase)) throw new NotSupportedException("Only .xlsx workbooks are supported.");
    }

}
