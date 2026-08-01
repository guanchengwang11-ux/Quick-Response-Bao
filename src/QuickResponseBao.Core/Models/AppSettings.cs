namespace QuickResponseBao.Core.Models;

public sealed class AppSettings
{
    public string Language { get; set; } = "zh-CN";
    public string Theme { get; set; } = "System";
    public bool MinimizeToTrayOnClose { get; set; } = true;
    public bool EnableListenerOnStartup { get; set; } = true;
    public bool StartMinimized { get; set; }
    public bool StartWithWindows { get; set; }
    public bool ShowNotifications { get; set; } = true;
    public bool GlobalSearchEnabled { get; set; } = true;
    public int MinimumTriggerLength { get; set; } = 4;
    public int MaximumSuggestions { get; set; } = 10;
    public bool MatchSummary { get; set; } = true;
    public bool MatchContent { get; set; } = true;
    public bool MatchKeywords { get; set; } = true;
    public bool MatchCategory { get; set; } = true;
    public bool CaseSensitive { get; set; }
    public bool SortByUsage { get; set; } = true;
    public bool AutoPasteEnabled { get; set; } = true;
    public bool PreserveClipboard { get; set; } = true;
    public bool RestoreClipboard { get; set; } = true;
    public int ClipboardRestoreDelayMs { get; set; } = 500;
    public bool CheckUpdatesOnStartup { get; set; } = true;
    public bool AutoDownloadUpdates { get; set; }
    public bool NotifyOnlyForUpdates { get; set; } = true;
    public bool IncludePrereleaseUpdates { get; set; }
    public List<string> AllowedProcesses { get; set; } =
        ["Lark.exe", "Telegram.exe", "Discord.exe", "chrome.exe", "msedge.exe"];

    public void Normalize()
    {
        MinimumTriggerLength = Math.Clamp(MinimumTriggerLength, 2, 20);
        MaximumSuggestions = Math.Clamp(MaximumSuggestions, 3, 30);
        ClipboardRestoreDelayMs = Math.Clamp(ClipboardRestoreDelayMs, 100, 5000);
        Theme = ThemeMode.Normalize(Theme);
        if (AutoDownloadUpdates) NotifyOnlyForUpdates = false;
        AllowedProcesses = AllowedProcesses.Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
