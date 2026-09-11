using PcLogger.Core.Sampling;
using Xunit;

namespace PcLogger.Core.Tests;

public class SamplerTests
{
    private static SystemSnapshot Idle => new(false, false, null);
    private static SystemSnapshot Typing(string? app = null) => new(true, false, app);
    private static SystemSnapshot Pad(string? app = null) => new(false, true, app);

    [Fact]
    public void EmitsNothingBeforeBucketBoundary()
    {
        var sampler = new Sampler();

        for (var t = 1000L; t < 1010L; t++)
            Assert.Null(sampler.Tick(t, Typing()));
    }

    [Fact]
    public void EmitsCompletedBucketOnBoundary()
    {
        var sampler = new Sampler();
        for (var t = 1000L; t < 1010L; t++) sampler.Tick(t, Typing());

        var bucket = sampler.Tick(1010, Idle);

        Assert.NotNull(bucket);
        Assert.Equal(1000, bucket!.Value.Ts);
        Assert.Equal(10, bucket.Value.Active);
        Assert.Equal(0, bucket.Value.Pad);
    }

    [Fact]
    public void CountsGamepadSecondsSeparately()
    {
        var sampler = new Sampler();
        for (var t = 1000L; t < 1004L; t++) sampler.Tick(t, Pad());
        for (var t = 1004L; t < 1010L; t++) sampler.Tick(t, Idle);

        var bucket = sampler.Tick(1010, Idle)!.Value;

        Assert.Equal(0, bucket.Active);
        Assert.Equal(4, bucket.Pad);
    }

    [Fact]
    public void PicksForegroundAppThatHeldFocusLongest()
    {
        var sampler = new Sampler();
        for (var t = 1000L; t < 1003L; t++) sampler.Tick(t, Typing(@"D:\a.exe"));
        for (var t = 1003L; t < 1010L; t++) sampler.Tick(t, Typing(@"D:\b.exe"));

        Assert.Equal(@"D:\b.exe", sampler.Tick(1010, Idle)!.Value.FgPath);
    }

    [Fact]
    public void BreaksForegroundTieTowardTheMostRecentApp()
    {
        var sampler = new Sampler();
        for (var t = 1000L; t < 1005L; t++) sampler.Tick(t, Typing(@"D:\a.exe"));
        for (var t = 1005L; t < 1010L; t++) sampler.Tick(t, Typing(@"D:\b.exe"));

        Assert.Equal(@"D:\b.exe", sampler.Tick(1010, Idle)!.Value.FgPath);
    }

    [Fact]
    public void LeavesForegroundNullWhenNeverResolved()
    {
        var sampler = new Sampler();
        for (var t = 1000L; t < 1010L; t++) sampler.Tick(t, Typing());

        Assert.Null(sampler.Tick(1010, Idle)!.Value.FgPath);
    }

    [Fact]
    public void EmitsShortBucketWhenSecondsAreMissed()
    {
        var sampler = new Sampler();
        sampler.Tick(1000, Typing());
        sampler.Tick(1002, Typing());

        var bucket = sampler.Tick(1010, Idle)!.Value;

        Assert.Equal(1000, bucket.Ts);
        Assert.Equal(2, bucket.Active);
    }

    [Fact]
    public void SkippingWholeBucketsEmitsOnlyTheAccumulatedOne()
    {
        var sampler = new Sampler();
        sampler.Tick(1000, Typing());

        var bucket = sampler.Tick(1200, Typing());

        Assert.Equal(1000, bucket!.Value.Ts);
        Assert.Equal(1, bucket.Value.Active);
    }

    [Fact]
    public void FlushReturnsPartialBucketThenNothing()
    {
        var sampler = new Sampler();
        sampler.Tick(1000, Typing());
        sampler.Tick(1001, Typing());

        var flushed = sampler.Flush();

        Assert.Equal(1000, flushed!.Value.Ts);
        Assert.Equal(2, flushed.Value.Active);
        Assert.Null(sampler.Flush());
    }
}
