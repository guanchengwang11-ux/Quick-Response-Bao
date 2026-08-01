using QuickResponseBao.Core.Models;
using QuickResponseBao.Core.Services;

namespace QuickResponseBao.UnitTests;

public sealed class SearchServiceTests
{
    private readonly SearchService _service = new();
    private static QuickResponse Item(string summary, string content = "", params string[] keywords) =>
        new() { Summary = summary, Content = content, Keywords = [.. keywords] };

    [Fact]
    public void Search_IsCaseInsensitive_AndMatchesEverySupportedField()
    {
        var source = new[] { Item("Withdrawal pending", "Under review", "delay"), Item("Other", "WITHDRAWAL completed") };
        Assert.Equal(2, _service.Search(source, "withdrawal").Count);
        var keywordOnly = Item("Other", "Other", "withdrawal");
        Assert.Single(_service.Search(new[] { keywordOnly }, "Withdrawal"));
        var categoryOnly = Item("Other", "Other"); categoryOnly.Category = "Withdrawal";
        Assert.Single(_service.Search(new[] { categoryOnly }, "withdrawal"));
    }

    [Fact]
    public void ExactSummary_RanksBeforeContains_ThenUsageBreaksTies()
    {
        var exact = Item("withd"); var popular = Item("A withd answer"); popular.UsageCount = 100;
        Assert.Same(exact, _service.Search(new[] { popular, exact }, "withd")[0].Response);
        var low = Item("withd low"); var high = Item("withd high"); high.UsageCount = 5;
        Assert.Same(high, _service.Search(new[] { low, high }, "withd")[0].Response);
    }

    [Fact]
    public void DisabledResponses_AreExcluded()
    {
        var item = Item("withdrawal"); item.IsEnabled = false;
        Assert.Empty(_service.Search(new[] { item }, "withd"));
    }
}
