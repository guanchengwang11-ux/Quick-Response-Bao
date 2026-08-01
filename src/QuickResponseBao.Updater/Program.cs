using System.Diagnostics;
using System.IO.Compression;
using QuickResponseBao.Core.Models;

if (!UpdaterArguments.TryParse(args, out var options, out var parseError) || options is null)
{
    Console.Error.WriteLine(parseError);
    Console.Error.WriteLine("Usage: QuickResponseBao.Updater --process-id <id> --package <path> --target <dir> --executable <name> [--mode package|setup] [--log <path>] [--restart-arg <value>]");
    return 2;
}

Directory.CreateDirectory(Path.GetDirectoryName(options.LogPath)!);
await LogAsync("Updater started.");
try
{
    if (!File.Exists(options.PackagePath) || !Directory.Exists(options.TargetDirectory))
        throw new FileNotFoundException("The update file or target directory does not exist.");
    await WaitForMainProcessAsync(options.ProcessId);
    if (options.Mode == UpdateInstallMode.Setup) { await RunSetupSilentlyAsync(); RestartApplication(); }
    else await InstallPackageWithRollbackAndRestartAsync();
    await LogAsync("Update completed; application restart requested."); return 0;
}
catch (Exception ex)
{
    await LogAsync($"Update failed: {ex.GetType().Name}: {ex.Message}");
    Console.Error.WriteLine($"Update failed: {ex.Message}. See log: {options.LogPath}"); return 6;
}

async Task WaitForMainProcessAsync(int processId)
{
    if (processId == 0) return;
    try
    {
        using var process = Process.GetProcessById(processId);
        await LogAsync($"Waiting for process {processId} to exit.");
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(2));
    }
    catch (ArgumentException) { }
}

async Task RunSetupSilentlyAsync()
{
    await LogAsync("Starting Inno Setup with /SILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS and the current target directory.");
    var start = new ProcessStartInfo(options.PackagePath) { UseShellExecute = true };
    foreach (var argument in new[] { "/SILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/CLOSEAPPLICATIONS", $"/DIR={options.TargetDirectory}" }) start.ArgumentList.Add(argument);
    using var installer = Process.Start(start)
        ?? throw new InvalidOperationException("Setup.exe could not be started.");
    await installer.WaitForExitAsync();
    if (installer.ExitCode != 0) throw new InvalidOperationException($"Setup.exe returned exit code {installer.ExitCode}.");
}

async Task InstallPackageWithRollbackAndRestartAsync()
{
    if (!Path.GetExtension(options.PackagePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        throw new InvalidDataException("Package mode requires a ZIP update asset.");
    var workRoot = Path.Combine(Path.GetDirectoryName(options.PackagePath)!, $"install-{Guid.NewGuid():N}");
    var staging = Path.Combine(workRoot, "staging"); var backup = Path.Combine(workRoot, "rollback");
    Directory.CreateDirectory(staging); Directory.CreateDirectory(backup);
    var replaced = new List<(string Target, string? Backup)>();
    try
    {
        ExtractSafely(options.PackagePath, staging);
        if (!File.Exists(Path.Combine(staging, options.ExecutableName)))
            throw new InvalidDataException($"The update package does not contain '{options.ExecutableName}' at its root.");
        foreach (var source in Directory.EnumerateFiles(staging, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(staging, source);
            if (UpdatePathPolicy.IsProtectedUserDataPath(relative)) { await LogAsync($"Protected user data path skipped: {relative}"); continue; }
            var target = SafeTarget(options.TargetDirectory, relative); Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            string? backupPath = null;
            if (File.Exists(target))
            {
                backupPath = Path.Combine(backup, relative); Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!); File.Copy(target, backupPath, true);
            }
            var temporary = $"{target}.update-new-{Guid.NewGuid():N}";
            File.Copy(source, temporary, true); File.Move(temporary, target, true); replaced.Add((target, backupPath));
        }
        await LogAsync($"Safely replaced {replaced.Count} program files.");
        RestartApplication();
        try { Directory.Delete(workRoot, true); } catch (Exception ex) { await LogAsync($"Non-fatal cleanup failure at {workRoot}: {ex.Message}"); }
    }
    catch
    {
        var rollbackErrors = new List<string>();
        foreach (var item in replaced.AsEnumerable().Reverse())
        {
            try
            {
                if (item.Backup is null) File.Delete(item.Target);
                else { var temporary = $"{item.Target}.rollback-{Guid.NewGuid():N}"; File.Copy(item.Backup, temporary, true); File.Move(temporary, item.Target, true); }
            }
            catch (Exception ex) { rollbackErrors.Add($"{Path.GetFileName(item.Target)}: {ex.Message}"); }
        }
        await LogAsync(rollbackErrors.Count == 0 ? "Replacement failed; old version restored." : $"Rollback incomplete; backup retained at {backup}. Errors: {string.Join(" | ", rollbackErrors)}");
        if (rollbackErrors.Count == 0) try { Directory.Delete(workRoot, true); } catch { }
        throw;
    }
}

void RestartApplication()
{
    var path = Path.Combine(options.TargetDirectory, options.ExecutableName);
    var start = new ProcessStartInfo(path) { UseShellExecute = true };
    foreach (var argument in options.RestartArguments) start.ArgumentList.Add(argument);
    _ = Process.Start(start) ?? throw new InvalidOperationException("The updated application could not be restarted.");
}

void ExtractSafely(string archivePath, string staging)
{
    var root = Path.GetFullPath(staging) + Path.DirectorySeparatorChar;
    using var archive = ZipFile.OpenRead(archivePath);
    foreach (var entry in archive.Entries)
    {
        var destination = Path.GetFullPath(Path.Combine(staging, entry.FullName));
        if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The update ZIP contains an unsafe path.");
        if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\')) { Directory.CreateDirectory(destination); continue; }
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!); entry.ExtractToFile(destination, true);
    }
}

string SafeTarget(string root, string relative)
{
    var fullRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
    var target = Path.GetFullPath(Path.Combine(root, relative));
    if (!target.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The update package targets a path outside the install directory.");
    return target;
}

Task LogAsync(string message) => File.AppendAllTextAsync(options.LogPath, $"{DateTimeOffset.Now:O} | {message.ReplaceLineEndings(" ")}{Environment.NewLine}");
