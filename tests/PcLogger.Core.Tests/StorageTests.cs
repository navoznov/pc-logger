using PcLogger.Core.Model;
using PcLogger.Core.Storage;
using Xunit;

namespace PcLogger.Core.Tests;

public class BucketStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"pclog-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        // WAL mode leaves -wal and -shm sidecars next to the database.
        foreach (var f in new[] { _path, _path + "-wal", _path + "-shm" })
            if (File.Exists(f)) File.Delete(f);
    }

    [Fact]
    public void WritesAndReadsBucketsInRange()
    {
        using var db = new Db(_path);
        var store = new BucketStore(db);

        store.Write(new Bucket(1000, 10, 0, null));
        store.Write(new Bucket(1010, 0, 4, 7));
        store.Write(new Bucket(1020, 3, 0, null));

        var read = store.Read(1000, 1015);

        Assert.Equal(2, read.Count);
        Assert.Equal(new Bucket(1000, 10, 0, null), read[0]);
        Assert.Equal(new Bucket(1010, 0, 4, 7), read[1]);
    }

    [Fact]
    public void ReturnsBucketsOrderedByTime()
    {
        using var db = new Db(_path);
        var store = new BucketStore(db);

        store.Write(new Bucket(3000, 1, 0, null));
        store.Write(new Bucket(1000, 1, 0, null));
        store.Write(new Bucket(2000, 1, 0, null));

        var read = store.Read(0, 9999);

        Assert.Equal(new long[] { 1000, 2000, 3000 }, read.Select(b => b.Ts));
    }

    [Fact]
    public void RewritingSameTimestampReplacesRowInsteadOfDuplicating()
    {
        using var db = new Db(_path);
        var store = new BucketStore(db);

        store.Write(new Bucket(1000, 2, 0, null));
        store.Write(new Bucket(1000, 9, 1, 5));

        var read = store.Read(0, 9999);

        Assert.Single(read);
        Assert.Equal(new Bucket(1000, 9, 1, 5), read[0]);
    }

    [Fact]
    public void SurvivesReopeningTheDatabase()
    {
        using (var db = new Db(_path))
        {
            new BucketStore(db).Write(new Bucket(1000, 5, 0, null));
        }

        using var reopened = new Db(_path);
        var read = new BucketStore(reopened).Read(0, 9999);

        Assert.Single(read);
    }
}

public class AppStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"pclog-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        // WAL mode leaves -wal and -shm sidecars next to the database.
        foreach (var f in new[] { _path, _path + "-wal", _path + "-shm" })
            if (File.Exists(f)) File.Delete(f);
    }

    [Fact]
    public void ReturnsSameIdForSamePath()
    {
        using var db = new Db(_path);
        var store = new AppStore(db);

        var first = store.GetOrCreateAppId(@"D:\Games\a.exe");
        var second = store.GetOrCreateAppId(@"D:\Games\a.exe");

        Assert.Equal(first, second);
    }

    [Fact]
    public void ReturnsDifferentIdsForDifferentPaths()
    {
        using var db = new Db(_path);
        var store = new AppStore(db);

        Assert.NotEqual(
            store.GetOrCreateAppId(@"D:\Games\a.exe"),
            store.GetOrCreateAppId(@"D:\Games\b.exe"));
    }

    [Fact]
    public void ResolvesIdBackToPath()
    {
        using var db = new Db(_path);
        var store = new AppStore(db);

        var id = store.GetOrCreateAppId(@"D:\Games\a.exe");

        Assert.Equal(@"D:\Games\a.exe", store.GetPath(id));
        Assert.Null(store.GetPath(9999));
    }

    [Fact]
    public void ClosesOpenRunAtGivenTime()
    {
        using var db = new Db(_path);
        var store = new AppStore(db);
        var id = store.GetOrCreateAppId(@"D:\Games\a.exe");

        store.OpenRun(id, 1000);
        store.CloseRun(id, 1600);

        var runs = store.ReadRuns(0, 9999);
        Assert.Single(runs);
        Assert.Equal(new AppRun(id, 1000, 1600), runs[0]);
    }

    [Fact]
    public void KeepsRunOpenUntilClosed()
    {
        using var db = new Db(_path);
        var store = new AppStore(db);
        var id = store.GetOrCreateAppId(@"D:\Games\a.exe");

        store.OpenRun(id, 1000);

        Assert.Null(store.ReadRuns(0, 9999)[0].Ended);
    }

    [Fact]
    public void SecondOpenRunWithoutCloseIsANoOp()
    {
        using var db = new Db(_path);
        var store = new AppStore(db);
        var id = store.GetOrCreateAppId(@"D:\Games\a.exe");

        store.OpenRun(id, 1000);
        store.OpenRun(id, 1200);

        var runs = store.ReadRuns(0, 9999);
        Assert.Single(runs);
        Assert.Equal(1000, runs[0].Started);
    }

    [Fact]
    public void CloseDanglingClosesEveryOpenRun()
    {
        using var db = new Db(_path);
        var store = new AppStore(db);
        var a = store.GetOrCreateAppId(@"D:\Games\a.exe");
        var b = store.GetOrCreateAppId(@"D:\Games\b.exe");

        store.OpenRun(a, 1000);
        store.OpenRun(b, 1100);
        store.CloseRun(a, 1200);
        store.CloseDangling(1300);

        var runs = store.ReadRuns(0, 9999);
        Assert.Equal(2, runs.Count);
        Assert.All(runs, r => Assert.NotNull(r.Ended));
        Assert.Equal(1200, runs.Single(r => r.AppId == a).Ended);
        Assert.Equal(1300, runs.Single(r => r.AppId == b).Ended);
    }

    [Fact]
    public void ReadRunsIncludesRunsOverlappingTheRange()
    {
        using var db = new Db(_path);
        var store = new AppStore(db);
        var id = store.GetOrCreateAppId(@"D:\Games\a.exe");

        store.OpenRun(id, 1000);
        store.CloseRun(id, 5000);

        Assert.Single(store.ReadRuns(2000, 3000));
        Assert.Empty(store.ReadRuns(6000, 7000));
    }

    [Fact]
    public void ReadRunsIncludesStillOpenRunThatStartedBeforeTheRange()
    {
        using var db = new Db(_path);
        var store = new AppStore(db);
        var id = store.GetOrCreateAppId(@"D:\Games\a.exe");

        store.OpenRun(id, 1000);

        Assert.Single(store.ReadRuns(2000, 3000));
    }

    [Fact]
    public void TreatsPathsDifferingOnlyInCaseAsTheSameApp()
    {
        using var db = new Db(_path);

        var first = new AppStore(db).GetOrCreateAppId(@"D:\Games\A.exe");
        var second = new AppStore(db).GetOrCreateAppId(@"d:\games\a.exe");

        Assert.Equal(first, second);
        Assert.Single(new AppStore(db).AllPaths());
    }

    // A run that ends before it starts covers no slot, so the reporting layer drops it
    // silently rather than drawing something odd. The corruption is invisible, which is
    // why the invariant is enforced in SQL at the only two places that write `ended`.
    [Fact]
    public void CloseRunNeverStampsAnEndBeforeTheStart()
    {
        using var db = new Db(_path);
        var store = new AppStore(db);
        var id = store.GetOrCreateAppId(@"D:\Games\a.exe");
        store.OpenRun(id, 1000);

        store.CloseRun(id, 900);

        Assert.Equal(1000, store.ReadRuns(0, 5000).Single().Ended);
    }

    [Fact]
    public void CloseDanglingNeverStampsAnEndBeforeTheStart()
    {
        using var db = new Db(_path);
        var store = new AppStore(db);
        var early = store.GetOrCreateAppId(@"D:\Games\a.exe");
        var late = store.GetOrCreateAppId(@"D:\Games\b.exe");
        store.OpenRun(early, 500);
        store.OpenRun(late, 2000);

        store.CloseDangling(900);

        var runs = store.ReadRuns(0, 5000).OrderBy(r => r.Started).ToList();
        Assert.Equal(900, runs[0].Ended);
        Assert.Equal(2000, runs[1].Ended);
    }
}
