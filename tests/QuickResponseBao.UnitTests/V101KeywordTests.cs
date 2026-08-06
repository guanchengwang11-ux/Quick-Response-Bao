using ClosedXML.Excel;
using QuickResponseBao.Core.Models;
using QuickResponseBao.Core.Services;
using QuickResponseBao.Infrastructure.ImportExport;

namespace QuickResponseBao.UnitTests;

public sealed class V101KeywordTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"qrb-v101-keywords-{Guid.NewGuid():N}");
    public V101KeywordTests() => Directory.CreateDirectory(_root);

    [Theory][InlineData("txid")][InlineData("TXID")][InlineData("trans")]
    public void Keyword_SearchMatchesExactCaseInsensitiveAndPartial(string query)
    {
        var item = new QuickResponse { Summary = "Request transaction hash", Content = "Please provide the transaction hash.", Keywords = ["txid", "hash", "transaction"] };
        Assert.Single(new SearchService().Search([item], query));
    }

    [Fact]
    public void Keyword_SearchSwitchDisablesKeywordOnlyMatch()
    {
        var item = new QuickResponse { Summary = "Unrelated summary", Content = "Unrelated content", Keywords = ["txid"] };
        Assert.Empty(new SearchService().Search([item], "txid", new SearchOptions(MatchKeywords: false)));
    }

    [Fact]
    public void KeywordParser_SupportsAllSeparatorsTrimmingAndCaseInsensitiveDeduplication()
    {
        var parsed = KeywordNormalizer.Parse(" txid ;HASH； transaction,hash，proof\n receipt \r\nTXID ");
        Assert.Equal(["txid", "HASH", "transaction", "proof", "receipt"], parsed);
    }

    [Fact]
    public void KeywordParser_SupportsExcelArrayFormat()
    {
        Assert.Equal(["txid", "hash"], KeywordNormalizer.Parse("[\"txid\", \" hash \", \"TXID\"]"));
    }

    [Theory][InlineData("Keyword")][InlineData("Keywords")][InlineData("Key Word")][InlineData(" Key   Words ")][InlineData("关键词")]
    public void KeywordHeaderAliases_MapIgnoringCaseAndWhitespace(string header)
    {
        var mapping = QuickResponseImportParser.SuggestMapping(["Summary", "Content", header.ToLowerInvariant()]);
        Assert.Equal(header.ToLowerInvariant(), mapping.Get(QuickResponseField.Keywords));
    }

    [Fact]
    public async Task Excel_KeyWordColumnMapsAndDatabaseRoundTripKeepsNormalizedKeywords()
    {
        var path = Path.Combine(_root, "key-word.xlsx");
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.Worksheets.Add("Data");
            sheet.Cell(1, 1).Value = "Summary"; sheet.Cell(1, 2).Value = "Content"; sheet.Cell(1, 3).Value = "Key Word";
            sheet.Cell(2, 1).Value = "Transaction hash"; sheet.Cell(2, 2).Value = "Provide it"; sheet.Cell(2, 3).Value = "txid， hash;TXID";
            workbook.SaveAs(path);
        }
        var excel = new ExcelQuickResponseService(); var preview = await excel.PreviewAsync(path);
        var outcome = await excel.ImportAsync(path, excel.SuggestMapping(preview.Headers));
        using var workspace = new TestWorkspace(); await workspace.Repository.InitializeAsync();
        await workspace.Repository.UpsertAsync(outcome.Items.Single().Response);
        Assert.Equal(["txid", "hash"], (await workspace.Repository.GetAllAsync()).Single().Keywords);
    }

    [Fact]
    public async Task ExcelExportAndReimport_PreservesAllKeywordsAndSortOrder()
    {
        var path = Path.Combine(_root, "roundtrip.xlsx"); var source = new QuickResponse
        { Summary = "Round trip", Content = "Content", Keywords = ["hash", "txid"], SortOrder = 7 };
        var excel = new ExcelQuickResponseService(); await excel.ExportAsync(path, [source], false);
        var preview = await excel.PreviewAsync(path); var result = await excel.ImportAsync(path, excel.SuggestMapping(preview.Headers));
        Assert.Equal(source.Keywords, result.Items.Single().Response.Keywords); Assert.Equal(7, result.Items.Single().Response.SortOrder);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
