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
