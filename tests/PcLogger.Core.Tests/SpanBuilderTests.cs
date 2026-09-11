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
}
