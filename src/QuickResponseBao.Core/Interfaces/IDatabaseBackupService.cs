namespace QuickResponseBao.Core.Interfaces;

public sealed record DatabaseBackupInfo(string Path, DateTimeOffset CreatedAt, long Size);
public sealed record DatabaseRestoreResult(bool Succeeded, string Message, string? SafetyBackupPath);

public interface IDatabaseBackupService
{
    Task<DatabaseBackupInfo> CreateBackupAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DatabaseBackupInfo>> GetBackupsAsync(CancellationToken cancellationToken = default);
    Task<bool> ValidateAsync(string path, CancellationToken cancellationToken = default);
    Task<DatabaseRestoreResult> RestoreAsync(string path, CancellationToken cancellationToken = default);
}
