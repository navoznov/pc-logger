using PcLogger.Core.Config;
using PcLogger.Core.Model;
using PcLogger.Core.Reporting;
using Xunit;

namespace PcLogger.Core.Tests;

public class PresenceSpanTests
{
    [Fact]
    public void EmptyDatabaseProducesOneOffSpanCoveringTheWindow()
    {
        var spans = SpanBuilder.BuildPresence(Array.Empty<Bucket>(), 1000, 1060);

        Assert.Single(spans);
        Assert.Equal(new PresenceSpan(1000, 60, "off"), spans[0]);
    }

    [Fact]
    public void ConsecutiveActiveBucketsCollapseIntoOneSpan()
    {
        var buckets = new[]
        {
            new Bucket(1000, 5, 0, null),
            new Bucket(1010, 7, 0, null),
            new Bucket(1020, 1, 0, null)
        };

        var spans = SpanBuilder.BuildPresence(buckets, 1000, 1030);

        Assert.Single(spans);
        Assert.Equal(new PresenceSpan(1000, 30, "active"), spans[0]);
    }

    [Fact]
    public void ZeroActivityBucketBecomesAway()
    {
        var buckets = new[] { new Bucket(1000, 0, 0, null) };

        var spans = SpanBuilder.BuildPresence(buckets, 1000, 1010);

        Assert.Equal("away", spans[0].S);
    }

    [Fact]
    public void MissingBucketBecomesOff()
    {
        var buckets = new[] { new Bucket(1000, 5, 0, null), new Bucket(1020, 5, 0, null) };

        var spans = SpanBuilder.BuildPresence(buckets, 1000, 1030);

        Assert.Equal(3, spans.Count);
        Assert.Equal(new PresenceSpan(1000, 10, "active"), spans[0]);
        Assert.Equal(new PresenceSpan(1010, 10, "off"), spans[1]);
        Assert.Equal(new PresenceSpan(1020, 10, "active"), spans[2]);
    }

    [Fact]
    public void GamepadOnlyActivityCountsAsActive()
    {
        var buckets = new[] { new Bucket(1000, 0, 6, null) };

        var spans = SpanBuilder.BuildPresence(buckets, 1000, 1010);

        Assert.Equal("active", spans[0].S);
    }

    [Fact]
    public void SpansCoverTheWindowWithoutGapsOrOverlap()
    {
        var buckets = new[] { new Bucket(1010, 5, 0, null), new Bucket(1030, 0, 0, null) };

        var spans = SpanBuilder.BuildPresence(buckets, 1000, 1050);

        Assert.Equal(1000, spans[0].T);
        Assert.Equal(1050, spans[^1].T + spans[^1].D);
        for (var i = 1; i < spans.Count; i++)
            Assert.Equal(spans[i - 1].T + spans[i - 1].D, spans[i].T);
    }
}

public class AppSpanTests
{
    private static readonly Dictionary<long, string> Paths = new()
    {
        [1] = @"D:\Games\deeprock\game.exe",
        [2] = @"C:\Program Files\Chrome\chrome.exe"
    };

    [Fact]
    public void ReportsFileNameRatherThanFullPath()
    {
        var buckets = new[] { new Bucket(1000, 5, 0, 1) };

        var spans = SpanBuilder.BuildAppSpans(buckets, Paths);

        Assert.Equal("game.exe", spans[0].App);
    }

    [Fact]
    public void CollapsesConsecutiveBucketsOfTheSameApp()
    {
        var buckets = new[]
        {
            new Bucket(1000, 5, 0, 1),
            new Bucket(1010, 5, 0, 1)
        };

        var spans = SpanBuilder.BuildAppSpans(buckets, Paths);

        Assert.Single(spans);
        Assert.Equal(new AppSpan(1000, 20, "game.exe"), spans[0]);
    }

    [Fact]
    public void SplitsWhenTheAppChanges()
    {
        var buckets = new[]
        {
            new Bucket(1000, 5, 0, 1),
            new Bucket(1010, 5, 0, 2)
        };

        var spans = SpanBuilder.BuildAppSpans(buckets, Paths);

        Assert.Equal(2, spans.Count);
        Assert.Equal("chrome.exe", spans[1].App);
    }

    [Fact]
    public void SplitsAcrossAGapInTime()
    {
        var buckets = new[]
        {
            new Bucket(1000, 5, 0, 1),
            new Bucket(1030, 5, 0, 1)
        };

        var spans = SpanBuilder.BuildAppSpans(buckets, Paths);

        Assert.Equal(2, spans.Count);
    }

    [Fact]
    public void IgnoresInactiveBuckets()
    {
        var buckets = new[] { new Bucket(1000, 0, 0, 1) };

        Assert.Empty(SpanBuilder.BuildAppSpans(buckets, Paths));
    }

    [Fact]
    public void IgnoresBucketsWithUnknownApp()
    {
        var buckets = new[] { new Bucket(1000, 5, 0, null), new Bucket(1010, 5, 0, 777) };

        Assert.Empty(SpanBuilder.BuildAppSpans(buckets, Paths));
    }
}

public class IntensityTests
{
    [Fact]
    public void MarksMinutesWithoutBucketsAsNull()
    {
        var intensity = SpanBuilder.BuildIntensity(Array.Empty<Bucket>(), 600, 720);

        Assert.Equal(600, intensity.T0);
        Assert.Equal(60, intensity.Step);
        Assert.Equal(new int?[] { null, null }, intensity.V);
    }

    [Fact]
    public void FullyActiveMinuteIsHundredPercent()
    {
        var buckets = Enumerable.Range(0, 6)
            .Select(i => new Bucket(600 + i * 10, 10, 0, null))
            .ToArray();

        var intensity = SpanBuilder.BuildIntensity(buckets, 600, 660);

        Assert.Equal(new int?[] { 100 }, intensity.V);
    }

    [Fact]
    public void HalfActiveMinuteIsFiftyPercent()
    {
        var buckets = Enumerable.Range(0, 6)
            .Select(i => new Bucket(600 + i * 10, 5, 0, null))
            .ToArray();

        var intensity = SpanBuilder.BuildIntensity(buckets, 600, 660);

        Assert.Equal(new int?[] { 50 }, intensity.V);
    }

    [Fact]
    public void GamepadSecondsCountTowardIntensity()
    {
        var buckets = Enumerable.Range(0, 6)
            .Select(i => new Bucket(600 + i * 10, 0, 10, null))
            .ToArray();

        var intensity = SpanBuilder.BuildIntensity(buckets, 600, 660);

        Assert.Equal(new int?[] { 100 }, intensity.V);
    }

    [Fact]
    public void OverlappingKeyboardAndGamepadDoNotExceedHundred()
    {
        var buckets = Enumerable.Range(0, 6)
            .Select(i => new Bucket(600 + i * 10, 10, 10, null))
            .ToArray();

        var intensity = SpanBuilder.BuildIntensity(buckets, 600, 660);

        Assert.Equal(new int?[] { 100 }, intensity.V);
    }

    [Fact]
    public void PartiallyCoveredMinuteIsScaledAgainstFullMinute()
    {
        var buckets = new[] { new Bucket(600, 10, 0, null), new Bucket(610, 10, 0, null) };

        var intensity = SpanBuilder.BuildIntensity(buckets, 600, 660);

        Assert.Equal(new int?[] { 33 }, intensity.V);
    }

    [Fact]
    public void BucketBeforeTheWindowDoesNotLeakIntoTheFirstMinute()
    {
        var buckets = Enumerable.Range(0, 6)
            .Select(i => new Bucket(3600 + i * 10, 10, 0, null))
            .Append(new Bucket(3590, 10, 0, null))
            .ToArray();

        var intensity = SpanBuilder.BuildIntensity(buckets, 3600, 3660);

        Assert.Equal(new int?[] { 100 }, intensity.V);
    }

    [Fact]
    public void MinuteStaysNullWhenOnlyTouchedByABucketBeforeTheWindow()
    {
        var buckets = new[] { new Bucket(3590, 10, 0, null) };

        var intensity = SpanBuilder.BuildIntensity(buckets, 3600, 3660);

        Assert.Equal(new int?[] { null }, intensity.V);
    }
}

public class GameSpanTests
{
    private static readonly Dictionary<long, string> Paths = new()
    {
        [1] = @"D:\Games\deeprock\game.exe",
        [2] = @"C:\Program Files\Chrome\chrome.exe",
        [3] = @"D:\Games\terraria\terraria.exe"
    };

    private static readonly GameFolders Folders = new(new[] { @"D:\Games" });

    [Fact]
    public void RunningButUnfocusedGameIsBackground()
    {
        var runs = new[] { new AppRun(1, 1000, 1020) };
        var buckets = new[] { new Bucket(1000, 5, 0, 2), new Bucket(1010, 5, 0, 2) };

        var spans = SpanBuilder.BuildGameSpans(runs, buckets, Paths, Folders, 1000, 1020);

        Assert.Single(spans);
        Assert.Equal(new GameSpan(1000, 20, "bg", "game.exe"), spans[0]);
    }

    [Fact]
    public void FocusedGameIsForeground()
    {
        var runs = new[] { new AppRun(1, 1000, 1020) };
        var buckets = new[] { new Bucket(1000, 5, 0, 1), new Bucket(1010, 5, 0, 1) };

        var spans = SpanBuilder.BuildGameSpans(runs, buckets, Paths, Folders, 1000, 1020);

        Assert.Single(spans);
        Assert.Equal("fg", spans[0].Lvl);
    }

    [Fact]
    public void SplitsWhenFocusEntersAndLeavesTheGame()
    {
        var runs = new[] { new AppRun(1, 1000, 1030) };
        var buckets = new[]
        {
            new Bucket(1000, 5, 0, 2),
            new Bucket(1010, 5, 0, 1),
            new Bucket(1020, 5, 0, 2)
        };

        var spans = SpanBuilder.BuildGameSpans(runs, buckets, Paths, Folders, 1000, 1030);

        Assert.Equal(new[] { "bg", "fg", "bg" }, spans.Select(s => s.Lvl));
    }

    [Fact]
    public void ProducesNothingWhenNoGameIsRunning()
    {
        var runs = new[] { new AppRun(2, 1000, 1020) };
        var buckets = new[] { new Bucket(1000, 5, 0, 2) };

        Assert.Empty(SpanBuilder.BuildGameSpans(runs, buckets, Paths, Folders, 1000, 1020));
    }

    [Fact]
    public void IgnoresRunsWhosePathIsUnknown()
    {
        var runs = new[] { new AppRun(999, 1000, 1020) };
        var buckets = new[] { new Bucket(1000, 5, 0, null) };

        Assert.Empty(SpanBuilder.BuildGameSpans(runs, buckets, Paths, Folders, 1000, 1020));
    }

    [Fact]
    public void KeepsTheTrackContinuousAcrossOverlappingGames()
    {
        var runs = new[] { new AppRun(1, 1000, 1020), new AppRun(3, 1010, 1030) };
        var buckets = new[]
        {
            new Bucket(1000, 5, 0, 2),
            new Bucket(1010, 5, 0, 2),
            new Bucket(1020, 5, 0, 2)
        };

        var spans = SpanBuilder.BuildGameSpans(runs, buckets, Paths, Folders, 1000, 1030);

        // The label changes when one game hands over to the other, so this is two spans
        // rather than one. What matters is that they abut: the strip must draw an
        // unbroken game bar across the handover, with no slot left uncovered.
        Assert.Equal(new[] { "game.exe", "terraria.exe" }, spans.Select(s => s.App));
        Assert.All(spans, s => Assert.Equal("bg", s.Lvl));
        Assert.Equal(1000, spans[0].T);
        Assert.Equal(30, spans.Sum(s => s.D));
        for (var i = 1; i < spans.Count; i++)
            Assert.Equal(spans[i - 1].T + spans[i - 1].D, spans[i].T);
    }

    // Every other game-span test ends its window exactly at the run's Ended, so the Started
    // boundary was never walked past in either direction: widening it by one bucket left the
    // whole suite green while every game was drawn ten seconds early.
    [Fact]
    public void DrawsNoGameBeforeItsRunStarted()
    {
        var runs = new[] { new AppRun(1, 1020, 1040) };
        var buckets = new[] { new Bucket(1000, 5, 0, null), new Bucket(1010, 5, 0, null),
                              new Bucket(1020, 5, 0, null), new Bucket(1030, 5, 0, null) };

        var spans = SpanBuilder.BuildGameSpans(runs, buckets, Paths, Folders, 1000, 1040);

        Assert.Equal(1020, spans.Single().T);
        Assert.Equal(20, spans.Single().D);
    }

    // A slot with no bucket row means the recorder was not running, so there is no evidence
    // about anything — including whether the game was up. Claiming otherwise would make the
    // two fact tracks of one payload contradict each other at the same instant: the presence
    // track says "PC off" while the game track draws a solid bar underneath it.
    [Fact]
    public void DrawsNoGameForSlotsThatHaveNoBucket()
    {
        var runs = new[] { new AppRun(1, 1000, 1030) };

        var spans = SpanBuilder.BuildGameSpans(runs, Array.Empty<Bucket>(), Paths, Folders, 1000, 1030);

        Assert.Empty(spans);
    }

    [Fact]
    public void StopsDrawingAnOpenRunOnceTheBucketsStop()
    {
        // The ordinary case: the PC is slept with a game open. The recorder survives sleep, so
        // no Stop() runs and the row stays open, but no buckets are written for the night.
        var runs = new[] { new AppRun(1, 1000, null) };
        var buckets = new[] { new Bucket(1000, 5, 0, null), new Bucket(1010, 5, 0, null) };

        var spans = SpanBuilder.BuildGameSpans(runs, buckets, Paths, Folders, 1000, 3600);

        Assert.Equal(1000, spans.Single().T);
        Assert.Equal(20, spans.Single().D);
    }

    [Fact]
    public void TreatsAnOpenEndedRunAsStillRunningWhileBucketsKeepArriving()
    {
        var runs = new[] { new AppRun(1, 1000, null) };
        var buckets = Enumerable.Range(0, 3).Select(i => new Bucket(1000 + i * 10, 5, 0, null)).ToArray();

        var spans = SpanBuilder.BuildGameSpans(runs, buckets, Paths, Folders, 1000, 1030);

        Assert.Equal(30, spans[0].D);
    }

    [Fact]
    public void SplitsAGameSpanAroundAGapWithNoBuckets()
    {
        var runs = new[] { new AppRun(1, 1000, 3630) };
        var buckets = new[] { new Bucket(1000, 5, 0, null), new Bucket(1010, 5, 0, null),
                              new Bucket(3600, 5, 0, null), new Bucket(3610, 5, 0, null) };

        var spans = SpanBuilder.BuildGameSpans(runs, buckets, Paths, Folders, 1000, 3630);

        Assert.Equal(2, spans.Count);
        Assert.Equal((1000, 20), (spans[0].T, spans[0].D));
        Assert.Equal((3600, 20), (spans[1].T, spans[1].D));
    }

    [Fact]
    public void GameTrackDoesNotAffectPresenceSpans()
    {
        var buckets = new[] { new Bucket(1000, 5, 0, 1), new Bucket(1010, 5, 0, 2) };

        var withGame = SpanBuilder.BuildPresence(buckets, 1000, 1020);
        var withoutGame = SpanBuilder.BuildPresence(
            buckets.Select(b => b with { FgAppId = null }).ToArray(), 1000, 1020);

        Assert.Equal(withGame, withoutGame);
    }
}
