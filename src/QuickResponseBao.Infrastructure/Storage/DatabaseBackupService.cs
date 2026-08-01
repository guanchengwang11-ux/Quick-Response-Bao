using Microsoft.Data.Sqlite;
using QuickResponseBao.Core.Interfaces;

namespace QuickResponseBao.Infrastructure.Storage;

public sealed class DatabaseBackupService(AppPaths paths) : IDatabaseBackupService
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<DatabaseBackupInfo> CreateBackupAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try { return await CreateBackupCoreAsync("manual", cancellationToken); }
        finally { _gate.Release(); }
    }

    public Task<IReadOnlyList<DatabaseBackupInfo>> GetBackupsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<DatabaseBackupInfo> result = Directory.EnumerateFiles(paths.Backups, "quick-responses-*.db")
            .Select(path => new FileInfo(path)).OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file => new DatabaseBackupInfo(file.FullName, file.LastWriteTimeUtc, file.Length)).ToList();
        return Task.FromResult(result);
    }

    public async Task<bool> ValidateAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path)) return false;
        try
        {
            await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = Path.GetFullPath(path), Mode = SqliteOpenMode.ReadOnly, Pooling = false
            }.ToString());
            await connection.OpenAsync(cancellationToken);
            var integrity = connection.CreateCommand(); integrity.CommandText = "PRAGMA integrity_check";
            if (!string.Equals(await integrity.ExecuteScalarAsync(cancellationToken) as string, "ok", StringComparison.OrdinalIgnoreCase)) return false;
            var schema = connection.CreateCommand();
            schema.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('quick_responses','categories')";
            return Convert.ToInt32(await schema.ExecuteScalarAsync(cancellationToken)) == 2;
        }
        catch (SqliteException) { return false; }
        catch (InvalidDataException) { return false; }
    }

    public async Task<DatabaseRestoreResult> RestoreAsync(string path, CancellationToken cancellationToken = default)
    {
        path = Path.GetFullPath(path);
        await _gate.WaitAsync(cancellationToken);
        DatabaseBackupInfo? safety = null; var replaced = false;
        var staging = Path.Combine(paths.Data, $"restore-stage-{Guid.NewGuid():N}.db");
        var rollback = Path.Combine(paths.Data, $"restore-rollback-{Guid.NewGuid():N}.db");
        try
        {
            if (path.Equals(Path.GetFullPath(paths.Database), StringComparison.OrdinalIgnoreCase))
                return new DatabaseRestoreResult(false, "The active database cannot be used as its own backup.", null);
            if (!await ValidateAsync(path, cancellationToken))
                return new DatabaseRestoreResult(false, "The selected file is not a valid Quick Response Bao backup.", null);
            safety = await CreateBackupCoreAsync("before-restore", cancellationToken);
            File.Copy(path, staging, true);
            if (!await ValidateAsync(staging, cancellationToken)) throw new InvalidDataException("The staged backup failed validation.");
            File.Replace(staging, paths.Database, rollback, true); replaced = true;
            if (!await ValidateAsync(paths.Database, cancellationToken)) throw new InvalidDataException("The restored database failed validation.");
            if (File.Exists(rollback)) File.Delete(rollback);
            return new DatabaseRestoreResult(true, "Database restored successfully.", safety.Path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException or InvalidDataException)
        {
            if (replaced && safety is not null)
            {
                try
                {
                    var recovery = Path.Combine(paths.Data, $"restore-recovery-{Guid.NewGuid():N}.db");
                    File.Copy(safety.Path, recovery, true); File.Replace(recovery, paths.Database, null, true);
                }
                catch { return new DatabaseRestoreResult(false, $"Restore failed and automatic rollback also failed: {ex.Message}", safety.Path); }
            }
            return new DatabaseRestoreResult(false, $"Restore failed; the original data was preserved: {ex.Message}", safety?.Path);
        }
        finally
        {
            foreach (var temporary in new[] { staging, rollback })
                try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
            _gate.Release();
        }
    }

    private async Task<DatabaseBackupInfo> CreateBackupCoreAsync(string kind, CancellationToken cancellationToken)
    {
        var destination = Path.Combine(paths.Backups, $"quick-responses-{kind}-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.db");
        await using var source = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = paths.Database, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        await using var target = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = destination, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString());
        await source.OpenAsync(cancellationToken); await target.OpenAsync(cancellationToken); source.BackupDatabase(target);
        await target.CloseAsync(); var file = new FileInfo(destination);
        return new DatabaseBackupInfo(file.FullName, file.LastWriteTimeUtc, file.Length);
    }
}
