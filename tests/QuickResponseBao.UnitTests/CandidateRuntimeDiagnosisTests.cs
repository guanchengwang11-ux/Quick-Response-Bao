using QuickResponseBao.Core.Models;
using QuickResponseBao.Core.Services;
using QuickResponseBao.Infrastructure.Diagnostics;

namespace QuickResponseBao.UnitTests;

public sealed class CandidateRuntimeDiagnosisTests
{
    [Fact]
    public void ResponseMatchingEveryFieldStillProducesOneSearchResult()
    {
        var response = new QuickResponse
        {
            Summary = "risk summary", Content = "risk content", Keywords = ["risk"], Category = "risk category"
        };

        var results = new SearchService().Search([response], "risk", new SearchOptions(
            MatchSummary: true, MatchContent: true, MatchKeywords: true, MatchCategory: true));

        Assert.Single(results);
        Assert.Same(response, results[0].Response);
    }

    [Fact]
    public void SearchSequenceIsPreservedIntoConfirmationContext()
    {
        var search = new CandidateSearchContext("risk", 4, (nint)123, 456, "Lark.exe", DateTimeOffset.UtcNow, "risk", 101);
        var confirmation = search.Confirm(new QuickResponse { Summary = "Risk", Content = "Response" }, CandidateConfirmationMethod.Enter);
        Assert.Equal(101, confirmation.SequenceId);
    }

    [Fact]
    public void RuntimeTraceContainsSequenceAndStageWithoutRawTypedText()
    {
        var trace = new CandidateRuntimeTrace(101, "ShowSuggestions entered", "query='risk'; targetHWND=0x123").ToString();
        Assert.Contains("SearchSequence #101", trace);
        Assert.Contains("ShowSuggestions entered", trace);
        Assert.DoesNotContain("RawTypedText", trace);
    }
}
