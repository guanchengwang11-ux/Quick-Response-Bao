namespace QuickResponseBao.UnitTests;

public sealed class UnifiedFeedbackUiTests
{
    [Fact]
    public void FeedbackService_DefinesSemanticLevelsAndTimedMessages()
    {
        var source = File.ReadAllText(Path.Combine(Root(), "src", "QuickResponseBao.App", "Services", "UiFeedbackService.cs"));
        Assert.Contains("Success, Information, Warning, Error", source);
        Assert.Contains("TimeSpan.FromSeconds(3)", source);
    }

    [Fact]
    public void DangerousActions_UseCentralDialogService()
    {
        var source = File.ReadAllText(Path.Combine(Root(), "src", "QuickResponseBao.App", "Services", "UiDialogService.cs"));
        Assert.Contains("QrbDangerButtonStyle", source); Assert.Contains("IsDefault = true", source); Assert.Contains("IsCancel = true", source);
    }

    [Fact]
    public void Shell_ContainsGlobalFeedbackHostWithCopyAndCloseActions()
    {
        var text = File.ReadAllText(Path.Combine(Root(), "src", "QuickResponseBao.App", "MainWindow.xaml"));
        Assert.Contains("FeedbackHost", text); Assert.Contains("CopyFeedback_Click", text); Assert.Contains("CloseFeedback_Click", text);
    }

    [Fact]
    public void LongRunningImportAndBackup_ShowLoadingFeedback()
    {
        var root = Root();
        Assert.Contains("BusyOverlay", File.ReadAllText(Path.Combine(root, "src", "QuickResponseBao.App", "Views", "Pages", "ImportExportPage.xaml")));
        Assert.Contains("BusyProgress", File.ReadAllText(Path.Combine(root, "src", "QuickResponseBao.App", "BackupManagerWindow.xaml")));
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Directory.Build.props"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
