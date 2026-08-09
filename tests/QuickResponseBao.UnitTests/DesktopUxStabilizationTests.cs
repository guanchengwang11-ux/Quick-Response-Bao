using QuickResponseBao.App.Controls;
using QuickResponseBao.App.ViewModels;
using QuickResponseBao.Core.Models;
using QuickResponseBao.Infrastructure.Windows;

namespace QuickResponseBao.UnitTests;

public sealed class DesktopUxStabilizationTests
{
    [Fact] public void FocusRingThickness_IsAThicknessResource() => Assert.Contains("<Thickness x:Key=\"QrbFocusRingThickness\">2</Thickness>", Read("src", "QuickResponseBao.App", "Resources", "DesignTokens.xaml"));

    [Fact]
    public void DispatcherException_IsMarkedHandledBeforeAsynchronousLogging()
    {
        var source = Read("src", "QuickResponseBao.App", "App.xaml.cs");
        AssertInOrder(source, "DispatcherUnhandledException += (_, args) =>", "args.Handled = true;", "_logger?.WriteAsync");
    }

    [Fact]
    public void OwnForegroundProcess_IsNeverMonitored()
    {
        var environment = Environment("QuickResponseBao.exe", "QuickResponseBao.exe");
        Assert.False(GlobalKeyboardListener.ShouldMonitor(environment));
    }

    [Fact]
    public void OwnFocusedProcess_IsNeverMonitoredEvenUnderAnotherTopLevelWindow()
    {
        var environment = Environment("host.exe", "quickresponsebao.EXE");
        Assert.False(GlobalKeyboardListener.ShouldMonitor(environment));
    }

    [Fact] public void WhitelistedExternalProcess_RemainsMonitored() => Assert.True(GlobalKeyboardListener.ShouldMonitor(Environment("Lark.exe", "Lark.exe")));

    [Fact]
    public void FilterClone_IsIndependentFromCommittedState()
    {
        var committed = new ResponseLibraryFilterState { Summary = new TextColumnFilter("original") }; committed.Categories.Add("Risk");
        var draft = committed.Clone(); draft.Summary = new TextColumnFilter("draft"); draft.Categories.Add("Payment");
        Assert.Equal("original", committed.Summary!.Value); Assert.DoesNotContain("Payment", committed.Categories);
    }

    [Fact]
    public void CopyFieldFrom_CommitsOnlySummaryDraft()
    {
        var committed = new ResponseLibraryFilterState { Summary = new TextColumnFilter("old") }; committed.Categories.Add("Risk");
        var draft = committed.Clone(); draft.Summary = new TextColumnFilter("issue", TextFilterOperator.Contains); draft.Categories.Add("Payment");
        committed.CopyFieldFrom(draft, ResponseFilterField.Summary);
        Assert.Equal("issue", committed.Summary!.Value); Assert.Equal(["Risk"], committed.Categories);
    }

    [Fact]
    public void CopyFieldFrom_CommitsCategorySelection()
    {
        var committed = new ResponseLibraryFilterState(); committed.Categories.Add("Risk");
        var draft = committed.Clone(); draft.Categories.Clear(); draft.Categories.Add("Telegram");
        committed.CopyFieldFrom(draft, ResponseFilterField.Category);
        Assert.Equal(["Telegram"], committed.Categories);
    }

    [Fact] public void SummaryContains_IsCaseInsensitive() => Assert.True(MatchesSummary(TextFilterOperator.Contains, "ISSUE", "Welcoming issue owner"));
    [Fact] public void SummaryEquals_IsCaseInsensitive() => Assert.True(MatchesSummary(TextFilterOperator.Equals, "issue", "Issue"));
    [Fact] public void SummaryStartsWith_IsCaseInsensitive() => Assert.True(MatchesSummary(TextFilterOperator.StartsWith, "wel", "Welcoming new user"));
    [Fact] public void SummaryEndsWith_IsCaseInsensitive() => Assert.True(MatchesSummary(TextFilterOperator.EndsWith, "USER", "Welcoming new user"));
    [Fact] public void SummaryDoesNotContain_IsCaseInsensitive() => Assert.True(MatchesSummary(TextFilterOperator.DoesNotContain, "payment", "Issue report"));
    [Fact] public void KeywordContains_IsCaseInsensitive() => Assert.True(MatchesKeyword(TextFilterOperator.Contains, "RISK", "high-risk"));
    [Fact] public void KeywordEquals_IsCaseInsensitive() => Assert.True(MatchesKeyword(TextFilterOperator.Equals, "HIGH-RISK", "high-risk"));

    [Fact]
    public void LibraryApply_ExplicitlyRefreshesTheCollectionView()
    {
        var source = Read("src", "QuickResponseBao.App", "Views", "Pages", "ResponseLibraryPage.xaml.cs");
        Assert.Contains("_libraryView.Refresh();", source); Assert.DoesNotContain("using (_libraryView.DeferRefresh())", source);
    }

    [Fact]
    public void Apply_CommitsDraftThenRequestsPopupClose()
    {
        var source = Read("src", "QuickResponseBao.App", "Controls", "ColumnFilterPanel.xaml.cs");
        AssertInOrder(source, "_committedState.CopyFieldFrom(_draftState, _field);", "FilterCommitAction.Apply", "CloseRequested?.Invoke");
    }

    [Fact]
    public void Clear_ClearsCommittedStateThenRequestsPopupClose()
    {
        var source = Read("src", "QuickResponseBao.App", "Controls", "ColumnFilterPanel.xaml.cs");
        AssertInOrder(source, "_committedState.Clear(_field);", "FilterCommitAction.Clear", "CloseRequested?.Invoke");
    }

    [Fact]
    public void Cancel_DoesNotCopyDraftIntoCommittedState()
    {
        var source = Method("public void CancelDraft()", "private void UpdateDraftFromControls()");
        Assert.DoesNotContain("CopyFieldFrom", source); Assert.Contains("CloseRequested?.Invoke", source);
    }

    [Fact] public void FilterEnter_MapsToApply() { var source = Read("src", "QuickResponseBao.App", "Controls", "ColumnFilterPanel.xaml.cs"); Assert.Contains("args.Key == Key.Enter", source); Assert.Contains("ApplyDraft(); args.Handled = true", source); }
    [Fact] public void FilterEscape_MapsToCancel() { var source = Read("src", "QuickResponseBao.App", "Controls", "ColumnFilterPanel.xaml.cs"); Assert.Contains("args.Key == Key.Escape", source); Assert.Contains("Cancel_Click", source); }
    [Fact] public void FilterTab_IsNotIntercepted() { var source = Read("src", "QuickResponseBao.App", "Controls", "ColumnFilterPanel.xaml.cs"); Assert.DoesNotContain("Key.Tab", source); }
    [Fact] public void OperatorOption_DisplaysOnlyItsLocalizedLabel() => Assert.Equal("Contains", new OperatorOption<TextFilterOperator>("Contains", TextFilterOperator.Contains).ToString());

    [Fact]
    public async Task PreviewCount_UsesDraftWithoutMutatingCommittedState()
    {
        var rows = new[] { Response("Issue one"), Response("Other"), Response("ISSUE two") };
        var model = new ResponseLibraryViewModel(); model.Synchronize(rows, 1);
        var committed = new ResponseLibraryFilterState(); var draft = committed.Clone(); draft.Summary = new TextColumnFilter("issue");
        Assert.Equal(2, await model.CountMatchesAsync(draft)); Assert.Null(committed.Summary);
    }

    [Fact]
    public void CandidateItem_HasNoResponseContentToolTip()
    {
        var source = Read("src", "QuickResponseBao.App", "CandidateWindow.xaml.cs");
        Assert.DoesNotContain("ToolTip = response.Content", source); Assert.DoesNotContain("new ToolTip", source);
    }

    [Fact]
    public void CandidateHover_DoesNotConstructPopupWindow()
    {
        var source = Read("src", "QuickResponseBao.App", "CandidateWindow.xaml.cs");
        Assert.DoesNotContain("new Popup", source); Assert.DoesNotContain("Popup.IsOpen", source);
    }

    [Fact]
    public void NavigationPane_OwnsTheAlwaysAvailableHamburgerButton()
    {
        var xaml = Read("src", "QuickResponseBao.App", "MainWindow.xaml");
        Assert.Contains("x:Name=\"PaneToggleButton\"", xaml); Assert.Contains("IsPaneToggleVisible=\"False\"", xaml); Assert.Contains("NavigationView.PaneHeader", xaml);
    }

    [Fact]
    public void CompactNavigation_KeepsPaneToggleVisible()
    {
        var xaml = Read("src", "QuickResponseBao.App", "MainWindow.xaml");
        var button = xaml.IndexOf("x:Name=\"PaneToggleButton\"", StringComparison.Ordinal);
        var brandTrigger = xaml.IndexOf("Binding IsPaneOpen", StringComparison.Ordinal);
        Assert.True(button >= 0 && button < brandTrigger); Assert.DoesNotContain("x:Name=\"PaneToggleButton\" Visibility", xaml);
    }

    [Fact]
    public void TitleBar_HasNoDuplicateBrandOrLogo()
    {
        var xaml = Read("src", "QuickResponseBao.App", "MainWindow.xaml");
        Assert.Contains("<ui:TitleBar x:Name=\"AppTitleBar\" Title=\"\" />", xaml); Assert.DoesNotContain("<ui:TitleBar.Icon>", xaml);
    }

    [Fact]
    public void LibrarySearchBox_UsesCorrectedFocusVisualAndNoTabOverride()
    {
        var xaml = Read("src", "QuickResponseBao.App", "Views", "Pages", "ResponseLibraryPage.xaml");
        Assert.Contains("x:Name=\"SearchBox\"", xaml); Assert.Contains("QrbSearchBoxStyle", xaml); Assert.DoesNotContain("PreviewKeyDown=", xaml);
    }

    [Fact]
    public void FilterIcons_AreSubtleUntilHoverOrActive()
    {
        var xaml = Read("src", "QuickResponseBao.App", "Views", "Pages", "ResponseLibraryPage.xaml");
        Assert.Contains("<Setter Property=\"Opacity\" Value=\"0.46\"", xaml); Assert.Contains("Property=\"IsMouseOver\"", xaml);
    }

    private static bool MatchesSummary(TextFilterOperator op, string query, string summary) => new ResponseLibraryFilterState { Summary = new TextColumnFilter(query, op) }.Matches(Response(summary));
    private static bool MatchesKeyword(TextFilterOperator op, string query, string keyword) => new ResponseLibraryFilterState { Keywords = new TextColumnFilter(query, op) }.Matches(new QuickResponse { Summary = "Test", Content = "Body", Category = "General", Language = "English", Keywords = [keyword] });
    private static QuickResponse Response(string summary) => new() { Summary = summary, Content = "Body", Category = "General", Language = "English" };
    private static InputEnvironmentInfo Environment(string process, string focus) => new(process, focus, "Title", true, TextInputDetectionState.Detected, false, false, false);
    private static string Method(string start, string end) { var source = Read("src", "QuickResponseBao.App", "Controls", "ColumnFilterPanel.xaml.cs"); return source[source.IndexOf(start, StringComparison.Ordinal)..source.IndexOf(end, source.IndexOf(start, StringComparison.Ordinal), StringComparison.Ordinal)]; }
    private static void AssertInOrder(string source, params string[] values) { var position = -1; foreach (var value in values) { var next = source.IndexOf(value, position + 1, StringComparison.Ordinal); Assert.True(next > position, $"Expected '{value}' after index {position}."); position = next; } }
    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([Root(), .. parts]));
    private static string Root() { var directory = new DirectoryInfo(AppContext.BaseDirectory); while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Directory.Build.props"))) directory = directory.Parent; return directory?.FullName ?? throw new DirectoryNotFoundException(); }
}
