using ClosedXML.Excel;
using QuickResponseBao.Core.Models;
using QuickResponseBao.Core.Services;
using QuickResponseBao.Infrastructure.ImportExport;

namespace QuickResponseBao.UnitTests;

public sealed class ContentLengthRulesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"qrb-content-rules-{Guid.NewGuid():N}");

    public ContentLengthRulesTests() => Directory.CreateDirectory(_root);

    [Fact] public void FiveHundredNinetyNineEnglishWords_AreValid() => Assert.True(QuickResponseRules.IsContentValid(Words(599)));
    [Fact] public void SixHundredEnglishWords_AreValid() => Assert.True(QuickResponseRules.IsContentValid(Words(600)));
    [Fact] public void SixHundredAndOneEnglishWords_AreRejected() => Assert.False(QuickResponseRules.IsContentValid(Words(601)));

    [Fact]
    public void MultipleWhitespaceCharacters_CountAsOneSeparator()
    {
        var metrics = QuickResponseRules.GetContentMetrics("one   two\t\tthree\r\n four");
        Assert.Equal(4, metrics.WordCount); Assert.Equal(0, metrics.CjkCharacterCount);
    }

    [Fact]
    public void NewLineSeparatedEnglishWords_AreCounted()
    {
        Assert.Equal(3, QuickResponseRules.GetContentMetrics("one\ntwo\r\nthree").WordCount);
    }

    [Fact]
    public void PunctuationAttachedToWords_DoesNotCreateExtraWords()
    {
        Assert.Equal(3, QuickResponseRules.GetContentMetrics("hello, problem. withdrawal?").WordCount);
    }

    [Fact] public void TwoThousandNineHundredNinetyNineCjkCharacters_AreValid() => Assert.True(QuickResponseRules.IsContentValid(new string('中', 2999)));
    [Fact] public void ThreeThousandCjkCharacters_AreValid() => Assert.True(QuickResponseRules.IsContentValid(new string('中', 3000)));
    [Fact] public void ThreeThousandAndOneCjkCharacters_AreRejected() => Assert.False(QuickResponseRules.IsContentValid(new string('中', 3001)));

    [Fact]
    public void MixedContent_CountsWordsAndCjkCharactersIndependently()
    {
        var metrics = QuickResponseRules.GetContentMetrics("hello 世界\nwithdrawal? 한국어");
        Assert.Equal(2, metrics.WordCount); Assert.Equal(5, metrics.CjkCharacterCount);
        Assert.False(QuickResponseRules.IsContentValid(Words(601) + " " + new string('中', 3001)));
    }

    [Fact]
    public async Task Excel_SixHundredWords_ImportsSuccessfully()
    {
        var path = Path.Combine(_root, "valid.xlsx");
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.Worksheets.Add("Data"); sheet.Cell(1, 1).Value = "Summary"; sheet.Cell(1, 2).Value = "Content";
            sheet.Cell(2, 1).Value = "Valid"; sheet.Cell(2, 2).Value = Words(600);
            workbook.SaveAs(path);
        }
        var service = new ExcelQuickResponseService(); var preview = await service.PreviewAsync(path);
        var result = await service.ImportAsync(path, service.SuggestMapping(preview.Headers));
        Assert.Single(result.Items); Assert.Empty(result.Result.Failures);
    }

    [Fact]
    public async Task Excel_SixHundredAndOneWords_ReportsValidationFailure()
    {
        var path = Path.Combine(_root, "invalid.xlsx");
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.Worksheets.Add("Data"); sheet.Cell(1, 1).Value = "Summary"; sheet.Cell(1, 2).Value = "Content";
            sheet.Cell(2, 1).Value = "Invalid"; sheet.Cell(2, 2).Value = Words(601); workbook.SaveAs(path);
        }
        var service = new ExcelQuickResponseService(); var preview = await service.PreviewAsync(path);
        var result = await service.ImportAsync(path, service.SuggestMapping(preview.Headers));
        Assert.Empty(result.Items); Assert.Single(result.Result.Failures);
        Assert.Equal("ImportContentWordLimitExceeded", result.Result.Failures[0].Reason);
    }

    [Fact]
    public async Task Csv_UsesTheSameContentRules()
    {
        var path = Path.Combine(_root, "limits.csv");
        await File.WriteAllTextAsync(path, $"Summary,Content\r\nValid,\"{Words(600)}\"\r\nInvalid,\"{Words(601)}\"\r\n");
        var result = await new QuickResponseFileService().ImportCsvOutcomeAsync(path);
        Assert.Single(result.Items); Assert.Single(result.Result.Failures);
        Assert.Equal("ImportContentWordLimitExceeded", result.Result.Failures[0].Reason);
    }

    [Fact]
    public async Task Json_UsesTheSameContentRules()
    {
        var path = Path.Combine(_root, "limits.json");
        await File.WriteAllTextAsync(path, $$"""[{ "Summary": "Valid", "Content": "{{Words(600)}}" }, { "Summary": "Invalid", "Content": "{{Words(601)}}" }]""");
        var result = await new QuickResponseFileService().ImportJsonOutcomeAsync(path);
        Assert.Single(result.Items); Assert.Single(result.Result.Failures);
        Assert.Equal("ImportContentWordLimitExceeded", result.Result.Failures[0].Reason);
    }

    [Fact]
    public async Task EditingLegacyLongContent_DoesNotTruncateIt()
    {
        using var workspace = new TestWorkspace(); await workspace.Repository.InitializeAsync();
        var content = new string('x', 1000);
        var item = new QuickResponse { Summary = "Legacy content", Content = content };
        await workspace.Repository.UpsertAsync(item);
        item.Summary = "Edited legacy content"; await workspace.Repository.UpsertAsync(item);
        Assert.Equal(content, (await workspace.Repository.GetAsync(item.Id))!.Content);
    }

    [Fact]
    public async Task ExportThenImport_PreservesLongContentVerbatim()
    {
        var path = Path.Combine(_root, "roundtrip.json"); var content = Words(600) + "\n" + new string('中', 3000);
        var files = new QuickResponseFileService();
        await files.ExportJsonAsync(path, [new QuickResponse { Summary = "Round trip", Content = content }]);
        var result = await files.ImportJsonOutcomeAsync(path);
        Assert.Equal(content, Assert.Single(result.Items).Response.Content);
    }

    private static string Words(int count) => string.Join(' ', Enumerable.Repeat("word", count));
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
