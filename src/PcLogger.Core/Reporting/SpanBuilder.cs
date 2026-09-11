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

    public static IReadOnlyList<AppSpan> BuildAppSpans(
        IReadOnlyList<Bucket> buckets,
        IReadOnlyDictionary<long, string> paths,
        int bucketSeconds = 10)
    {
        var spans = new List<AppSpan>();

        string? runApp = null;
        var runStart = 0L;
        var runEnd = 0L;

        foreach (var bucket in buckets.OrderBy(b => b.Ts))
        {
            var app = ResolveName(bucket, paths);
            var contiguous = runApp is not null && bucket.Ts == runEnd && app == runApp;

            if (contiguous)
            {
                runEnd = bucket.Ts + bucketSeconds;
                continue;
            }

            if (runApp is not null)
                spans.Add(new AppSpan(runStart, (int)(runEnd - runStart), runApp));

            runApp = app;
            runStart = bucket.Ts;
            runEnd = bucket.Ts + bucketSeconds;
        }

        if (runApp is not null)
            spans.Add(new AppSpan(runStart, (int)(runEnd - runStart), runApp));

        return spans;
    }

    private static string? ResolveName(Bucket bucket, IReadOnlyDictionary<long, string> paths)
    {
        if (bucket.Active == 0 && bucket.Pad == 0) return null;
        if (bucket.FgAppId is null) return null;
        return paths.TryGetValue(bucket.FgAppId.Value, out var path) ? FileName(path) : null;
    }

    /// <summary>
    /// Last segment of a Windows path. Path.GetFileName cannot be used: it honours the
    /// running platform's separator, so on macOS it returns the whole "D:\Games\game.exe"
    /// unchanged. Stored paths are always Windows paths, wherever the report is built.
    /// </summary>
    private static string FileName(string path) => path.Split('\\', '/')[^1];

    public static Intensity BuildIntensity(
        IReadOnlyList<Bucket> buckets, long fromTs, long toTs, int bucketSeconds = 10)
    {
        const int Step = 60;
        var start = fromTs - fromTs % Step;
        var minutes = (int)Math.Ceiling((toTs - start) / (double)Step);
        var values = new int?[minutes];

        foreach (var bucket in buckets)
        {
            var index = (int)((bucket.Ts - start) / Step);
            if (index < 0 || index >= minutes) continue;

            // Keyboard and gamepad seconds may overlap within a bucket, so the
            // larger of the two is the only count that cannot exceed the bucket.
            var seconds = Math.Max(bucket.Active, bucket.Pad);
            values[index] = (values[index] ?? 0) + seconds;
        }

        for (var i = 0; i < minutes; i++)
            if (values[i] is { } seconds)
                values[i] = (int)Math.Round(100.0 * seconds / Step);

        return new Intensity(start, Step, values);
    }
}
