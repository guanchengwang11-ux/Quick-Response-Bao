using QuickResponseBao.Infrastructure.Storage;

namespace QuickResponseBao.UnitTests;

internal sealed class TestWorkspace : IDisposable
{
    public TestWorkspace()
    {
        Root = Path.Combine(Path.GetTempPath(), $"qrb-stage2-{Guid.NewGuid():N}");
        Paths = new AppPaths(Root);
        Repository = new SqliteQuickResponseRepository(Paths);
    }
    public string Root { get; }
    public AppPaths Paths { get; }
    public SqliteQuickResponseRepository Repository { get; }
    public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
}
