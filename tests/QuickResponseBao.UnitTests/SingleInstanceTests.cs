using QuickResponseBao.Infrastructure.Windows;

namespace QuickResponseBao.UnitTests;

public sealed class SingleInstanceTests
{
    [Fact]
    public void FirstInstance_AcquiresMutex_SecondInstanceIsRejected()
    {
        var suffix = Guid.NewGuid().ToString("N");
        using var first = new SingleInstanceService($@"Local\Qrb.Test.{suffix}", $"Qrb.Test.{suffix}");
        using var second = new SingleInstanceService($@"Local\Qrb.Test.{suffix}", $"Qrb.Test.{suffix}");
        Assert.True(first.TryAcquire()); Assert.True(first.IsPrimary); Assert.False(second.TryAcquire()); Assert.False(second.IsPrimary);
    }

    [Fact]
    public void RepeatedAcquire_OnPrimaryDoesNotCreateAnotherOwnershipPath()
    {
        using var service = new SingleInstanceService($@"Local\Qrb.Test.{Guid.NewGuid():N}");
        Assert.True(service.TryAcquire()); Assert.True(service.TryAcquire());
    }

    [Fact]
    public void AppChecksSingleInstanceBeforeCreatingListenerAndCandidate()
    {
        var source = Read("src", "QuickResponseBao.App", "App.xaml.cs");
        Assert.True(source.IndexOf("TryAcquire()", StringComparison.Ordinal) < source.IndexOf("new CandidateWindow", StringComparison.Ordinal));
        Assert.True(source.IndexOf("TryAcquire()", StringComparison.Ordinal) < source.IndexOf("new GlobalKeyboardListener", StringComparison.Ordinal));
    }

    [Fact]
    public void CandidateWindowExposesLiveInstanceDiagnosticAndAppReusesOneField()
    {
        var candidate = Read("src", "QuickResponseBao.App", "CandidateWindow.xaml.cs"); var app = Read("src", "QuickResponseBao.App", "App.xaml.cs");
        Assert.Contains("LiveInstanceCount", candidate); Assert.Equal(1, app.Split("new CandidateWindow()", StringSplitOptions.None).Length - 1); Assert.Contains("_candidates!.ShowResults", app);
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([Root(), .. parts]));
    private static string Root() { var directory = new DirectoryInfo(AppContext.BaseDirectory); while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Directory.Build.props"))) directory = directory.Parent; return directory?.FullName ?? throw new DirectoryNotFoundException(); }
}
