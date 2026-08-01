namespace QuickResponseBao.Core.Models;

public sealed class CategoryInfo
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed record BatchOperationResult(int Requested, int Processed, int Failed, IReadOnlyList<string> Errors)
{
    public static BatchOperationResult Success(int requested, int processed) =>
        new(requested, processed, Math.Max(0, requested - processed), []);
}
