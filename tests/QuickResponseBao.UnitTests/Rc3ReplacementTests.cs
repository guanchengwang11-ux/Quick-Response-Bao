using QuickResponseBao.Core.Models;
using QuickResponseBao.Core.Services;
using QuickResponseBao.Infrastructure.Windows;

namespace QuickResponseBao.UnitTests;

public sealed class Rc3ReplacementTests
{
    [Fact]
    public async Task ReplacingHowTo_ProducesResponseWithoutDuplicatePrefix()
    {
        var target = "how to"; const string response = "how to solve this problem";
        var result = await ExecuteAsync("how to", count => { target = target[..^count]; return Success(count * 2); },
            () => { target += response; return Success(4); }, raw => { target += raw; return Success(raw.Length * 2); });
        Assert.True(result.PasteSucceeded); Assert.Equal(response, target);
    }

    [Fact]
    public void RawCount_IsSeparateFromNormalizedQueryLength()
    {
        var buffer = Build("how  to"); Assert.Equal("how to", buffer.Value); Assert.Equal(7, buffer.RawTypedCharacterCount);
    }

    [Fact]
    public void ConsecutiveSpaces_DeleteUsingActualCharacterCount()
    {
        var buffer = Build("how  to"); Assert.Equal(7, buffer.RawTypedText.Length); Assert.Equal(6, buffer.Value.Length);
    }

    [Fact]
    public void TrailingSpace_IsIncludedInRawDeletionCount()
    {
        var buffer = Build("how "); Assert.Equal("how ", buffer.Value); Assert.Equal(4, buffer.RawTypedCharacterCount);
    }

    [Fact]
    public void Backspace_UpdatesRawAndNormalizedState()
    {
        var buffer = Build("how  to"); buffer.Backspace(); buffer.Backspace();
        Assert.Equal("how ", buffer.Value); Assert.Equal(5, buffer.RawTypedCharacterCount);
    }

    [Fact]
    public async Task DeleteFailure_DoesNotContinueToPaste()
    {
        var pasteCalled = false;
        var result = await ExecuteAsync("how to", _ => new(14, 0, 87),
            () => { pasteCalled = true; return Success(4); }, raw => Success(raw.Length * 2));
        Assert.False(pasteCalled); Assert.Equal(ReplacementFailure.DeleteFailed, result.Failure);
    }

    [Fact]
    public async Task PasteFailure_AttemptsToRestoreOriginalTriggerText()
    {
        string? restored = null;
        var result = await ExecuteAsync("how to", count => Success(count * 2), () => new(4, 0, 87),
            raw => { restored = raw; return Success(raw.Length * 2); });
        Assert.Equal("how to", restored); Assert.True(result.RollbackAttempted); Assert.True(result.RollbackSucceeded);
    }

    [Fact]
    public void ChangedTargetWindow_InvalidatesConfirmation() =>
        Assert.False(CandidateTargetPolicy.IsSameTarget((nint)100, 10, (nint)101, 10));

    [Fact]
    public void ChangedTargetProcess_InvalidatesConfirmation() =>
        Assert.False(CandidateTargetPolicy.IsSameTarget((nint)100, 10, (nint)100, 11));

    [Fact]
    public void InjectedBackspace_IsIgnoredBySearchHook() =>
        Assert.True(GlobalKeyboardListener.ShouldIgnoreInjectedKeyboard(0x10));

    [Fact]
    public void EnterConfirmation_DoesNotGenerateEnterInput()
    {
        var keys = TransactionKeys(6); Assert.DoesNotContain((ushort)0x0D, keys);
    }

    [Fact]
    public void TabConfirmation_DoesNotGenerateTabInput()
    {
        var keys = TransactionKeys(6); Assert.DoesNotContain((ushort)0x09, keys);
    }

    [Fact]
    public void MouseAndKeyboardConfirmation_CarryEquivalentTargetAndSearchContext()
    {
        var search = SearchContext(); var response = new QuickResponse { Summary = "Answer", Content = "text" };
        var enter = search.Confirm(response, CandidateConfirmationMethod.Enter); var mouse = search.Confirm(response, CandidateConfirmationMethod.Mouse);
        Assert.Equal(enter.SelectedResponse, mouse.SelectedResponse); Assert.Equal(enter.RawTypedCharacterCount, mouse.RawTypedCharacterCount);
        Assert.Equal(enter.TargetWindowHandle, mouse.TargetWindowHandle);
    }

    [Fact]
    public void DisabledReplaceSetting_PreservesInsertMode() =>
        Assert.Equal(ResponseInsertionMode.Insert, ResponseInsertionModePolicy.Resolve(false));

    [Fact]
    public void ReplaceTypedSearchText_IsEnabledByDefault()
    {
        var settings = new AppSettings(); Assert.True(settings.ReplaceTypedSearchText);
        Assert.Equal(ResponseInsertionMode.ReplaceTypedSearchText, ResponseInsertionModePolicy.Resolve(settings.ReplaceTypedSearchText));
    }

    [Fact]
    public async Task ReplaceTypedSearchTextPreference_IsPersisted()
    {
        using var workspace = new TestWorkspace();
        var store = new QuickResponseBao.Infrastructure.Storage.JsonSettingsStore(workspace.Paths);
        await store.SaveAsync(new AppSettings { ReplaceTypedSearchText = false });
        Assert.False((await store.LoadAsync()).ReplaceTypedSearchText);
    }

    private static async Task<ReplacementTransactionResult> ExecuteAsync(string raw,
        Func<int, InputInjectionResult> delete, Func<InputInjectionResult> paste, Func<string, InputInjectionResult> restore) =>
        await new ReplacementTransactionCoordinator().ExecuteAsync(raw.Length, raw, () => Task.FromResult(true),
            count => Task.FromResult(delete(count)), () => Task.FromResult(paste()), value => Task.FromResult(restore(value)));

    private static InputInjectionResult Success(int expected) => new(expected, expected, 0);
    private static ushort[] TransactionKeys(int characters) => PasteShortcutInput.CreateBackspaces(characters)
        .Concat(PasteShortcutInput.Create()).Select(input => input.Data.Keyboard.VirtualKey).ToArray();
    private static CandidateSearchContext SearchContext() => new("how to", 6, (nint)100, 10, "Lark.exe", DateTimeOffset.UtcNow, "how to");
    private static SearchPhraseBuffer Build(string text)
    {
        var buffer = new SearchPhraseBuffer();
        foreach (var character in text) { if (character == ' ') buffer.AppendSpace(); else buffer.AppendLetter(character); }
        return buffer;
    }
}
