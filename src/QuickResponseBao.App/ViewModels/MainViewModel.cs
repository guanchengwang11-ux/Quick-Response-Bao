using System.Collections.ObjectModel;
using QuickResponseBao.Core.Interfaces;
using QuickResponseBao.Core.Models;
using QuickResponseBao.Core.Services;

namespace QuickResponseBao.App.ViewModels;

public sealed class MainViewModel(IQuickResponseRepository repository, SearchService searchService) : ViewModelBase
{
    private string _searchText = string.Empty;
    private QuickResponse? _selected;
    private AppSettings _settings = new();
    public ObservableCollection<QuickResponse> Responses { get; } = [];
    public AppSettings Settings { get => _settings; set => Set(ref _settings, value); }
    public string SearchText { get => _searchText; set { if (Set(ref _searchText, value)) _ = RefreshAsync(); } }
    public QuickResponse? SelectedResponse { get => _selected; set => Set(ref _selected, value); }
    public int TotalCount => Responses.Count;
    public int EnabledCount => Responses.Count(x => x.IsEnabled);
    public int TodayUsageCount => Responses.Count(x => x.LastUsedAt?.LocalDateTime.Date == DateTime.Today);
    public IReadOnlyList<QuickResponse> RecentResponses => Responses.Where(x => x.LastUsedAt is not null)
        .OrderByDescending(x => x.LastUsedAt).Take(5).ToList();

    public async Task RefreshAsync()
    {
        var all = await repository.GetAllAsync();
        var filtered = string.IsNullOrWhiteSpace(SearchText) ? all :
            searchService.Search(all, SearchText, new SearchOptions(MaximumResults: 30)).Select(x => x.Response).ToList();
        Responses.Clear(); foreach (var item in filtered) Responses.Add(item);
        Notify(nameof(TotalCount)); Notify(nameof(EnabledCount)); Notify(nameof(TodayUsageCount)); Notify(nameof(RecentResponses));
    }
}
