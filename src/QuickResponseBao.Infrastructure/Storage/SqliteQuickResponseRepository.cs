using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using QuickResponseBao.Core.Interfaces;
using QuickResponseBao.Core.Models;

namespace QuickResponseBao.Infrastructure.Storage;

public sealed class SqliteQuickResponseRepository(AppPaths paths) : IQuickResponseRepository
{
    private string ConnectionString => new SqliteConnectionStringBuilder
    {
        DataSource = paths.Database,
        Mode = SqliteOpenMode.ReadWriteCreate,
        Cache = SqliteCacheMode.Shared,
        Pooling = false
    }.ToString();

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS quick_responses (
                id TEXT PRIMARY KEY,
                summary TEXT NOT NULL,
                content TEXT NOT NULL,
                keywords_json TEXT NOT NULL DEFAULT '[]',
                category TEXT NOT NULL DEFAULT 'General',
                language TEXT NOT NULL DEFAULT 'English',
                is_enabled INTEGER NOT NULL DEFAULT 1,
                sort_order INTEGER NOT NULL DEFAULT 0,
                usage_count INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                last_used_at TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_quick_responses_enabled ON quick_responses(is_enabled);
            CREATE INDEX IF NOT EXISTS ix_quick_responses_category ON quick_responses(category);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<QuickResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<QuickResponse>();
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM quick_responses ORDER BY sort_order DESC, updated_at DESC";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(Read(reader));
        return result;
    }

    public async Task<QuickResponse?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM quick_responses WHERE id = $id";
        command.Parameters.AddWithValue("$id", id.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    public async Task UpsertAsync(QuickResponse item, CancellationToken cancellationToken = default)
    {
        if (item.Summary.Trim().Length is < 2 or > 150) throw new ArgumentException("Summary must contain 2-150 characters.");
        if (string.IsNullOrWhiteSpace(item.Content)) throw new ArgumentException("Content is required.");
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO quick_responses
                (id, summary, content, keywords_json, category, language, is_enabled, sort_order,
                 usage_count, created_at, updated_at, last_used_at)
            VALUES ($id,$summary,$content,$keywords,$category,$language,$enabled,$sortOrder,
                    $usageCount,$createdAt,$updatedAt,$lastUsedAt)
            ON CONFLICT(id) DO UPDATE SET
                summary=excluded.summary, content=excluded.content, keywords_json=excluded.keywords_json,
                category=excluded.category, language=excluded.language, is_enabled=excluded.is_enabled,
                sort_order=excluded.sort_order, usage_count=excluded.usage_count,
                updated_at=excluded.updated_at, last_used_at=excluded.last_used_at;
            """;
        AddParameters(command, item);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM quick_responses WHERE id=$id";
        command.Parameters.AddWithValue("$id", id.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task IncrementUsageAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE quick_responses SET usage_count=usage_count+1,last_used_at=$now WHERE id=$id";
        command.Parameters.AddWithValue("$id", id.ToString());
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task BackupAsync(string destination, CancellationToken cancellationToken = default)
    {
        await using var source = await OpenAsync(cancellationToken);
        await using var target = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = destination }.ToString());
        await target.OpenAsync(cancellationToken);
        source.BackupDatabase(target);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static void AddParameters(SqliteCommand command, QuickResponse x)
    {
        command.Parameters.AddWithValue("$id", x.Id.ToString());
        command.Parameters.AddWithValue("$summary", x.Summary.Trim());
        command.Parameters.AddWithValue("$content", x.Content);
        command.Parameters.AddWithValue("$keywords", JsonSerializer.Serialize(x.Keywords));
        command.Parameters.AddWithValue("$category", x.Category);
        command.Parameters.AddWithValue("$language", x.Language);
        command.Parameters.AddWithValue("$enabled", x.IsEnabled ? 1 : 0);
        command.Parameters.AddWithValue("$sortOrder", x.SortOrder);
        command.Parameters.AddWithValue("$usageCount", x.UsageCount);
        command.Parameters.AddWithValue("$createdAt", x.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$updatedAt", x.UpdatedAt.ToString("O"));
        command.Parameters.AddWithValue("$lastUsedAt", x.LastUsedAt?.ToString("O") ?? (object)DBNull.Value);
    }

    private static QuickResponse Read(SqliteDataReader r) => new()
    {
        Id = Guid.Parse(r.GetString(r.GetOrdinal("id"))),
        Summary = r.GetString(r.GetOrdinal("summary")),
        Content = r.GetString(r.GetOrdinal("content")),
        Keywords = JsonSerializer.Deserialize<List<string>>(r.GetString(r.GetOrdinal("keywords_json"))) ?? [],
        Category = r.GetString(r.GetOrdinal("category")),
        Language = r.GetString(r.GetOrdinal("language")),
        IsEnabled = r.GetInt32(r.GetOrdinal("is_enabled")) != 0,
        SortOrder = r.GetInt32(r.GetOrdinal("sort_order")),
        UsageCount = r.GetInt64(r.GetOrdinal("usage_count")),
        CreatedAt = DateTimeOffset.Parse(r.GetString(r.GetOrdinal("created_at")), CultureInfo.InvariantCulture),
        UpdatedAt = DateTimeOffset.Parse(r.GetString(r.GetOrdinal("updated_at")), CultureInfo.InvariantCulture),
        LastUsedAt = r.IsDBNull(r.GetOrdinal("last_used_at")) ? null :
            DateTimeOffset.Parse(r.GetString(r.GetOrdinal("last_used_at")), CultureInfo.InvariantCulture)
    };
}
