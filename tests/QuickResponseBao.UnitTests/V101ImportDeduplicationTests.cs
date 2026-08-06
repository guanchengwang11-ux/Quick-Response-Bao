using QuickResponseBao.Core.Models;
using QuickResponseBao.Infrastructure.ImportExport;

namespace QuickResponseBao.UnitTests;

public sealed class V101ImportDeduplicationTests
{
    [Fact]
    public async Task ExistingExactRecord_IsSkippedWithoutChangingSystemFields()
    {
        using var workspace = new TestWorkspace(); await workspace.Repository.InitializeAsync();
        var existing = Item(); existing.UsageCount = 9; existing.UpdatedAt = DateTimeOffset.UtcNow.AddDays(-2); await workspace.Repository.UpsertAsync(existing);
        var saved = (await workspace.Repository.GetAsync(existing.Id))!;
        var result = await Coordinator(workspace).PersistAsync(Outcome(new ExcelImportItem(2, Item())));
        var after = (await workspace.Repository.GetAsync(existing.Id))!;
        Assert.Equal(0, result.Succeeded); Assert.Equal(1, result.DuplicateSkipped); Assert.Equal(saved.UsageCount, after.UsageCount); Assert.Equal(saved.UpdatedAt, after.UpdatedAt);
    }

    [Fact]
    public async Task OneCharacterContentDifference_IsImported()
    {
        using var workspace = new TestWorkspace(); await workspace.Repository.InitializeAsync(); await workspace.Repository.UpsertAsync(Item());
        var changed = Item(); changed.Content += "!";
        var result = await Coordinator(workspace).PersistAsync(Outcome(new ExcelImportItem(2, changed)));
        Assert.Equal(1, result.Succeeded); Assert.Equal(2, (await workspace.Repository.GetAllAsync()).Count);
    }

    [Fact]
    public async Task KeywordOrderAndCase_AreIgnoredForDuplicateComparison()
    {
        using var workspace = new TestWorkspace(); await workspace.Repository.InitializeAsync(); await workspace.Repository.UpsertAsync(Item());
        var reordered = Item(); reordered.Keywords = ["PENDING", "WITHDRAWAL"];
        var result = await Coordinator(workspace).PersistAsync(Outcome(new ExcelImportItem(2, reordered)));
        Assert.Equal(1, result.DuplicateSkipped);
    }

    [Fact]
    public async Task DuplicateRowsWithinFile_ImportOnceAndReportOriginalRow()
    {
        using var workspace = new TestWorkspace(); await workspace.Repository.InitializeAsync();
        var result = await Coordinator(workspace).PersistAsync(Outcome(new ExcelImportItem(5, Item()), new ExcelImportItem(8, Item()), new ExcelImportItem(11, Item())));
        Assert.Equal(1, result.Succeeded); Assert.Equal(2, result.DuplicateSkipped);
        Assert.All(result.SkippedDetails!, detail => Assert.Equal(5, detail.ReferenceRow));
    }

    [Fact]
    public async Task CategoryCaseLanguageAliasAndEnabledExpression_NormalizeBeforeDeduplication()
    {
        using var workspace = new TestWorkspace(); await workspace.Repository.InitializeAsync(); await workspace.Repository.UpsertAsync(Item());
        var alias = Item(); alias.Category = " payment "; alias.Language = "en-US";
        var result = await Coordinator(workspace).PersistAsync(Outcome(new ExcelImportItem(2, alias)));
        Assert.Equal(1, result.DuplicateSkipped);
    }

    [Fact]
    public async Task SortOrder_IsComparedOnlyWhenSourceContainsIt()
    {
        using var workspace = new TestWorkspace(); await workspace.Repository.InitializeAsync(); var existing = Item(); existing.SortOrder = 7; await workspace.Repository.UpsertAsync(existing);
        var imported = Item(); imported.SortOrder = 8;
        var withoutSort = await Coordinator(workspace).PersistAsync(Outcome(new ExcelImportItem(2, imported, false)));
        Assert.Equal(1, withoutSort.DuplicateSkipped);
        var withSort = await Coordinator(workspace).PersistAsync(Outcome(new ExcelImportItem(3, imported, true)));
        Assert.Equal(1, withSort.Succeeded);
    }

    [Fact]
    public async Task CsvAliasAndNormalizedValues_UseTheSameDuplicateRules()
    {
        using var workspace = new TestWorkspace(); await workspace.Repository.InitializeAsync(); await workspace.Repository.UpsertAsync(Item());
        var path = Path.Combine(workspace.Root, "responses.csv");
        await File.WriteAllTextAsync(path,
            "Summary,Content,Key Words,Category,Language,Enabled\r\n" +
            "Withdrawal pending,Your withdrawal is currently under review.,PENDING;withdrawal,payment,en-US,YES\r\n");

        var parsed = await new QuickResponseFileService().ImportCsvOutcomeAsync(path);
        var result = await Coordinator(workspace).PersistAsync(parsed);

        Assert.Equal(0, result.Succeeded);
        Assert.Equal(1, result.DuplicateSkipped);
        Assert.Single(await workspace.Repository.GetAllAsync());
    }

    [Fact]
    public async Task JsonKeywordArray_UsesTheSameDuplicateRules()
    {
        using var workspace = new TestWorkspace(); await workspace.Repository.InitializeAsync(); await workspace.Repository.UpsertAsync(Item());
        var path = Path.Combine(workspace.Root, "responses.json");
        await File.WriteAllTextAsync(path, """
            [{
              "Summary": "Withdrawal pending",
              "Content": "Your withdrawal is currently under review.",
              "关键词": ["PENDING", "withdrawal"],
              "Category": "PAYMENT",
              "Language": "English",
              "Enabled": true
            }]
            """);

        var parsed = await new QuickResponseFileService().ImportJsonOutcomeAsync(path);
        var result = await Coordinator(workspace).PersistAsync(parsed);

        Assert.Equal(0, result.Succeeded);
        Assert.Equal(1, result.DuplicateSkipped);
        Assert.Single(await workspace.Repository.GetAllAsync());
    }

    private static QuickResponseImportCoordinator Coordinator(TestWorkspace workspace) => new(workspace.Repository);
    private static ExcelImportOutcome Outcome(params ExcelImportItem[] items) => new(items,
        new DetailedImportResult(items.Length, items.Length, 0, 0, []));
    private static QuickResponse Item() => new()
    {
        Summary = "Withdrawal pending", Content = "Your withdrawal is currently under review.",
        Keywords = ["withdrawal", "pending"], Category = "Payment", Language = "English", IsEnabled = true
    };
}
