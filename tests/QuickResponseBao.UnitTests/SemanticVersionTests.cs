using QuickResponseBao.Core.Services;

namespace QuickResponseBao.UnitTests;

public sealed class SemanticVersionTests
{
    [Theory][InlineData("v1.0.0", "1.0.0")][InlineData("1.2.3-beta", "1.2.3")]
    public void Parse_NormalizesVersion(string input, string expected)
    { Assert.True(SemanticVersion.TryParse(input, out var value)); Assert.Equal(expected, value.ToString()); }
}
