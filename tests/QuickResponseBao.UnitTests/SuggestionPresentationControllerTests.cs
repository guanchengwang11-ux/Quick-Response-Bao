using QuickResponseBao.Core.Models;
using QuickResponseBao.Core.Services;
using QuickResponseBao.Infrastructure.Windows;

namespace QuickResponseBao.UnitTests;

public sealed class SuggestionPresentationControllerTests
{
    [Fact]
    public void OnlyLatestQueryCanPresent()
    {
        var controller = new SuggestionPresentationController();
        var oldContext = Context("ris", 3); var oldTicket = controller.Register(oldContext);
        var currentContext = Context("risk", 4); var currentTicket = controller.Register(currentContext);
        Assert.False(controller.IsCurrent(oldTicket, oldContext));
        Assert.True(controller.IsCurrent(currentTicket, currentContext));
    }

    [Fact]
    public void CancellationInvalidatesQueuedPresentation()
    {
        var controller = new SuggestionPresentationController(); var context = Context("risk", 4); var ticket = controller.Register(context);
        var cancellation = controller.Cancel();
        Assert.False(controller.IsCurrent(ticket, context)); Assert.True(controller.IsCurrent(cancellation));
    }

    [Fact]
    public void ChangedTargetInvalidatesPresentationContext()
    {
        var controller = new SuggestionPresentationController(); var context = Context("risk", 4); var ticket = controller.Register(context);
        Assert.False(controller.IsCurrent(ticket, context with { TargetWindowHandle = (nint)999 }));
    }

    [Theory]
    [InlineData(0x10)] [InlineData(0xA0)] [InlineData(0xA1)] [InlineData(0x11)] [InlineData(0xA2)] [InlineData(0xA3)]
    [InlineData(0x12)] [InlineData(0xA4)] [InlineData(0xA5)] [InlineData(0x14)]
    public void AllModifierAndCapsLockVariantsDoNotResetSearch(uint virtualKey) =>
        Assert.True(GlobalKeyboardListener.IsModifierVirtualKey(virtualKey));

    private static CandidateSearchContext Context(string query, long sequence) =>
        new(query, query.Length, (nint)123, 456, "Lark.exe", DateTimeOffset.UtcNow, query, sequence);
}
