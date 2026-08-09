namespace QuickResponseBao.Core.Models;

public enum ResponseFilterField { Summary, Category, Keywords, Language, Status, UsageCount, LastUsed }
public enum TextFilterOperator { Contains, DoesNotContain, Equals, StartsWith, EndsWith }
public enum NumberFilterOperator { Equals, GreaterThan, GreaterThanOrEqual, LessThan, LessThanOrEqual, Between }
public enum LastUsedFilterPeriod { All, Today, Last7Days, Last30Days, NeverUsed, CustomRange }

public sealed record TextColumnFilter(string Value, TextFilterOperator Operator = TextFilterOperator.Contains)
{
    public bool IsActive => !string.IsNullOrWhiteSpace(Value);
    public bool Matches(string? source)
    {
        if (!IsActive) return true; source ??= string.Empty;
        return Operator switch
        {
            TextFilterOperator.DoesNotContain => !source.Contains(Value, StringComparison.OrdinalIgnoreCase),
            TextFilterOperator.Equals => source.Equals(Value, StringComparison.OrdinalIgnoreCase),
            TextFilterOperator.StartsWith => source.StartsWith(Value, StringComparison.OrdinalIgnoreCase),
            TextFilterOperator.EndsWith => source.EndsWith(Value, StringComparison.OrdinalIgnoreCase),
            _ => source.Contains(Value, StringComparison.OrdinalIgnoreCase)
        };
    }
}

public sealed record NumberColumnFilter(NumberFilterOperator Operator, int Value, int? SecondValue = null)
{
    public bool Matches(long source) => Operator switch
    {
        NumberFilterOperator.Equals => source == Value,
        NumberFilterOperator.GreaterThan => source > Value,
        NumberFilterOperator.GreaterThanOrEqual => source >= Value,
        NumberFilterOperator.LessThan => source < Value,
        NumberFilterOperator.LessThanOrEqual => source <= Value,
        NumberFilterOperator.Between => source >= Math.Min(Value, SecondValue ?? Value) && source <= Math.Max(Value, SecondValue ?? Value),
        _ => true
    };
}

public sealed record LastUsedColumnFilter(LastUsedFilterPeriod Period, DateTimeOffset? From = null, DateTimeOffset? To = null)
{
    public bool IsActive => Period != LastUsedFilterPeriod.All;
    public bool Matches(DateTimeOffset? source, DateTimeOffset now)
    {
        var localDate = source?.LocalDateTime.Date; var today = now.LocalDateTime.Date;
        return Period switch
        {
            LastUsedFilterPeriod.Today => localDate == today,
            LastUsedFilterPeriod.Last7Days => localDate is not null && localDate >= today.AddDays(-6) && localDate <= today,
            LastUsedFilterPeriod.Last30Days => localDate is not null && localDate >= today.AddDays(-29) && localDate <= today,
            LastUsedFilterPeriod.NeverUsed => source is null,
            LastUsedFilterPeriod.CustomRange => localDate is not null && (From is null || localDate >= From.Value.LocalDateTime.Date) && (To is null || localDate <= To.Value.LocalDateTime.Date),
            _ => true
        };
    }
}

public sealed class ResponseLibraryFilterState
{
    public string GlobalSearch { get; set; } = string.Empty;
    public TextColumnFilter? Summary { get; set; }
    public TextColumnFilter? Keywords { get; set; }
    public HashSet<string> Categories { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Languages { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<bool> Statuses { get; } = [];
    public NumberColumnFilter? UsageCount { get; set; }
    public LastUsedColumnFilter? LastUsed { get; set; }
    public DateTimeOffset Now { get; set; } = DateTimeOffset.Now;

    public bool IsActive(ResponseFilterField field) => field switch
    {
        ResponseFilterField.Summary => Summary?.IsActive == true,
        ResponseFilterField.Category => Categories.Count > 0,
        ResponseFilterField.Keywords => Keywords?.IsActive == true,
        ResponseFilterField.Language => Languages.Count > 0,
        ResponseFilterField.Status => Statuses.Count > 0,
        ResponseFilterField.UsageCount => UsageCount is not null,
        ResponseFilterField.LastUsed => LastUsed?.IsActive == true,
        _ => false
    };
    public bool HasColumnFilters => Enum.GetValues<ResponseFilterField>().Any(IsActive);

    public bool Matches(QuickResponse response) => Matches(response, null);

    public bool Matches(QuickResponse response, ResponseFilterField? ignoredField)
    {
        return MatchesGlobal(response)
            && (ignoredField == ResponseFilterField.Summary || (Summary?.Matches(response.Summary) ?? true))
            && (ignoredField == ResponseFilterField.Keywords || MatchesKeywords(response.Keywords))
            && (ignoredField == ResponseFilterField.Category || Categories.Count == 0 || Categories.Contains(response.Category))
            && (ignoredField == ResponseFilterField.Language || Languages.Count == 0 || Languages.Contains(response.Language))
            && (ignoredField == ResponseFilterField.Status || Statuses.Count == 0 || Statuses.Contains(response.IsEnabled))
            && (ignoredField == ResponseFilterField.UsageCount || (UsageCount?.Matches(response.UsageCount) ?? true))
            && (ignoredField == ResponseFilterField.LastUsed || (LastUsed?.Matches(response.LastUsedAt, Now) ?? true));
    }

    public void Clear(ResponseFilterField field)
    {
        switch (field)
        {
            case ResponseFilterField.Summary: Summary = null; break;
            case ResponseFilterField.Category: Categories.Clear(); break;
            case ResponseFilterField.Keywords: Keywords = null; break;
            case ResponseFilterField.Language: Languages.Clear(); break;
            case ResponseFilterField.Status: Statuses.Clear(); break;
            case ResponseFilterField.UsageCount: UsageCount = null; break;
            case ResponseFilterField.LastUsed: LastUsed = null; break;
        }
    }
    public void ClearAll() { foreach (var field in Enum.GetValues<ResponseFilterField>()) Clear(field); }

    private bool MatchesGlobal(QuickResponse response)
    {
        if (string.IsNullOrWhiteSpace(GlobalSearch)) return true;
        return response.Summary.Contains(GlobalSearch, StringComparison.OrdinalIgnoreCase) || response.Content.Contains(GlobalSearch, StringComparison.OrdinalIgnoreCase)
            || response.Category.Contains(GlobalSearch, StringComparison.OrdinalIgnoreCase) || response.Keywords.Any(x => x.Contains(GlobalSearch, StringComparison.OrdinalIgnoreCase));
    }
    private bool MatchesKeywords(IReadOnlyCollection<string> keywords)
    {
        if (Keywords?.IsActive != true) return true;
        return Keywords.Operator == TextFilterOperator.DoesNotContain ? keywords.All(Keywords.Matches) : keywords.Any(Keywords.Matches);
    }
}
