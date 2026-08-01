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
    public void Dispose()
    {
        for (var attempt = 0; Directory.Exists(Root); attempt++)
        {
            try
            {
                Directory.Delete(Root, true);
                return;
            }
            catch (IOException) when (attempt < 19)
            {
                Thread.Sleep(50 * (attempt + 1));
            }
            catch (UnauthorizedAccessException) when (attempt < 19)
            {
                Thread.Sleep(50 * (attempt + 1));
            }
        }
    }
}
