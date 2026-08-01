using QuickResponseBao.Core.Models;
using QuickResponseBao.Core.Services;

namespace QuickResponseBao.UnitTests;

public sealed class SearchPhraseBufferTests
{
    [Fact]
    public void ThreeLetters_DoNotTrigger()
    {
        var value = Build("how"); Assert.False(value.IsReady(4));
    }

    [Fact]
    public void ThreeLettersAndSpace_Trigger()
    {
        var value = Build("how "); Assert.True(value.IsReady(4)); Assert.Equal("how ", value.Value);
    }

    [Fact]
    public void Space_DoesNotClearExistingPhrase() => Assert.Equal("how ", Build("how ").Value);

    [Fact]
    public void ConsecutiveSpaces_AreNormalizedToOne() => Assert.Equal("solve this", Build("solve   this").Value);

    [Fact]
    public void LeadingSpaces_AreIgnored() => Assert.Equal("with", Build("   with").Value);

    [Fact]
    public void Backspace_RemovesTrailingSpace()
    {
        var value = Build("how "); Assert.True(value.Backspace()); Assert.Equal("how", value.Value);
    }

    [Fact]
    public void HowWithTrailingSpace_MatchesHowToContent()
    {
        var item = new QuickResponse { Summary = "Answer", Content = "how to solve this problem" };
        Assert.Single(new SearchService().Search([item], Build("how ").Value));
    }

    [Fact]
    public void PhraseWithSpaces_MatchesCaseInsensitively()
    {
        var item = new QuickResponse { Summary = "Answer", Content = "How To SOLVE This Problem" };
        Assert.Single(new SearchService().Search([item], Build("solve this").Value));
    }

    [Fact]
    public void EscapeOrApplicationSwitch_ClearsPhrase()
    {
        var value = Build("solve this"); Assert.True(value.Clear()); Assert.Equal(0, value.Length);
    }

    private static SearchPhraseBuffer Build(string text)
    {
        var value = new SearchPhraseBuffer();
        foreach (var character in text)
        {
            if (character == ' ') value.AppendSpace(); else value.AppendLetter(character);
        }
        return value;
    }
}
