using System.Text.Json;
using PcLogger.Core.Config;
using PcLogger.Core.Model;
using PcLogger.Core.Reporting;
using PcLogger.Core.Storage;
using Xunit;

namespace PcLogger.Core.Tests;

public class AppConfigTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"pclog-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    [Fact]
    public void ReadsConfiguredFolders()
    {
        File.WriteAllText(_path, """{"game_folders": ["D:\\Games", "E:\\Epic"]}""");

        var config = AppConfig.Load(_path);

        Assert.Equal(new[] { @"D:\Games", @"E:\Epic" }, config.GameFolders);
    }

    [Fact]
    public void CreatesEmptyConfigWhenFileIsMissing()
    {
        var config = AppConfig.Load(_path);

        Assert.Empty(config.GameFolders);
        Assert.True(File.Exists(_path));
    }

    [Fact]
    public void FallsBackToEmptyListOnMalformedJson()
    {
        File.WriteAllText(_path, "{ this is not json");

        var config = AppConfig.Load(_path);

        Assert.Empty(config.GameFolders);
    }

    [Fact]
    public void FallsBackToEmptyListWhenGameFoldersKeyIsAbsent()
    {
        File.WriteAllText(_path, "{}");

        var config = AppConfig.Load(_path);

        Assert.Empty(config.GameFolders);
    }

    [Fact]
    public void FallsBackToEmptyListWhenGameFoldersIsNull()
    {
        File.WriteAllText(_path, """{"game_folders": null}""");

        var config = AppConfig.Load(_path);

        Assert.Empty(config.GameFolders);
    }

    [Fact]
    public void FallsBackToEmptyListWhenConfigDirectoryCannotBeCreated()
    {
        // A file where a directory is expected makes Directory.CreateDirectory fail on both
        // macOS and Windows — a portable stand-in for an unwritable config location.
        var blocker = Path.Combine(Path.GetTempPath(), $"pclog-{Guid.NewGuid():N}");
        File.WriteAllText(blocker, "not a directory");
        try
        {
            var config = AppConfig.Load(Path.Combine(blocker, "config.json"));

            Assert.Empty(config.GameFolders);
        }
        finally
        {
            File.Delete(blocker);
        }
    }
}

public class RenderTests
{
    [Fact]
    public void SubstitutesTheDataPlaceholder()
    {
        var html = ReportBuilder.Render("<script>const DATA = /*__DATA__*/;</script>", "{\"a\":1}", _ => null);

        Assert.Contains("const DATA = {\"a\":1};", html);
    }

    [Fact]
    public void InlinesReferencedScripts()
    {
        var html = ReportBuilder.Render(
            """<script src="regime.js"></script>/*__DATA__*/""",
            "{}",
            name => name == "regime.js" ? "var x = 1;" : null);

        Assert.Contains("<script>var x = 1;</script>", html);
        Assert.DoesNotContain("src=", html);
    }

    [Fact]
    public void ThrowsWhenAReferencedScriptIsMissing()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ReportBuilder.Render("""<script src="missing.js"></script>""", "{}", _ => null));
    }
}

public class BuildJsonTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"pclog-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        // WAL mode leaves -wal and -shm sidecars next to the database.
        foreach (var f in new[] { _path, _path + "-wal", _path + "-shm" })
            if (File.Exists(f)) File.Delete(f);
    }

    private JsonElement Build(long nowTs, int historyDays = 90)
    {
        using var db = new Db(_path);
        var builder = new ReportBuilder(db, new AppConfig(new[] { @"D:\Games" }));
        return JsonDocument.Parse(builder.BuildJson(nowTs, historyDays)).RootElement;
    }

    [Fact]
    public void ProducesEveryTopLevelKeyTheDashboardReads()
    {
        var root = Build(86_400);

        foreach (var key in new[] { "generated", "tz_offset_minutes", "defaults", "spans", "app_spans", "game_spans", "intensity", "events" })
            Assert.True(root.TryGetProperty(key, out _), $"missing key: {key}");
    }

    // The browser reads this payload by field name and never validates it: a renamed field
    // does not fail, it silently blanks a track or shifts an axis. Nothing else in the suite
    // pins these names, so five separate one-token edits used to leave 100 % of tests green
    // while the dashboard rendered empty. Every name below is read by dashboard.template.html.
    [Fact]
    public void PinsEveryFieldNameAndShapeTheDashboardReadsInsideTheArrays()
    {
        using (var db = new Db(_path))
        {
            var buckets = new BucketStore(db);
            var apps = new AppStore(db);
            var gameId = apps.GetOrCreateAppId(@"D:\Games\x\game.exe");
            apps.OpenRun(gameId, 500_000);
            apps.CloseRun(gameId, 500_030);
            for (var i = 0; i < 3; i++) buckets.Write(new Bucket(500_000 + i * 10, 10, 0, gameId));
            new EventStore(db).Write(500_000, RecorderEventKind.Error, "disk full");
        }

        var root = Build(500_030);

        var span = root.GetProperty("spans").EnumerateArray().First();
        Assert.Equal(JsonValueKind.Number, span.GetProperty("t").ValueKind);
        Assert.Equal(JsonValueKind.Number, span.GetProperty("d").ValueKind);
        Assert.Contains(span.GetProperty("s").GetString(), new[] { "active", "away", "off" });

        var appSpan = root.GetProperty("app_spans").EnumerateArray().First();
        Assert.Equal(JsonValueKind.Number, appSpan.GetProperty("t").ValueKind);
        Assert.Equal(JsonValueKind.Number, appSpan.GetProperty("d").ValueKind);
        Assert.Equal("game.exe", appSpan.GetProperty("app").GetString());

        var gameSpan = root.GetProperty("game_spans").EnumerateArray().First();
        Assert.Equal(JsonValueKind.Number, gameSpan.GetProperty("t").ValueKind);
        Assert.Equal(JsonValueKind.Number, gameSpan.GetProperty("d").ValueKind);
        Assert.Contains(gameSpan.GetProperty("lvl").GetString(), new[] { "fg", "bg" });
        Assert.Equal("game.exe", gameSpan.GetProperty("app").GetString());

        var intensity = root.GetProperty("intensity");
        Assert.Equal(60, intensity.GetProperty("step").GetInt32());
        Assert.Equal(JsonValueKind.Array, intensity.GetProperty("v").ValueKind);
        // The dashboard derives an array index as (view.from - t0) / step and rounds it, so a
        // t0 that is not minute-aligned shifts the whole histogram by up to half a minute.
        Assert.Equal(0, intensity.GetProperty("t0").GetInt64() % 60);

        var evt = root.GetProperty("events").EnumerateArray().First();
        Assert.Equal(500_000, evt.GetProperty("t").GetInt64());
        Assert.Equal("error", evt.GetProperty("kind").GetString());
        Assert.Equal("disk full", evt.GetProperty("detail").GetString());
    }

    [Fact]
    public void ReportsTheLocalOffsetSoTheDashboardCanDrawLocalDays()
    {
        var expected = (int)TimeZoneInfo.Local
            .GetUtcOffset(DateTimeOffset.FromUnixTimeSeconds(500_030)).TotalMinutes;

        Assert.Equal(expected, Build(500_030).GetProperty("tz_offset_minutes").GetInt32());
    }

    [Fact]
    public void CarriesDefaultThresholdsFromTheSpec()
    {
        var defaults = Build(86_400).GetProperty("defaults");

        Assert.Equal(3, defaults.GetProperty("micro_gap").GetInt32());
        Assert.Equal(15, defaults.GetProperty("break_minutes").GetInt32());
        Assert.Equal(15, defaults.GetProperty("session_minutes").GetInt32());
        Assert.Equal(2, defaults.GetProperty("tolerance").GetInt32());
    }

    [Fact]
    public void IncludesRecentBucketsAndExcludesOlderOnes()
    {
        using (var db = new Db(_path))
        {
            var buckets = new BucketStore(db);
            var apps = new AppStore(db);
            var gameId = apps.GetOrCreateAppId(@"D:\Games\a.exe");
            buckets.Write(new Bucket(500_000, 10, 0, gameId));
            buckets.Write(new Bucket(100_000, 10, 0, gameId));
            apps.OpenRun(gameId, 500_000);
            apps.CloseRun(gameId, 500_010);
        }

        var root = Build(500_010, historyDays: 1);
        var appSpans = root.GetProperty("app_spans");

        Assert.Equal(1, appSpans.GetArrayLength());
        Assert.Equal("a.exe", appSpans[0].GetProperty("app").GetString());
    }

    [Fact]
    public void ReportsGameSpansForExecutablesInsideGameFolders()
    {
        using (var db = new Db(_path))
        {
            var apps = new AppStore(db);
            var id = apps.GetOrCreateAppId(@"D:\Games\a.exe");
            apps.OpenRun(id, 500_000);
            apps.CloseRun(id, 500_030);
            new BucketStore(db).Write(new Bucket(500_000, 10, 0, id));
        }

        var gameSpans = Build(500_030, historyDays: 1).GetProperty("game_spans");

        Assert.True(gameSpans.GetArrayLength() > 0);
    }

    [Fact]
    public void AlignsTheWindowToTheBucketGrid()
    {
        // nowTs is deliberately not a multiple of the 10-second bucket grid. If BuildJson
        // passed `from` through unaligned, BuildPresence's exact-timestamp lookup would
        // miss every stored bucket and the whole window would render as "off".
        const long nowTs = 1_000_007;

        using (var db = new Db(_path))
        {
            new BucketStore(db).Write(new Bucket(nowTs - nowTs % 10, 10, 0, null));
        }

        var spans = Build(nowTs, historyDays: 1).GetProperty("spans");

        var states = spans.EnumerateArray().Select(s => s.GetProperty("s").GetString());
        Assert.Contains("active", states);
    }

    [Fact]
    public void RoundsTheWindowToIncludeTheInProgressBucket()
    {
        // nowTs sits exactly on a bucket boundary, and a bucket is stored at that exact
        // timestamp — the bucket currently being filled when the report is generated.
        // BuildJson rounds `to` one bucket past nowTs specifically so this bucket, which
        // falls exactly on the otherwise-exclusive upper bound, is still walked. Without
        // the "+ BucketSeconds" the half-open [from, to) walk would stop short of it.
        const long nowTs = 1_000_000;

        using (var db = new Db(_path))
        {
            new BucketStore(db).Write(new Bucket(nowTs, 10, 0, null));
        }

        var spans = Build(nowTs, historyDays: 1).GetProperty("spans");

        var states = spans.EnumerateArray().Select(s => s.GetProperty("s").GetString());
        Assert.Contains("active", states);
    }
}
