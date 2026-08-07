using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using QuickResponseBao.Core.Interfaces;
using QuickResponseBao.Core.Models;
using QuickResponseBao.Core.Services;

namespace QuickResponseBao.Infrastructure.Storage;

public sealed class SqliteQuickResponseRepository(AppPaths paths) : IQuickResponseRepository, ICategoryRepository
{
    public const string UncategorizedName = "Uncategorized";
    private static readonly string[] DefaultCategories =
        ["General", "Account", "Deposit", "Withdrawal", "Verification", "Security", "Complaint", "Technical Issue", "Promotion", UncategorizedName];
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
            CREATE TABLE IF NOT EXISTS categories (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL COLLATE NOCASE UNIQUE,
                sort_order INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        await SeedCategoriesAsync(connection, cancellationToken);
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
        if (!QuickResponseRules.IsSummaryValid(item.Summary)) throw new ArgumentException("Summary must contain 2-150 characters.");
        if (string.IsNullOrWhiteSpace(item.Content)) throw new ArgumentException("Content is required.");
        var contentValidationError = QuickResponseRules.GetContentValidationErrorCode(item.Content);
        if (!string.IsNullOrEmpty(contentValidationError)) throw new ArgumentException(contentValidationError);
        item.Keywords = KeywordNormalizer.Normalize(item.Keywords);
        item.Category = NormalizeCategoryName(string.IsNullOrWhiteSpace(item.Category) ? "General" : item.Category);
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await using var connection = await OpenAsync(cancellationToken);
        await EnsureCategoryRecordAsync(connection, item.Category, cancellationToken);
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

    public Task<BatchOperationResult> SetEnabledAsync(IReadOnlyCollection<Guid> ids, bool enabled, CancellationToken cancellationToken = default) =>
        ExecuteForIdsAsync(ids, "UPDATE quick_responses SET is_enabled=$value,updated_at=$now WHERE id IN ({0})", cancellationToken,
            command => command.Parameters.AddWithValue("$value", enabled ? 1 : 0));

    public Task<BatchOperationResult> DeleteManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
        ExecuteForIdsAsync(ids, "DELETE FROM quick_responses WHERE id IN ({0})", cancellationToken);

    public async Task<BatchOperationResult> MoveToCategoryAsync(IReadOnlyCollection<Guid> ids, string category, CancellationToken cancellationToken = default)
    {
        category = NormalizeCategoryName(category);
        await EnsureCategoryExistsAsync(category, cancellationToken);
        return await ExecuteForIdsAsync(ids, "UPDATE quick_responses SET category=$category,updated_at=$now WHERE id IN ({0})", cancellationToken,
            command => command.Parameters.AddWithValue("$category", category));
    }

    public async Task<IReadOnlyList<CategoryInfo>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<CategoryInfo>();
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT id,name,sort_order,created_at,updated_at FROM categories ORDER BY sort_order,id";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(new CategoryInfo
        {
            Id = Guid.Parse(reader.GetString(0)), Name = reader.GetString(1), SortOrder = reader.GetInt32(2),
            CreatedAt = DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture),
            UpdatedAt = DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture)
        });
        return result;
    }

    public async Task<CategoryInfo> AddCategoryAsync(string name, CancellationToken cancellationToken = default)
    {
        name = NormalizeCategoryName(name); var item = new CategoryInfo { Name = name };
        await using var connection = await OpenAsync(cancellationToken);
        var orderCommand = connection.CreateCommand(); orderCommand.CommandText = "SELECT COALESCE(MAX(sort_order),-1)+1 FROM categories";
        item.SortOrder = Convert.ToInt32(await orderCommand.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO categories(id,name,sort_order,created_at,updated_at) VALUES($id,$name,$order,$created,$updated)";
        command.Parameters.AddWithValue("$id", item.Id.ToString()); command.Parameters.AddWithValue("$name", item.Name);
        command.Parameters.AddWithValue("$order", item.SortOrder); command.Parameters.AddWithValue("$created", item.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$updated", item.UpdatedAt.ToString("O"));
        try { await command.ExecuteNonQueryAsync(cancellationToken); }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19) { throw new InvalidOperationException("A category with this name already exists.", ex); }
        return item;
    }

    public async Task RenameCategoryAsync(Guid id, string newName, CancellationToken cancellationToken = default)
    {
        newName = NormalizeCategoryName(newName);
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var oldName = await GetCategoryNameAsync(connection, id, transaction, cancellationToken);
        if (oldName.Equals(UncategorizedName, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("The Uncategorized category cannot be renamed.");
        var now = DateTimeOffset.UtcNow.ToString("O");
        var categoryCommand = connection.CreateCommand(); categoryCommand.Transaction = transaction;
        categoryCommand.CommandText = "UPDATE categories SET name=$newName,updated_at=$now WHERE id=$id";
        categoryCommand.Parameters.AddWithValue("$newName", newName); categoryCommand.Parameters.AddWithValue("$now", now); categoryCommand.Parameters.AddWithValue("$id", id.ToString());
        try { await categoryCommand.ExecuteNonQueryAsync(cancellationToken); }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19) { throw new InvalidOperationException("A category with this name already exists.", ex); }
        var responseCommand = connection.CreateCommand(); responseCommand.Transaction = transaction;
        responseCommand.CommandText = "UPDATE quick_responses SET category=$newName,updated_at=$now WHERE category=$oldName COLLATE NOCASE";
        responseCommand.Parameters.AddWithValue("$newName", newName); responseCommand.Parameters.AddWithValue("$oldName", oldName); responseCommand.Parameters.AddWithValue("$now", now);
        await responseCommand.ExecuteNonQueryAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
    }

    public async Task<int> CountResponsesAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var name = await GetCategoryNameAsync(connection, categoryId, null, cancellationToken);
        var command = connection.CreateCommand(); command.CommandText = "SELECT COUNT(*) FROM quick_responses WHERE category=$name COLLATE NOCASE";
        command.Parameters.AddWithValue("$name", name); return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    public async Task DeleteCategoryAsync(Guid id, bool moveResponsesToUncategorized, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var name = await GetCategoryNameAsync(connection, id, transaction, cancellationToken);
        if (name.Equals(UncategorizedName, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("The Uncategorized category cannot be deleted.");
        var countCommand = connection.CreateCommand(); countCommand.Transaction = transaction;
        countCommand.CommandText = "SELECT COUNT(*) FROM quick_responses WHERE category=$name COLLATE NOCASE"; countCommand.Parameters.AddWithValue("$name", name);
        var count = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        if (count > 0 && !moveResponsesToUncategorized) throw new InvalidOperationException("The category still contains responses.");
        if (count > 0)
        {
            var moveCommand = connection.CreateCommand(); moveCommand.Transaction = transaction;
            moveCommand.CommandText = "UPDATE quick_responses SET category=$uncategorized,updated_at=$now WHERE category=$name COLLATE NOCASE";
            moveCommand.Parameters.AddWithValue("$uncategorized", UncategorizedName); moveCommand.Parameters.AddWithValue("$name", name);
            moveCommand.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O")); await moveCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        var deleteCommand = connection.CreateCommand(); deleteCommand.Transaction = transaction;
        deleteCommand.CommandText = "DELETE FROM categories WHERE id=$id"; deleteCommand.Parameters.AddWithValue("$id", id.ToString());
        await deleteCommand.ExecuteNonQueryAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
    }

    public async Task ReorderCategoriesAsync(IReadOnlyList<Guid> categoryIds, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        for (var index = 0; index < categoryIds.Count; index++)
        {
            var command = connection.CreateCommand(); command.Transaction = transaction;
            command.CommandText = "UPDATE categories SET sort_order=$order,updated_at=$now WHERE id=$id";
            command.Parameters.AddWithValue("$order", index); command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("$id", categoryIds[index].ToString());
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1) throw new InvalidOperationException("A category no longer exists.");
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task BackupAsync(string destination, CancellationToken cancellationToken = default)
    {
        await using var source = await OpenAsync(cancellationToken);
        await using var target = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = destination }.ToString());
        await target.OpenAsync(cancellationToken);
        source.BackupDatabase(target);
    }

    private async Task<BatchOperationResult> ExecuteForIdsAsync(
        IReadOnlyCollection<Guid> ids, string sql, CancellationToken cancellationToken, Action<SqliteCommand>? configure = null)
    {
        var unique = ids.Distinct().ToList(); if (unique.Count == 0) return BatchOperationResult.Success(0, 0);
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        var placeholders = new List<string>();
        for (var i = 0; i < unique.Count; i++) { var name = $"$id{i}"; placeholders.Add(name); command.Parameters.AddWithValue(name, unique[i].ToString()); }
        command.CommandText = string.Format(CultureInfo.InvariantCulture, sql, string.Join(',', placeholders));
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O")); configure?.Invoke(command);
        var processed = await command.ExecuteNonQueryAsync(cancellationToken);
        return BatchOperationResult.Success(unique.Count, processed);
    }

    private async Task SeedCategoriesAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        for (var i = 0; i < DefaultCategories.Length; i++)
        {
            var command = connection.CreateCommand(); command.CommandText = "INSERT OR IGNORE INTO categories(id,name,sort_order,created_at,updated_at) VALUES($id,$name,$order,$now,$now)";
            command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString()); command.Parameters.AddWithValue("$name", DefaultCategories[i]);
            command.Parameters.AddWithValue("$order", i); command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O")); await command.ExecuteNonQueryAsync(cancellationToken);
        }
        var custom = connection.CreateCommand();
        custom.CommandText = """
            INSERT OR IGNORE INTO categories(id,name,sort_order,created_at,updated_at)
            SELECT lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-4' || substr(lower(hex(randomblob(2))),2) || '-a' || substr(lower(hex(randomblob(2))),2) || '-' || lower(hex(randomblob(6))),
                   category, (SELECT COALESCE(MAX(sort_order),0)+1 FROM categories), $now, $now
            FROM quick_responses WHERE trim(category) <> '' GROUP BY category;
            """;
        custom.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O")); await custom.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureCategoryRecordAsync(SqliteConnection connection, string name, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR IGNORE INTO categories(id,name,sort_order,created_at,updated_at)
            VALUES($id,$name,(SELECT COALESCE(MAX(sort_order),-1)+1 FROM categories),$now,$now)
            """;
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString()); command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O")); await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task EnsureCategoryExistsAsync(string name, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand(); command.CommandText = "SELECT COUNT(*) FROM categories WHERE name=$name COLLATE NOCASE";
        command.Parameters.AddWithValue("$name", name);
        if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) == 0)
            throw new InvalidOperationException("The target category does not exist.");
    }

    private static async Task<string> GetCategoryNameAsync(SqliteConnection connection, Guid id, System.Data.Common.DbTransaction? transaction, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand(); command.Transaction = transaction as SqliteTransaction;
        command.CommandText = "SELECT name FROM categories WHERE id=$id"; command.Parameters.AddWithValue("$id", id.ToString());
        return await command.ExecuteScalarAsync(cancellationToken) as string ?? throw new InvalidOperationException("The category does not exist.");
    }

    private static string NormalizeCategoryName(string name)
    {
        name = name.Trim();
        if (name.Length is < 1 or > 100) throw new ArgumentException("Category name must contain 1-100 characters.");
        return name;
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
        Keywords = KeywordNormalizer.Normalize(JsonSerializer.Deserialize<List<string>>(r.GetString(r.GetOrdinal("keywords_json"))) ?? []),
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
