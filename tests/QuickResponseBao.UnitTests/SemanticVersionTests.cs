using QuickResponseBao.Core.Services;

namespace QuickResponseBao.UnitTests;

public sealed class SemanticVersionTests
{
    [Theory][InlineData("v1.0.0", "1.0.0")][InlineData("1.2.3-beta", "1.2.3-beta")]
    public void Parse_NormalizesVersion(string input, string expected)
    { Assert.True(SemanticVersion.TryParse(input, out var value)); Assert.Equal(expected, value.ToString()); }

    [Fact]
    public void Compare_FollowsSemanticVersionPrecedence()
    {
        Assert.True(SemanticVersion.TryParse("1.10.0", out var newer)); Assert.True(SemanticVersion.TryParse("1.9.9", out var older));
        Assert.True(SemanticVersion.TryParse("2.0.0-beta.2", out var prerelease)); Assert.True(SemanticVersion.TryParse("2.0.0", out var stable));
        Assert.True(newer.CompareTo(older) > 0); Assert.True(prerelease.CompareTo(stable) < 0);
    }
}
