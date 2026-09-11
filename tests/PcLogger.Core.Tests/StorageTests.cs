using PcLogger.Core.Model;
using PcLogger.Core.Storage;
using Xunit;

namespace PcLogger.Core.Tests;

public class BucketStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"pclog-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
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
