using ClosedXML.Excel;
using QuickResponseBao.Core.Models;
using QuickResponseBao.Infrastructure.ImportExport;

namespace QuickResponseBao.UnitTests;

public sealed class ExcelQuickResponseServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"qrb-excel-{Guid.NewGuid():N}");
    private readonly ExcelQuickResponseService _service = new();
    public ExcelQuickResponseServiceTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task Import_ReadsValidRowsWithSuggestedMapping()
    {
        var path = CreateWorkbook(["Summary", "Content", "Keywords", "Category", "Language", "IsEnabled"],
            ["Valid reply", "Complete text", "one;two", "General", "English", "true"]);
        var preview = await _service.PreviewAsync(path); var outcome = await _service.ImportAsync(path, _service.SuggestMapping(preview.Headers));
        Assert.Equal(1, outcome.Result.Succeeded); Assert.Empty(outcome.Result.Failures);
        Assert.Equal(["one", "two"], outcome.Items[0].Response.Keywords); Assert.True(outcome.Items[0].Response.IsEnabled);
    }

    [Fact]
    public async Task Import_ReportsInvalidRowWithoutDiscardingValidRows()
    {
        var path = CreateWorkbook(["Summary", "Content", "IsEnabled"],
            ["Valid reply", "Complete text", "true"], ["x", "", "maybe"]);
        var preview = await _service.PreviewAsync(path); var outcome = await _service.ImportAsync(path, _service.SuggestMapping(preview.Headers));
        Assert.Equal(2, outcome.Result.Total); Assert.Equal(1, outcome.Result.Succeeded); Assert.Equal(1, outcome.Result.Failed);
        Assert.Equal(3, outcome.Result.Failures[0].RowNumber);
    }

    [Fact]
    public async Task ExportedWorkbook_CanBeImportedAgain()
    {
        var path = Path.Combine(_root, "roundtrip.xlsx");
        var source = new[] { new QuickResponse { Summary = "Multiline reply", Content = "Line 1\nLine 2", Keywords = ["line"], Category = "Technical Issue", IsEnabled = false } };
        await _service.ExportAsync(path, source, false);
        var preview = await _service.PreviewAsync(path); var outcome = await _service.ImportAsync(path, _service.SuggestMapping(preview.Headers));
        Assert.Single(outcome.Items); Assert.Equal(source[0].Content, outcome.Items[0].Response.Content); Assert.False(outcome.Items[0].Response.IsEnabled);
    }

    private string CreateWorkbook(string[] headers, params string[][] rows)
    {
        var path = Path.Combine(_root, $"input-{Guid.NewGuid():N}.xlsx"); using var workbook = new XLWorkbook(); var sheet = workbook.Worksheets.Add("Data");
        for (var column = 0; column < headers.Length; column++) sheet.Cell(1, column + 1).Value = headers[column];
        for (var row = 0; row < rows.Length; row++) for (var column = 0; column < rows[row].Length; column++) sheet.Cell(row + 2, column + 1).Value = rows[row][column];
        workbook.SaveAs(path); return path;
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
