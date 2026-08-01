namespace QuickResponseBao.Core.Models;

public sealed class QuickResponse
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Summary { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<string> Keywords { get; set; } = [];
    public string Category { get; set; } = "General";
    public string Language { get; set; } = "English";
    public bool IsEnabled { get; set; } = true;
    public int SortOrder { get; set; }
    public long UsageCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastUsedAt { get; set; }
}
