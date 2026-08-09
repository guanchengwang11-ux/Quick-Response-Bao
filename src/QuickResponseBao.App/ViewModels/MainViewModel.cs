using QuickResponseBao.Core.Collections;
using QuickResponseBao.Core.Interfaces;
using QuickResponseBao.Core.Models;
using QuickResponseBao.Core.Services;

namespace QuickResponseBao.App.ViewModels;

public sealed class MainViewModel(IQuickResponseRepository repository, SearchService searchService) : ViewModelBase
{
    private string _searchText = string.Empty;
    private QuickResponse? _selected;
    private AppSettings _settings = new();
    private Task? _initialLoadTask;
    public BulkObservableCollection<QuickResponse> Responses { get; } = [];
    public IQuickResponseRepository Repository => repository;
    public SearchService SearchService => searchService;
    public AppSettings Settings { get => _settings; set => Set(ref _settings, value); }
    public string SearchText { get => _searchText; set => Set(ref _searchText, value); }
    public bool IsLoaded { get; private set; }
    public int DataVersion { get; private set; }
    public QuickResponse? SelectedResponse { get => _selected; set => Set(ref _selected, value); }
    public int TotalCount => Responses.Count;
    public int EnabledCount => Responses.Count(x => x.IsEnabled);
    public int TodayUsageCount => Responses.Count(x => x.LastUsedAt?.LocalDateTime.Date == DateTime.Today);
    public IReadOnlyList<QuickResponse> RecentResponses => Responses.Where(x => x.LastUsedAt is not null)
        .OrderByDescending(x => x.LastUsedAt).Take(5).ToList();

    public Task EnsureLoadedAsync() => IsLoaded ? Task.CompletedTask : _initialLoadTask ??= RefreshAsync();

    public async Task RefreshAsync()
    {
        var all = await repository.GetAllAsync();
        Responses.ReplaceRange(all);
        IsLoaded = true; DataVersion++;
        Notify(nameof(TotalCount)); Notify(nameof(EnabledCount)); Notify(nameof(TodayUsageCount)); Notify(nameof(RecentResponses));
    }
}
