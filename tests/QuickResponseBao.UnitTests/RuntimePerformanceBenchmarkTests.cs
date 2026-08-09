using System.Diagnostics;
using QuickResponseBao.Core.Models;
using QuickResponseBao.Core.Services;
using Xunit.Abstractions;

namespace QuickResponseBao.UnitTests;

public sealed class RuntimePerformanceBenchmarkTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1_000)]
    [InlineData(10_000)]
    public void SearchAndFacetedFilteringRemainInteractive(int size)
    {
        var data = Enumerable.Range(0, size).Select(index => new QuickResponse
        {
            Summary = $"Account risk review {index}", Content = "How to solve this account compliance problem quickly",
            Keywords = ["account", "risk", $"group-{index % 25}"], Category = $"Category {index % 20}",
            Language = index % 2 == 0 ? "English" : "Chinese", IsEnabled = index % 3 != 0, UsageCount = index % 50
        }).ToArray();
        var search = new SearchService(); var index = new ResponseLibraryFacetIndex(); index.Rebuild(data);
        var state = new ResponseLibraryFilterState { GlobalSearch = "risk" }; state.Languages.Add("English");
        search.Search(data, "account risk", new SearchOptions(true, true, true, true, false, true, 30));
        index.GetValues(ResponseFilterField.Category, state);
        var searchTimes = Measure(30, () => search.Search(data, "account risk", new SearchOptions(true, true, true, true, false, true, 30)));
        var filterTimes = Measure(30, () => index.GetValues(ResponseFilterField.Category, state));
        output.WriteLine($"rows={size}; candidateSearch P50={Percentile(searchTimes, .50):F3}ms P95={Percentile(searchTimes, .95):F3}ms; facet P50={Percentile(filterTimes, .50):F3}ms P95={Percentile(filterTimes, .95):F3}ms");
        Assert.True(Percentile(searchTimes, .95) < 250, $"Search P95 exceeded the regression budget for {size} rows.");
        Assert.True(Percentile(filterTimes, .95) < 250, $"Facet P95 exceeded the regression budget for {size} rows.");
    }

    private static double[] Measure(int iterations, Action action)
    {
        var values = new double[iterations];
        for (var iteration = 0; iteration < iterations; iteration++) { var timer = Stopwatch.StartNew(); action(); timer.Stop(); values[iteration] = timer.Elapsed.TotalMilliseconds; }
        Array.Sort(values); return values;
    }
    private static double Percentile(double[] sorted, double percentile) => sorted[Math.Clamp((int)Math.Ceiling(sorted.Length * percentile) - 1, 0, sorted.Length - 1)];
}
