namespace QuickResponseBao.Core.Services;

public static class ProcessWhitelist
{
    public static bool Contains(IEnumerable<string> allowedProcesses, string processName) =>
        allowedProcesses.Any(x => string.Equals(x?.Trim(), processName?.Trim(), StringComparison.OrdinalIgnoreCase));
}
