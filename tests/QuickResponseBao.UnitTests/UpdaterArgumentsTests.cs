using QuickResponseBao.Core.Models;

namespace QuickResponseBao.UnitTests;

public sealed class UpdaterArgumentsTests
{
    [Fact]
    public void Parse_AcceptsNamedArgumentsAndRestartArguments()
    {
        var args = new[] { "--process-id", "42", "--package", "update.zip", "--target", ".", "--executable", "QuickResponseBao.exe", "--mode", "package", "--restart-arg", "--minimized" };
        Assert.True(UpdaterArguments.TryParse(args, out var value, out var error), error); Assert.NotNull(value);
        Assert.Equal(42, value.ProcessId); Assert.Equal(UpdateInstallMode.Package, value.Mode); Assert.Equal(["--minimized"], value.RestartArguments);
    }

    [Theory][InlineData("data/quick-responses.db")][InlineData("config/settings.json")][InlineData("logs/app.log")][InlineData("backups/backup.db")][InlineData("updates/file.zip")][InlineData("nested/other.db")]
    public void PathPolicy_ProtectsUserDataFromUpdateReplacement(string path) => Assert.True(UpdatePathPolicy.IsProtectedUserDataPath(path));
}
