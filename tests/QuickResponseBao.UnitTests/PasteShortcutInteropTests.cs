using QuickResponseBao.Infrastructure.Windows;

namespace QuickResponseBao.UnitTests;

public sealed class PasteShortcutInteropTests
{
    [Fact]
    public void NativeInput_HasCorrectPlatformSize()
    {
        Assert.Equal(PasteShortcutInput.ExpectedStructureSize, PasteShortcutInput.StructureSize);
        if (Environment.Is64BitProcess) Assert.Equal(40, PasteShortcutInput.StructureSize);
    }

    [Fact]
    public void Shortcut_ContainsCtrlDownVDownVUpCtrlUp()
    {
        var inputs = PasteShortcutInput.Create();
        Assert.Equal(4, inputs.Length);
        Assert.Equal([PasteShortcutInput.ControlKey, PasteShortcutInput.VKey, PasteShortcutInput.VKey, PasteShortcutInput.ControlKey],
            inputs.Select(x => x.Data.Keyboard.VirtualKey));
        Assert.Equal([0u, 0u, PasteShortcutInput.KeyUp, PasteShortcutInput.KeyUp], inputs.Select(x => x.Data.Keyboard.Flags));
        Assert.All(inputs, x => Assert.Equal(PasteShortcutInput.KeyboardType, x.Type));
    }

    [Fact]
    public void KeyUpFlag_IsAppliedOnlyToReleaseEvents()
    {
        var inputs = PasteShortcutInput.Create();
        Assert.Equal(0u, inputs[0].Data.Keyboard.Flags & PasteShortcutInput.KeyUp);
        Assert.Equal(0u, inputs[1].Data.Keyboard.Flags & PasteShortcutInput.KeyUp);
        Assert.NotEqual(0u, inputs[2].Data.Keyboard.Flags & PasteShortcutInput.KeyUp);
        Assert.NotEqual(0u, inputs[3].Data.Keyboard.Flags & PasteShortcutInput.KeyUp);
    }

    [Theory]
    [InlineData(0u)][InlineData(1u)][InlineData(3u)]
    public void PartialSend_IsFailure(uint count) => Assert.False(PasteShortcutInput.WasFullySent(count));

    [Fact]
    public void CompleteSend_IsSuccess() => Assert.True(PasteShortcutInput.WasFullySent(4));

    [Fact]
    public void FailedSend_CannotBeReportedAsSuccessfulPaste() => Assert.False(PasteShortcutInput.WasFullySent(0));

    [Theory]
    [InlineData(1, 100)][InlineData(750, 750)][InlineData(9000, 5000)]
    public void ClipboardRestoreDelay_IsClamped(int requested, int expected) =>
        Assert.Equal(TimeSpan.FromMilliseconds(expected), PasteShortcutInput.RestoreDelay(requested));
}
