using ClosedXML.Excel;
using QuickResponseBao.Core.Models;
using QuickResponseBao.Core.Services;
using QuickResponseBao.Infrastructure.ImportExport;

namespace QuickResponseBao.UnitTests;

public sealed class V101ScrollAndValidationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"qrb-v101-validation-{Guid.NewGuid():N}");
    public V101ScrollAndValidationTests() => Directory.CreateDirectory(_root);

    [Fact] public void NonOverflowingCandidateList_DoesNotRequireScrolling() =>
        Assert.False(ScrollViewportPolicy.RequiresScrolling(400, 500));

    [Fact] public void OverflowingCandidateList_AllowsReasonableDownwardStep() =>
        Assert.Equal(48, ScrollViewportPolicy.WheelOffset(0, -120, 600));

    [Fact] public void WheelUp_IsClampedAtTop() =>
        Assert.Equal(0, ScrollViewportPolicy.WheelOffset(10, 120, 600));

    [Fact] public void WheelDown_IsClampedAtBottom() =>
        Assert.Equal(600, ScrollViewportPolicy.WheelOffset(590, -120, 600));

    [Fact]
    public void CandidateWindow_KeepsNoActivateAndImplementsSelectionVisibilityAndRefreshReset()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "QuickResponseBao.App", "CandidateWindow.xaml"));
        var code = File.ReadAllText(Path.Combine(root, "src", "QuickResponseBao.App", "CandidateWindow.xaml.cs"));
        Assert.Contains("ShowActivated=\"False\"", xaml); Assert.Contains("PreviewMouseWheel", xaml);
        Assert.Contains("e.Handled = true", code); Assert.Contains("BringIntoView", code); Assert.Contains("ScrollToTop", code);
    }

    [Theory][InlineData(299, true)][InlineData(300, true)][InlineData(301, false)]
    public void ContentLimit_UsesThreeHundredCharacters(int length, bool expected) =>
        Assert.Equal(expected, QuickResponseRules.IsContentValid(new string('内', length)));

    [Fact]
    public async Task Repository_PreservesThreeHundredCharacterMultilineContent()
    {
        using var workspace = new TestWorkspace(); await workspace.Repository.InitializeAsync();
        var content = new string('a', 149) + "\n" + new string('中', 150);
        var item = new QuickResponse { Summary = "Long multiline", Content = content };
        await workspace.Repository.UpsertAsync(item);
        Assert.Equal(content, (await workspace.Repository.GetAsync(item.Id))!.Content);
    }

    [Fact]
    public async Task Repository_RejectsThreeHundredAndOneCharacters()
    {
        using var workspace = new TestWorkspace(); await workspace.Repository.InitializeAsync();
        var item = new QuickResponse { Summary = "Too long", Content = new string('x', 301) };
        await Assert.ThrowsAsync<ArgumentException>(() => workspace.Repository.UpsertAsync(item));
    }

    [Fact]
    public async Task Excel_ImportsThreeHundredAndRejectsThreeHundredAndOneWithoutStoppingValidRows()
    {
        var path = Path.Combine(_root, "limits.xlsx");
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.Worksheets.Add("Data"); sheet.Cell(1, 1).Value = "Summary"; sheet.Cell(1, 2).Value = "Content";
            sheet.Cell(2, 1).Value = "Maximum"; sheet.Cell(2, 2).Value = new string('x', 300);
            sheet.Cell(3, 1).Value = "Too long"; sheet.Cell(3, 2).Value = new string('x', 301);
            sheet.Cell(4, 1).Value = "Still valid"; sheet.Cell(4, 2).Value = "continues"; workbook.SaveAs(path);
        }
        var service = new ExcelQuickResponseService(); var preview = await service.PreviewAsync(path);
        var outcome = await service.ImportAsync(path, service.SuggestMapping(preview.Headers));
        Assert.Equal(2, outcome.Items.Count); Assert.Single(outcome.Result.Failures);
        Assert.Equal(3, outcome.Result.Failures[0].RowNumber); Assert.Equal("ImportContentTooLong", outcome.Result.Failures[0].Reason);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "QuickResponseBao.sln"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
