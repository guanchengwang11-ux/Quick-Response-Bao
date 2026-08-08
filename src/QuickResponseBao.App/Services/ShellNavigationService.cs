using System.Windows.Controls;

namespace QuickResponseBao.App.Services;

public static class ShellRoutes
{
    public const string Dashboard = nameof(Dashboard);
    public const string Library = nameof(Library);
    public const string Categories = nameof(Categories);
    public const string ImportExport = nameof(ImportExport);
    public const string Applications = nameof(Applications);
    public const string Diagnostics = nameof(Diagnostics);
    public const string Settings = nameof(Settings);
    public const string About = nameof(About);

    public static string Normalize(string? route) => route switch
    {
        Library => Library, Categories => Categories, ImportExport => ImportExport,
        Applications => Applications, Diagnostics => Diagnostics, Settings => Settings, About => About,
        _ => Dashboard
    };
}

public sealed class ShellNavigationService
{
    private readonly Dictionary<string, Func<Page>> _factories = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Page> _cache = new(StringComparer.Ordinal);

    public void Register(string route, Func<Page> factory) => _factories[ShellRoutes.Normalize(route)] = factory;

    public Page GetPage(string route)
    {
        route = ShellRoutes.Normalize(route);
        if (_cache.TryGetValue(route, out var page)) return page;
        if (!_factories.TryGetValue(route, out var factory)) throw new InvalidOperationException($"No page is registered for route '{route}'.");
        return _cache[route] = factory();
    }

    public bool TryGetCached<TPage>(string route, out TPage? page) where TPage : Page
    {
        page = _cache.TryGetValue(ShellRoutes.Normalize(route), out var value) ? value as TPage : null;
        return page is not null;
    }

    public int CachedPageCount => _cache.Count;
}
