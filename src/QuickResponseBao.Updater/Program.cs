using System.Diagnostics;
using System.IO.Compression;

if (args.Length < 3 || !int.TryParse(args[0], out var processId))
{
    Console.Error.WriteLine("Usage: QuickResponseBao.Updater <processId> <update.zip> <installDirectory> [executable]");
    return 2;
}

var archive = Path.GetFullPath(args[1]);
var destination = Path.GetFullPath(args[2]);
var executable = args.Length > 3 ? args[3] : "QuickResponseBao.exe";
if (!File.Exists(archive) || !Directory.Exists(destination))
{
    Console.Error.WriteLine("The update archive or install directory does not exist.");
    return 3;
}

try
{
    try { using var process = Process.GetProcessById(processId); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30)); }
    catch (ArgumentException) { }
    var staging = Path.Combine(Path.GetTempPath(), $"QuickResponseBao-update-{Guid.NewGuid():N}");
    Directory.CreateDirectory(staging); ZipFile.ExtractToDirectory(archive, staging);
    foreach (var source in Directory.EnumerateFiles(staging, "*", SearchOption.AllDirectories))
    {
        var relative = Path.GetRelativePath(staging, source);
        var target = Path.Combine(destination, relative); Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(source, target, true);
    }
    Directory.Delete(staging, true);
    Process.Start(new ProcessStartInfo(Path.Combine(destination, executable)) { UseShellExecute = true });
    return 0;
}
catch (UnauthorizedAccessException ex) { Console.Error.WriteLine($"Permission denied: {ex.Message}"); return 4; }
catch (IOException ex) { Console.Error.WriteLine($"File replacement failed: {ex.Message}"); return 5; }
catch (Exception ex) { Console.Error.WriteLine($"Update installation failed: {ex.Message}"); return 6; }
