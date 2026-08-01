using QuickResponseBao.Core.Models;
using QuickResponseBao.Infrastructure.Storage;

namespace QuickResponseBao.UnitTests;

public sealed class SqliteRepositoryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"qrb-tests-{Guid.NewGuid():N}");
    [Fact]
    public async Task Repository_PerformsCrudAndUsageUpdate()
    {
        var repository = new SqliteQuickResponseRepository(new AppPaths(_root)); await repository.InitializeAsync();
        var item = new QuickResponse { Summary = "Test reply", Content = "Full reply" };
        await repository.UpsertAsync(item); Assert.NotNull(await repository.GetAsync(item.Id));
        item.Content = "Updated"; await repository.UpsertAsync(item); Assert.Equal("Updated", (await repository.GetAsync(item.Id))!.Content);
        await repository.IncrementUsageAsync(item.Id); Assert.Equal(1, (await repository.GetAsync(item.Id))!.UsageCount);
        await repository.DeleteAsync(item.Id); Assert.Null(await repository.GetAsync(item.Id));
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
