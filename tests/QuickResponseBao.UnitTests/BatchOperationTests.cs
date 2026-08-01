using QuickResponseBao.Core.Models;

namespace QuickResponseBao.UnitTests;

public sealed class BatchOperationTests
{
    [Fact]
    public async Task BatchEnableAndDisable_ReportsActualCount()
    {
        using var workspace = new TestWorkspace(); await workspace.Repository.InitializeAsync();
        var items = await AddItems(workspace); var disabled = await workspace.Repository.SetEnabledAsync(items.Select(x => x.Id).ToList(), false);
        Assert.Equal(2, disabled.Processed); Assert.All(await workspace.Repository.GetAllAsync(), x => Assert.False(x.IsEnabled));
        var enabled = await workspace.Repository.SetEnabledAsync(items.Select(x => x.Id).ToList(), true);
        Assert.Equal(2, enabled.Processed); Assert.All(await workspace.Repository.GetAllAsync(), x => Assert.True(x.IsEnabled));
    }

    [Fact]
    public async Task BatchMoveCategory_MovesEverySelectedResponse()
    {
        using var workspace = new TestWorkspace(); await workspace.Repository.InitializeAsync(); var category = await workspace.Repository.AddCategoryAsync("Destination");
        var items = await AddItems(workspace); var result = await workspace.Repository.MoveToCategoryAsync(items.Select(x => x.Id).ToList(), category.Name);
        Assert.Equal(2, result.Processed); Assert.All(await workspace.Repository.GetAllAsync(), x => Assert.Equal(category.Name, x.Category));
    }

    [Fact]
    public async Task BatchDelete_DeletesOnlySelectedResponses()
    {
        using var workspace = new TestWorkspace(); await workspace.Repository.InitializeAsync(); var items = await AddItems(workspace);
        var result = await workspace.Repository.DeleteManyAsync([items[0].Id]);
        Assert.Equal(1, result.Processed); Assert.Null(await workspace.Repository.GetAsync(items[0].Id)); Assert.NotNull(await workspace.Repository.GetAsync(items[1].Id));
    }

    private static async Task<List<QuickResponse>> AddItems(TestWorkspace workspace)
    {
        var items = new List<QuickResponse> { new() { Summary = "First reply", Content = "One" }, new() { Summary = "Second reply", Content = "Two" } };
        foreach (var item in items) await workspace.Repository.UpsertAsync(item); return items;
    }
}
