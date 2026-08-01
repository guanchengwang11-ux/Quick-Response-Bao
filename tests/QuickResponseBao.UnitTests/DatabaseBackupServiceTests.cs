using QuickResponseBao.Core.Models;
using QuickResponseBao.Infrastructure.Storage;

namespace QuickResponseBao.UnitTests;

public sealed class DatabaseBackupServiceTests
{
    [Fact]
    public async Task CreateBackup_ProducesValidDatabaseAndMetadata()
    {
        using var workspace = new TestWorkspace(); await workspace.Repository.InitializeAsync(); await workspace.Repository.UpsertAsync(new QuickResponse { Summary = "Backup reply", Content = "Text" });
        var service = new DatabaseBackupService(workspace.Paths); var backup = await service.CreateBackupAsync();
        Assert.True(File.Exists(backup.Path)); Assert.True(backup.Size > 0); Assert.True(await service.ValidateAsync(backup.Path));
    }

    [Fact]
    public async Task Restore_ReplacesDatabaseAndCreatesSafetyBackup()
    {
        using var workspace = new TestWorkspace(); await workspace.Repository.InitializeAsync(); var original = new QuickResponse { Summary = "Original reply", Content = "Text" };
        await workspace.Repository.UpsertAsync(original); var service = new DatabaseBackupService(workspace.Paths); var backup = await service.CreateBackupAsync();
        await workspace.Repository.DeleteAsync(original.Id); await workspace.Repository.UpsertAsync(new QuickResponse { Summary = "Later reply", Content = "Later" });
        var result = await service.RestoreAsync(backup.Path);
        Assert.True(result.Succeeded, result.Message); Assert.NotNull(result.SafetyBackupPath); Assert.NotNull(await workspace.Repository.GetAsync(original.Id));
    }

    [Fact]
    public async Task InvalidBackup_IsRejectedWithoutChangingCurrentData()
    {
        using var workspace = new TestWorkspace(); await workspace.Repository.InitializeAsync(); var original = new QuickResponse { Summary = "Protected reply", Content = "Text" };
        await workspace.Repository.UpsertAsync(original); var invalid = Path.Combine(workspace.Root, "invalid.db"); await File.WriteAllTextAsync(invalid, "not a sqlite database");
        var result = await new DatabaseBackupService(workspace.Paths).RestoreAsync(invalid);
        Assert.False(result.Succeeded); Assert.NotNull(await workspace.Repository.GetAsync(original.Id));
    }
}
