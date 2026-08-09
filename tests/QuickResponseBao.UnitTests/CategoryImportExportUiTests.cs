namespace QuickResponseBao.UnitTests;

public sealed class CategoryImportExportUiTests
{
    [Fact]
    public void CategoriesPage_ProvidesCountsOrderingAndCompleteActions()
    {
        var xaml = Read("Views", "Pages", "CategoriesPage.xaml");
        var code = Read("Views", "Pages", "CategoriesPage.xaml.cs");
        Assert.Contains("ResponseCount", xaml, StringComparison.Ordinal);
        foreach (var action in new[] { "Add_Click", "Rename_Click", "Up_Click", "Down_Click", "Delete_Click" })
            Assert.Contains(action, xaml, StringComparison.Ordinal);
        Assert.Contains("ReorderCategoriesAsync", code, StringComparison.Ordinal);
    }

    [Fact]
    public void CategoryDelete_MovesResponsesInsteadOfDeletingThem()
    {
        var xaml = Read("CategoryDeleteWindow.xaml");
        var pageCode = Read("Views", "Pages", "CategoriesPage.xaml.cs");
        Assert.Contains("MoveResponsesTo", xaml, StringComparison.Ordinal);
        Assert.Contains("UncategorizedCategory", Read("CategoryDeleteWindow.xaml.cs"), StringComparison.Ordinal);
        Assert.Contains("DeleteCategoryAsync(row.Category.Id, row.ResponseCount > 0)", pageCode, StringComparison.Ordinal);
        Assert.DoesNotContain("DeleteManyAsync", pageCode, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_ImplementsFourVisibleWorkflowSteps()
    {
        var xaml = Read("Views", "Pages", "ImportExportPage.xaml");
        foreach (var step in Enumerable.Range(1, 4))
        {
            Assert.Contains($"Step{step}Indicator", xaml, StringComparison.Ordinal);
            Assert.Contains($"Step{step}Panel", xaml, StringComparison.Ordinal);
        }
        Assert.Contains("PreviewGrid", xaml, StringComparison.Ordinal);
        Assert.Contains("FieldMapping", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ImportValidation_PreservesExistingAndWithinFileDuplicateRules()
    {
        var code = Read("Services", "ImportValidationService.cs");
        Assert.Contains("QuickResponseBusinessKey.Create", code, StringComparison.Ordinal);
        Assert.Contains("ImportDuplicateExisting", code, StringComparison.Ordinal);
        Assert.Contains("ImportDuplicateCurrent", code, StringComparison.Ordinal);
        Assert.Contains("referenceRow", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_ProvidesFormatsScopesAndDisabledOption()
    {
        var xaml = Read("Views", "Pages", "ImportExportPage.xaml");
        foreach (var tag in new[] { "xlsx", "csv", "json", "all", "filtered", "selected" })
            Assert.Contains($"Tag=\"{tag}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("IncludeDisabledBox", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase6Pages_UseSemanticResourcesWithoutHardcodedPalette()
    {
        var xaml = Read("Views", "Pages", "CategoriesPage.xaml") + Read("Views", "Pages", "ImportExportPage.xaml");
        Assert.DoesNotMatch("#[0-9a-fA-F]{6,8}", xaml);
        Assert.Contains("QrbCardStyle", xaml, StringComparison.Ordinal);
        Assert.Contains("DynamicResource", xaml, StringComparison.Ordinal);
    }

    private static string Read(params string[] path) => File.ReadAllText(Path.Combine(new[] { Root(), "src", "QuickResponseBao.App" }.Concat(path).ToArray()));
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Directory.Build.props"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
