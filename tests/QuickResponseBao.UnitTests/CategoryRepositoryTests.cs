using QuickResponseBao.Core.Models;
using QuickResponseBao.Infrastructure.Storage;

namespace QuickResponseBao.UnitTests;

public sealed class CategoryRepositoryTests
{
    [Fact]
    public async Task AddCategory_PersistsCategory()
    {
        using var workspace = new TestWorkspace(); await workspace.Repository.InitializeAsync();
        var added = await workspace.Repository.AddCategoryAsync("Custom");
        Assert.Contains(await workspace.Repository.GetCategoriesAsync(), x => x.Id == added.Id && x.Name == "Custom");
    }

    [Fact]
    public async Task RenameCategory_UpdatesResponsesAtomically()
    {
        using var workspace = new TestWorkspace(); await workspace.Repository.InitializeAsync();
        var category = await workspace.Repository.AddCategoryAsync("Old name"); var response = new QuickResponse { Summary = "Test reply", Content = "Text", Category = category.Name };
        await workspace.Repository.UpsertAsync(response); await workspace.Repository.RenameCategoryAsync(category.Id, "New name");
        Assert.Equal("New name", (await workspace.Repository.GetAsync(response.Id))!.Category);
    }

    [Fact]
    public async Task DeleteEmptyCategory_DoesNotAffectResponses()
    {
        using var workspace = new TestWorkspace(); await workspace.Repository.InitializeAsync();
        var category = await workspace.Repository.AddCategoryAsync("Empty"); var response = new QuickResponse { Summary = "Other reply", Content = "Text" };
        await workspace.Repository.UpsertAsync(response); await workspace.Repository.DeleteCategoryAsync(category.Id, false);
        Assert.DoesNotContain(await workspace.Repository.GetCategoriesAsync(), x => x.Id == category.Id); Assert.NotNull(await workspace.Repository.GetAsync(response.Id));
    }

    [Fact]
    public async Task DeletePopulatedCategory_RequiresMoveAndMovesToUncategorized()
    {
        using var workspace = new TestWorkspace(); await workspace.Repository.InitializeAsync();
        var category = await workspace.Repository.AddCategoryAsync("Populated"); var response = new QuickResponse { Summary = "Move reply", Content = "Text", Category = category.Name };
        await workspace.Repository.UpsertAsync(response);
        await Assert.ThrowsAsync<InvalidOperationException>(() => workspace.Repository.DeleteCategoryAsync(category.Id, false));
        await workspace.Repository.DeleteCategoryAsync(category.Id, true);
        Assert.Equal(SqliteQuickResponseRepository.UncategorizedName, (await workspace.Repository.GetAsync(response.Id))!.Category);
    }

    [Fact]
    public async Task ReorderCategories_PersistsRequestedOrder()
    {
        using var workspace = new TestWorkspace(); await workspace.Repository.InitializeAsync();
        var first = await workspace.Repository.AddCategoryAsync("First custom"); var second = await workspace.Repository.AddCategoryAsync("Second custom");
        var all = (await workspace.Repository.GetCategoriesAsync()).ToList(); all.RemoveAll(x => x.Id == first.Id || x.Id == second.Id); all.Insert(0, second); all.Insert(1, first);
        await workspace.Repository.ReorderCategoriesAsync(all.Select(x => x.Id).ToList()); var reordered = await workspace.Repository.GetCategoriesAsync();
        Assert.Equal(second.Id, reordered[0].Id); Assert.Equal(first.Id, reordered[1].Id);
    }
}
