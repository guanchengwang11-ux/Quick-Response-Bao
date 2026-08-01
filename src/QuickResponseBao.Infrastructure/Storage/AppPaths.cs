namespace QuickResponseBao.Infrastructure.Storage;

public sealed class AppPaths
{
    public AppPaths(string? root = null)
    {
        Root = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickResponseBao");
        Data = Path.Combine(Root, "data");
        Config = Path.Combine(Root, "config");
        Logs = Path.Combine(Root, "logs");
        Backups = Path.Combine(Root, "backups");
        Updates = Path.Combine(Root, "updates");
        foreach (var path in new[] { Root, Data, Config, Logs, Backups, Updates }) Directory.CreateDirectory(path);
    }

    public string Root { get; }
    public string Data { get; }
    public string Config { get; }
    public string Logs { get; }
    public string Backups { get; }
    public string Updates { get; }
    public string Database => Path.Combine(Data, "quick-responses.db");
    public string Settings => Path.Combine(Config, "settings.json");
}
