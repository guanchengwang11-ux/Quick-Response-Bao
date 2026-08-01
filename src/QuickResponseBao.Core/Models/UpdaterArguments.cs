namespace QuickResponseBao.Core.Models;

public enum UpdateInstallMode { Package, Setup }

public sealed record UpdaterArguments(
    int ProcessId,
    string PackagePath,
    string TargetDirectory,
    string ExecutableName,
    UpdateInstallMode Mode,
    string LogPath,
    IReadOnlyList<string> RestartArguments)
{
    public static bool TryParse(IReadOnlyList<string> args, out UpdaterArguments? options, out string error)
    {
        options = null; error = string.Empty;
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var restart = new List<string>();
        for (var index = 0; index < args.Count; index++)
        {
            var key = args[index];
            if (key == "--restart-arg" && index + 1 < args.Count) { restart.Add(args[++index]); continue; }
            if (!key.StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Count) { error = $"Invalid argument near '{key}'."; return false; }
            values[key] = args[++index];
        }
        if (!values.TryGetValue("--process-id", out var processText) || !int.TryParse(processText, out var processId) || processId < 0)
        { error = "A valid --process-id is required."; return false; }
        foreach (var required in new[] { "--package", "--target", "--executable" })
            if (!values.TryGetValue(required, out var requiredValue) || string.IsNullOrWhiteSpace(requiredValue))
            { error = $"{required} is required."; return false; }
        var package = values["--package"]; var target = values["--target"]; var executable = values["--executable"];
        if (Path.GetFileName(executable) != executable) { error = "--executable must be a file name, not a path."; return false; }
        var modeText = values.TryGetValue("--mode", out var requestedMode) ? requestedMode : "package";
        if (!modeText.Equals("package", StringComparison.OrdinalIgnoreCase) && !modeText.Equals("setup", StringComparison.OrdinalIgnoreCase))
        { error = "--mode must be package or setup."; return false; }
        var mode = modeText.Equals("setup", StringComparison.OrdinalIgnoreCase) ? UpdateInstallMode.Setup : UpdateInstallMode.Package;
        var log = values.TryGetValue("--log", out var logText) && !string.IsNullOrWhiteSpace(logText)
            ? Path.GetFullPath(logText) : Path.Combine(Path.GetTempPath(), "QuickResponseBao-updater.log");
        options = new UpdaterArguments(processId, Path.GetFullPath(package), Path.GetFullPath(target), executable, mode, log, restart);
        return true;
    }
}

public static class UpdatePathPolicy
{
    private static readonly string[] ProtectedDirectories = ["data", "config", "logs", "backups", "updates"];

    public static bool IsProtectedUserDataPath(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        var first = normalized.Split('/', 2)[0];
        return ProtectedDirectories.Contains(first, StringComparer.OrdinalIgnoreCase) ||
            normalized.EndsWith(".db", StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith("settings.json", StringComparison.OrdinalIgnoreCase);
    }
}
