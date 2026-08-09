namespace QuickResponseBao.UnitTests;

public sealed class ResponseEditorUiTests
{
    [Fact]
    public void Editor_UsesFluentWindowAndSemanticDesignSystem()
    {
        var xaml = Read("ResponseEditorWindow.xaml");
        Assert.StartsWith("<ui:FluentWindow", xaml.TrimStart(), StringComparison.Ordinal);
        Assert.Contains("QrbBackgroundBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("QrbPrimaryButtonStyle", xaml, StringComparison.Ordinal);
        Assert.DoesNotMatch("#[0-9a-fA-F]{6,8}", xaml);
    }

    [Fact]
    public void Editor_ProvidesLargeContentAreaAndLiveLimitFeedback()
    {
        var xaml = Read("ResponseEditorWindow.xaml");
        var code = Read("ResponseEditorWindow.xaml.cs");
        Assert.Contains("MinHeight=\"240\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ContentCounter", xaml, StringComparison.Ordinal);
        Assert.Contains("GetContentMetrics", code, StringComparison.Ordinal);
        Assert.Contains("QrbWarningBrush", code, StringComparison.Ordinal);
        Assert.Contains("QrbErrorBrush", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Editor_UsesKeywordChipsAndAllRequiredSeparators()
    {
        var xaml = Read("ResponseEditorWindow.xaml");
        var code = Read("ResponseEditorWindow.xaml.cs");
        Assert.Contains("KeywordItems", xaml, StringComparison.Ordinal);
        Assert.Contains("RemoveKeyword_Click", xaml, StringComparison.Ordinal);
        foreach (var separator in new[] { "','", "';'", "'，'", "'；'" }) Assert.Contains(separator, code, StringComparison.Ordinal);
        Assert.Contains("KeywordNormalizer.Parse", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Editor_ShowsValidationNextToAffectedFields()
    {
        var xaml = Read("ResponseEditorWindow.xaml");
        Assert.Contains("x:Name=\"SummaryError\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ContentError\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ValidationSummary", xaml, StringComparison.Ordinal);
        Assert.Contains("ValidationContent", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Editor_ContinuesToUseSharedBusinessValidation()
    {
        var code = Read("ResponseEditorWindow.xaml.cs");
        Assert.Contains("QuickResponseRules.IsSummaryValid", code, StringComparison.Ordinal);
        Assert.Contains("QuickResponseRules.IsContentValid", code, StringComparison.Ordinal);
        Assert.DoesNotContain("Substring", code, StringComparison.Ordinal);
    }

    private static string Read(string file) => File.ReadAllText(Path.Combine(Root(), "src", "QuickResponseBao.App", file));
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Directory.Build.props"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
