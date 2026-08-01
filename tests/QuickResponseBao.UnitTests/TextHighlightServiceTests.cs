using QuickResponseBao.Core.Services;

namespace QuickResponseBao.UnitTests;

public sealed class TextHighlightServiceTests
{
    [Fact]
    public void Split_HighlightsAllMatches_IgnoringCase()
    {
        var parts = new TextHighlightService().Split("Withdrawal WITHDRAWAL", "withdrawal");
        Assert.Equal(2, parts.Count(x => x.IsMatch));
        Assert.Equal("Withdrawal WITHDRAWAL", string.Concat(parts.Select(x => x.Text)));
    }
}
