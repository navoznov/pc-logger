using PcLogger.Core.Model;
using PcLogger.Core.Recording;
using PcLogger.Core.Sampling;
using PcLogger.Core.Storage;
using Xunit;

namespace PcLogger.Core.Tests;

file sealed class FakeSystemProbe : ISystemProbe
{
    public SystemSnapshot Next = new(false, false, null);
    public SystemSnapshot Sample() => Next;
}

file sealed class FakeProcessProbe : IProcessProbe
{
    public List<string> Paths = new();
    public bool Throw;
    public IReadOnlyList<string> WindowedProcessPaths() =>
        Throw ? throw new InvalidOperationException("access denied") : Paths;
}

public class RecorderServiceTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"pclog-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        // WAL mode leaves -wal and -shm sidecars next to the database.
        foreach (var f in new[] { _path, _path + "-wal", _path + "-shm" })
            if (File.Exists(f)) File.Delete(f);
    }

    [Fact]
    public void WritesOneBucketPerTenSeconds()
    {
        using var db = new Db(_path);
        var system = new FakeSystemProbe { Next = new SystemSnapshot(true, false, @"D:\a.exe") };
        var recorder = new RecorderService(system, new FakeProcessProbe(), db);

        for (var t = 1000L; t <= 1020L; t++) recorder.Tick(t);

        var buckets = new BucketStore(db).Read(0, 9999);
        Assert.Equal(2, buckets.Count);
        Assert.Equal(10, buckets[0].Active);
    }

    [Fact]
    public void ResolvesForegroundPathToAnAppId()
    {
        using var db = new Db(_path);
        var system = new FakeSystemProbe { Next = new SystemSnapshot(true, false, @"D:\a.exe") };
        var recorder = new RecorderService(system, new FakeProcessProbe(), db);

        for (var t = 1000L; t <= 1010L; t++) recorder.Tick(t);

        var bucket = new BucketStore(db).Read(0, 9999)[0];
        Assert.NotNull(bucket.FgAppId);
        Assert.Equal(@"D:\a.exe", new AppStore(db).GetPath(bucket.FgAppId!.Value));
    }

    [Fact]
    public void OpensAndClosesRunsAsProcessesComeAndGo()
    {
        using var db = new Db(_path);
        var processes = new FakeProcessProbe { Paths = { @"D:\game.exe" } };
        var recorder = new RecorderService(new FakeSystemProbe(), processes, db);

        recorder.Tick(1000);
        processes.Paths.Clear();
        recorder.Tick(1010);

        var runs = new AppStore(db).ReadRuns(0, 9999);
        Assert.Single(runs);
        Assert.Equal(1000, runs[0].Started);
        Assert.Equal(1010, runs[0].Ended);
    }

    [Fact]
    public void ScansProcessesOnlyOnBucketBoundaries()
    {
        using var db = new Db(_path);
        var processes = new FakeProcessProbe { Paths = { @"D:\game.exe" } };
        var recorder = new RecorderService(new FakeSystemProbe(), processes, db);

        recorder.Tick(1000);
        processes.Paths.Add(@"D:\other.exe");
        recorder.Tick(1005);

        Assert.Single(new AppStore(db).ReadRuns(0, 9999));
    }

    [Fact]
    public void StopFlushesPartialBucketAndClosesOpenRuns()
    {
        using var db = new Db(_path);
        var system = new FakeSystemProbe { Next = new SystemSnapshot(true, false, null) };
        var processes = new FakeProcessProbe { Paths = { @"D:\game.exe" } };
        var recorder = new RecorderService(system, processes, db);

        recorder.Tick(1000);
        recorder.Tick(1001);
        recorder.Stop(1002);

        Assert.Equal(2, new BucketStore(db).Read(0, 9999)[0].Active);
        Assert.Equal(1002, new AppStore(db).ReadRuns(0, 9999)[0].Ended);
    }

    [Fact]
    public void KeepsRecordingWhenTheProcessProbeThrows()
    {
        using var db = new Db(_path);
        var system = new FakeSystemProbe { Next = new SystemSnapshot(true, false, null) };
        var processes = new FakeProcessProbe { Throw = true };
        var recorder = new RecorderService(system, processes, db);

        for (var t = 1000L; t <= 1010L; t++) recorder.Tick(t);

        Assert.Single(new BucketStore(db).Read(0, 9999));
    }

    [Fact]
    public void ClosesRunsLeftOpenByAKilledSessionAtTheLastRecordedBucket()
    {
        using var db = new Db(_path);
        var buckets = new BucketStore(db);
        var apps = new AppStore(db);
        buckets.Write(new Bucket(1000, 5, 0, null));
        buckets.Write(new Bucket(1010, 5, 0, null));
        apps.OpenRun(apps.GetOrCreateAppId(@"D:\Games\a.exe"), 1000);

        // A new session starts long after the machine went down without Stop().
        _ = new RecorderService(new FakeSystemProbe(), new FakeProcessProbe(), db);

        Assert.Equal(1020, apps.ReadRuns(0, 99999)[0].Ended);
    }

    [Fact]
    public void RecoveryDoesNothingWhenThereAreNoBucketsToAnchorTo()
    {
        using var db = new Db(_path);
        var apps = new AppStore(db);
        apps.OpenRun(apps.GetOrCreateAppId(@"D:\Games\a.exe"), 1000);

        _ = new RecorderService(new FakeSystemProbe(), new FakeProcessProbe(), db);

        Assert.Null(apps.ReadRuns(0, 99999)[0].Ended);
    }
}
