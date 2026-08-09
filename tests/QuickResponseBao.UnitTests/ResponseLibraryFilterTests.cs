using System.Diagnostics;
using QuickResponseBao.Core.Models;
using Xunit.Abstractions;

namespace QuickResponseBao.UnitTests;

public sealed class ResponseLibraryFilterTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(TextFilterOperator.Contains, "risk", true)]
    [InlineData(TextFilterOperator.DoesNotContain, "payment", true)]
    [InlineData(TextFilterOperator.Equals, "Risk review", true)]
    [InlineData(TextFilterOperator.StartsWith, "risk", true)]
    [InlineData(TextFilterOperator.EndsWith, "review", true)]
    public void SummaryOperators_WorkCaseInsensitively(TextFilterOperator op, string query, bool expected)
    {
        var state = new ResponseLibraryFilterState { Summary = new TextColumnFilter(query, op) };
        Assert.Equal(expected, state.Matches(Response()));
    }

    [Theory][InlineData("risk", TextFilterOperator.Contains)][InlineData("HIGH-RISK", TextFilterOperator.Equals)]
    public void KeywordFilter_MatchesAnyKeywordWithoutCaseSensitivity(string query, TextFilterOperator op)
    {
        var state = new ResponseLibraryFilterState { Keywords = new TextColumnFilter(query, op) };
        Assert.True(state.Matches(Response()));
    }

    [Fact] public void CategoryMultiSelect_UsesOrWithinField() { var state = new ResponseLibraryFilterState(); state.Categories.UnionWith(["Payment", "Risk"]); Assert.True(state.Matches(Response())); }
    [Fact] public void LanguageMultiSelect_UsesOrWithinField() { var state = new ResponseLibraryFilterState(); state.Languages.UnionWith(["Chinese", "English"]); Assert.True(state.Matches(Response())); }
    [Fact] public void StatusFilter_MatchesSelectedState() { var state = new ResponseLibraryFilterState(); state.Statuses.Add(true); Assert.True(state.Matches(Response())); state.Statuses.Clear(); state.Statuses.Add(false); Assert.False(state.Matches(Response())); }

    [Theory]
    [InlineData(NumberFilterOperator.Equals, 12, null, true)]
    [InlineData(NumberFilterOperator.GreaterThan, 10, null, true)]
    [InlineData(NumberFilterOperator.GreaterThanOrEqual, 12, null, true)]
    [InlineData(NumberFilterOperator.LessThan, 20, null, true)]
    [InlineData(NumberFilterOperator.LessThanOrEqual, 12, null, true)]
    [InlineData(NumberFilterOperator.Between, 10, 15, true)]
    public void UsageOperators_FilterNumerically(NumberFilterOperator op, int value, int? second, bool expected)
    {
        var state = new ResponseLibraryFilterState { UsageCount = new NumberColumnFilter(op, value, second) }; Assert.Equal(expected, state.Matches(Response()));
    }

    [Theory]
    [InlineData(LastUsedFilterPeriod.Today, 0, true)]
    [InlineData(LastUsedFilterPeriod.Last7Days, -6, true)]
    [InlineData(LastUsedFilterPeriod.Last30Days, -29, true)]
    [InlineData(LastUsedFilterPeriod.Last7Days, -8, false)]
    public void RelativeLastUsedPeriods_AreInclusive(LastUsedFilterPeriod period, int days, bool expected)
    {
        var now = new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero); var state = new ResponseLibraryFilterState { Now = now, LastUsed = new LastUsedColumnFilter(period) };
        Assert.Equal(expected, state.Matches(Response(lastUsed: now.AddDays(days))));
    }
    [Fact] public void NeverUsed_MatchesNullOnly() { var state = new ResponseLibraryFilterState { LastUsed = new LastUsedColumnFilter(LastUsedFilterPeriod.NeverUsed) }; var never = Response(); never.LastUsedAt = null; Assert.True(state.Matches(never)); Assert.False(state.Matches(Response())); }
    [Fact] public void CustomDateRange_IsInclusive() { var from = DateTimeOffset.Now.AddDays(-4); var to = DateTimeOffset.Now; var state = new ResponseLibraryFilterState { LastUsed = new LastUsedColumnFilter(LastUsedFilterPeriod.CustomRange, from, to) }; Assert.True(state.Matches(Response(lastUsed: from.AddDays(2)))); }

    [Fact]
    public void DifferentFields_CombineWithAnd()
    {
        var state = new ResponseLibraryFilterState { Summary = new TextColumnFilter("risk"), UsageCount = new NumberColumnFilter(NumberFilterOperator.GreaterThan, 5) }; state.Categories.Add("Risk"); state.Languages.Add("English"); state.Statuses.Add(true);
        Assert.True(state.Matches(Response())); state.Languages.Clear(); state.Languages.Add("Chinese"); Assert.False(state.Matches(Response()));
    }
    [Fact] public void GlobalSearch_CombinesWithColumnFilterUsingAnd() { var state = new ResponseLibraryFilterState { GlobalSearch = "withdrawal", Summary = new TextColumnFilter("risk") }; Assert.True(state.Matches(Response(content: "Withdrawal is pending"))); state.Categories.Add("VIP"); Assert.False(state.Matches(Response(content: "Withdrawal is pending"))); }
    [Fact] public void ClearSingleField_PreservesOtherFilters() { var state = new ResponseLibraryFilterState { Summary = new TextColumnFilter("risk"), UsageCount = new NumberColumnFilter(NumberFilterOperator.GreaterThan, 5) }; state.Clear(ResponseFilterField.Summary); Assert.Null(state.Summary); Assert.NotNull(state.UsageCount); }
    [Fact] public void ClearAll_RemovesEveryColumnFilterButKeepsGlobalSearch() { var state = new ResponseLibraryFilterState { GlobalSearch = "risk", Summary = new TextColumnFilter("risk"), UsageCount = new NumberColumnFilter(NumberFilterOperator.GreaterThan, 5), LastUsed = new LastUsedColumnFilter(LastUsedFilterPeriod.Today) }; state.Categories.Add("Risk"); state.ClearAll(); Assert.False(state.HasColumnFilters); Assert.Equal("risk", state.GlobalSearch); }

    [Fact]
    public void TenThousandRows_FiveFiltersAndClearAllStayInteractive()
    {
        var rows = Enumerable.Range(0, 10_000).Select(index => Response($"Risk review {index}", index % 2 == 0 ? "Risk" : "Payment", index % 30, DateTimeOffset.Now.AddDays(-(index % 40)))).ToArray();
        var state = new ResponseLibraryFilterState { GlobalSearch = "risk", Summary = new TextColumnFilter("review"), Keywords = new TextColumnFilter("risk"), UsageCount = new NumberColumnFilter(NumberFilterOperator.GreaterThan, 5), LastUsed = new LastUsedColumnFilter(LastUsedFilterPeriod.Last30Days) }; state.Categories.UnionWith(["Risk", "Payment"]); state.Languages.Add("English");
        var singleState = new ResponseLibraryFilterState { Summary = new TextColumnFilter("risk") }; var one = Stopwatch.StartNew(); _ = rows.Count(singleState.Matches); one.Stop();
        var five = Stopwatch.StartNew(); var count = rows.Count(state.Matches); five.Stop();
        var clear = Stopwatch.StartNew(); state.ClearAll(); var all = rows.Count(state.Matches); clear.Stop();
        output.WriteLine($"single={one.Elapsed.TotalMilliseconds:F2}ms five={five.Elapsed.TotalMilliseconds:F2}ms clear={clear.Elapsed.TotalMilliseconds:F2}ms results={count}");
        Assert.Equal(10_000, all); Assert.True(one.ElapsedMilliseconds < 100); Assert.True(five.ElapsedMilliseconds < 100); Assert.True(clear.ElapsedMilliseconds < 100);
    }

    [Fact]
    public void ColumnFilterUi_UsesPopupActiveBarAndAccentState()
    {
        var xaml = Read("src", "QuickResponseBao.App", "Views", "Pages", "ResponseLibraryPage.xaml"); var code = Read("src", "QuickResponseBao.App", "Views", "Pages", "ResponseLibraryPage.xaml.cs");
        Assert.Contains("StaysOpen=\"False\"", xaml); Assert.Contains("ActiveFiltersBar", xaml); Assert.Contains("QrbAccentBrush", code); Assert.Contains("e.Handled = true", code);
    }

    private static QuickResponse Response(string summary = "Risk review", string category = "Risk", int usage = 12, DateTimeOffset? lastUsed = default, string content = "Please provide the withdrawal ID") => new() { Summary = summary, Content = content, Category = category, Language = "English", Keywords = ["HIGH-RISK", "control"], IsEnabled = true, UsageCount = usage, LastUsedAt = lastUsed == default ? DateTimeOffset.Now : lastUsed };
    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([Root(), .. parts]));
    private static string Root() { var directory = new DirectoryInfo(AppContext.BaseDirectory); while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Directory.Build.props"))) directory = directory.Parent; return directory?.FullName ?? throw new DirectoryNotFoundException(); }
}
