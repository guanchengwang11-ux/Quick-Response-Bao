using QuickResponseBao.Core.Models;
using QuickResponseBao.Core.Services;

namespace QuickResponseBao.UnitTests;

public sealed class ResponseLibraryFacetIndexTests
{
    private static readonly QuickResponse[] Responses =
    [
        new() { Summary = "Alpha", Category = "Support", Language = "English", IsEnabled = true },
        new() { Summary = "Beta", Category = "Support", Language = "Chinese", IsEnabled = false },
        new() { Summary = "Gamma", Category = "Sales", Language = "English", IsEnabled = true }
    ];

    [Fact]
    public void CategoryFacetCountsAllValuesWhenNoFilterIsActive()
    {
        var index = Create();
        var values = index.GetValues(ResponseFilterField.Category, new ResponseLibraryFilterState());
        Assert.Equal([new FacetValue("Sales", 1), new FacetValue("Support", 2)], values);
    }

    [Fact]
    public void FacetAppliesOtherColumnsButIgnoresItsOwnSelection()
    {
        var index = Create(); var state = new ResponseLibraryFilterState();
        state.Categories.Add("Sales"); state.Languages.Add("English");
        var values = index.GetValues(ResponseFilterField.Category, state);
        Assert.Equal([new FacetValue("Sales", 1), new FacetValue("Support", 1)], values);
    }

    [Fact]
    public void StatusFacetUsesStableMachineValues()
    {
        var values = Create().GetValues(ResponseFilterField.Status, new ResponseLibraryFilterState());
        Assert.Contains(new FacetValue("enabled", 2), values);
        Assert.Contains(new FacetValue("disabled", 1), values);
    }

    [Fact]
    public void GlobalSearchConstrainsFacetCounts()
    {
        var state = new ResponseLibraryFilterState { GlobalSearch = "Beta" };
        Assert.Equal([new FacetValue("Support", 1)], Create().GetValues(ResponseFilterField.Category, state));
    }

    [Fact]
    public void TenThousandRowsBuildAndFacetRemainInteractive()
    {
        var index = new ResponseLibraryFacetIndex();
        var data = Enumerable.Range(0, 10_000).Select(i => new QuickResponse { Summary = $"Response {i}", Category = $"Category {i % 20}", Language = i % 2 == 0 ? "English" : "Chinese" }).ToArray();
        var timer = System.Diagnostics.Stopwatch.StartNew(); index.Rebuild(data); var values = index.GetValues(ResponseFilterField.Category, new ResponseLibraryFilterState()); timer.Stop();
        Assert.Equal(20, values.Count); Assert.True(timer.Elapsed < TimeSpan.FromSeconds(1), $"Facet indexing took {timer.Elapsed}.");
    }

    private static ResponseLibraryFacetIndex Create() { var index = new ResponseLibraryFacetIndex(); index.Rebuild(Responses); return index; }
}
