using System.Diagnostics;
using QuickResponseBao.Core.Models;
using QuickResponseBao.Infrastructure.Storage;

namespace QuickResponseBao.Infrastructure.Updates;

public sealed class UpdateInstallerLauncher(AppPaths paths)
{
    public Process Launch(string packagePath, ReleaseAssetKind kind, string targetDirectory, string executableName, IEnumerable<string>? restartArguments = null)
    {
        var installedUpdater = Path.Combine(targetDirectory, "QuickResponseBao.Updater.exe");
        if (!File.Exists(installedUpdater)) throw new FileNotFoundException("QuickResponseBao.Updater.exe is missing from the application directory.", installedUpdater);
        var runnerDirectory = Path.Combine(paths.Updates, $"updater-run-{Guid.NewGuid():N}"); Directory.CreateDirectory(runnerDirectory);
        foreach (var source in Directory.EnumerateFiles(targetDirectory, "QuickResponseBao.Updater*", SearchOption.TopDirectoryOnly)
                     .Concat(Directory.EnumerateFiles(targetDirectory, "QuickResponseBao.Core.dll", SearchOption.TopDirectoryOnly)))
            File.Copy(source, Path.Combine(runnerDirectory, Path.GetFileName(source)), true);
        var runner = Path.Combine(runnerDirectory, "QuickResponseBao.Updater.exe");
        var start = new ProcessStartInfo(runner) { UseShellExecute = false, WorkingDirectory = runnerDirectory };
        Add("--process-id", Environment.ProcessId.ToString()); Add("--package", Path.GetFullPath(packagePath));
        Add("--target", Path.GetFullPath(targetDirectory)); Add("--executable", executableName);
        Add("--mode", kind == ReleaseAssetKind.Setup ? "setup" : "package");
        Add("--log", Path.Combine(paths.Logs, $"updater-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log"));
        foreach (var argument in restartArguments ?? []) Add("--restart-arg", argument);
        return Process.Start(start) ?? throw new InvalidOperationException("The independent updater could not be started.");

        void Add(string key, string value) { start.ArgumentList.Add(key); start.ArgumentList.Add(value); }
    }
}
