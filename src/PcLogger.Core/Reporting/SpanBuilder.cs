using PcLogger.Core.Model;

namespace PcLogger.Core.Reporting;

/// <summary>
/// Compresses buckets into intervals. This is compression only: no regime threshold
/// is applied here, because the verdict lives in the dashboard so that thresholds
/// stay adjustable over the whole history.
/// </summary>
public static class SpanBuilder
{
    public const string Active = "active";
    public const string Away = "away";
    public const string Off = "off";

    public static IReadOnlyList<PresenceSpan> BuildPresence(
        IReadOnlyList<Bucket> buckets, long fromTs, long toTs, int bucketSeconds = 10)
    {
        var byTs = buckets.ToDictionary(b => b.Ts);
        var spans = new List<PresenceSpan>();

        string? runState = null;
        var runStart = 0L;

        for (var ts = fromTs; ts < toTs; ts += bucketSeconds)
        {
            var state = Off;
            if (byTs.TryGetValue(ts, out var bucket))
                state = bucket.Active > 0 || bucket.Pad > 0 ? Active : Away;

            if (state != runState)
            {
                if (runState is not null)
                    spans.Add(new PresenceSpan(runStart, (int)(ts - runStart), runState));
                runState = state;
                runStart = ts;
            }
        }

        if (runState is not null)
            spans.Add(new PresenceSpan(runStart, (int)(toTs - runStart), runState));

        return spans;
    }
}
