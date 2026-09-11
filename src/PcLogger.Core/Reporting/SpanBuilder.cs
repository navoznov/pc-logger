using PcLogger.Core.Config;
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
            // Case-insensitive because Windows paths are: two apps rows differing only in
            // case denote the same executable and must not split one run into two spans.
            var contiguous = runApp is not null && bucket.Ts == runEnd
                             && string.Equals(app, runApp, StringComparison.OrdinalIgnoreCase);

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
        IReadOnlyList<Bucket> buckets, long fromTs, long toTs)
    {
        const int Step = 60;
        var start = fromTs - fromTs % Step;
        var minutes = (int)Math.Ceiling((toTs - start) / (double)Step);
        var values = new int?[minutes];

        foreach (var bucket in buckets)
        {
            // Integer division truncates toward zero, so a negative offset would land on
            // index 0 instead of -1 and leak pre-window data into the first minute.
            var offset = bucket.Ts - start;
            if (offset < 0) continue;
            var index = (int)(offset / Step);
            if (index >= minutes) continue;

            // Keyboard and gamepad seconds may overlap within a bucket, so the
            // larger of the two is the only count that cannot exceed the bucket.
            var seconds = Math.Max(bucket.Active, bucket.Pad);
            values[index] = (values[index] ?? 0) + seconds;
        }

        // Clamped because this is the last place a bad row can be stopped from becoming a bad
        // pixel: a database written before the sampler capped its counters can still hold a
        // bucket with more counted seconds than the bucket is long, and the dashboard draws
        // the value straight into a rect height.
        for (var i = 0; i < minutes; i++)
            if (values[i] is { } seconds)
                values[i] = Math.Clamp((int)Math.Round(100.0 * seconds / Step), 0, 100);

        return new Intensity(start, Step, values);
    }

    public static IReadOnlyList<GameSpan> BuildGameSpans(
        IReadOnlyList<AppRun> runs,
        IReadOnlyList<Bucket> buckets,
        IReadOnlyDictionary<long, string> paths,
        GameFolders folders,
        long fromTs,
        long toTs,
        int bucketSeconds = 10)
    {
        var gameRuns = runs
            .Where(r => paths.TryGetValue(r.AppId, out var path) && folders.IsGame(path))
            .OrderBy(r => r.Started)
            .ToArray();

        if (gameRuns.Length == 0) return Array.Empty<GameSpan>();

        var byTs = buckets.ToDictionary(b => b.Ts);
        var spans = new List<GameSpan>();

        (string Lvl, string App)? run = null;
        var runStart = 0L;

        // Runs arrive sorted by Started, so the grid walk sweeps them with a moving window
        // instead of rescanning the whole array at every slot. Ninety days is 777 601 slots,
        // and the old rescan made this function 65 % of the whole report build — growing
        // linearly with every game the child ever launched.
        var active = new List<AppRun>();
        var next = 0;

        for (var ts = fromTs; ts < toTs; ts += bucketSeconds)
        {
            while (next < gameRuns.Length && gameRuns[next].Started <= ts)
            {
                active.Add(gameRuns[next]);
                next++;
            }
            for (var i = active.Count - 1; i >= 0; i--)
            {
                if (active[i].Ended is { } ended && ended <= ts) active.RemoveAt(i);
            }

            // active keeps the array's order, so this is the same run the old linear scan
            // would have stopped at: the earliest-started one still covering this slot.
            AppRun? covering = active.Count > 0 ? active[0] : null;

            // A slot with no bucket row means the recorder was not running there, so there is
            // no evidence about anything in it — including whether the game was up. Drawing a
            // game across such a slot makes the payload contradict itself: the presence track
            // says the PC was off while the game track draws a solid bar underneath it. The
            // ordinary way in is sleeping the PC with a game open, which closes no run.
            (string Lvl, string App)? slot = null;
            if (byTs.TryGetValue(ts, out var bucket) && covering is { } activeRun)
            {
                // The foreground app has to be one of the games actually running, not merely
                // an executable that sits in a game folder: without this the slot could be
                // labelled with a game for which no run exists at all.
                var fgRunning = false;
                if (bucket.FgAppId is { } id)
                {
                    for (var i = 0; i < active.Count; i++)
                    {
                        if (active[i].AppId != id) continue;
                        fgRunning = true;
                        break;
                    }
                }

                var focused = fgRunning
                    && bucket.FgAppId is { } fgId
                    && paths.TryGetValue(fgId, out var fgPath)
                    && folders.IsGame(fgPath)
                        ? FileName(fgPath)
                        : null;

                slot = focused is not null
                    ? ("fg", focused)
                    : ("bg", FileName(paths[activeRun.AppId]));
            }

            if (slot?.Lvl != run?.Lvl || slot?.App != run?.App)
            {
                if (run is not null)
                    spans.Add(new GameSpan(runStart, (int)(ts - runStart), run.Value.Lvl, run.Value.App));
                run = slot;
                runStart = ts;
            }
        }

        if (run is not null)
            spans.Add(new GameSpan(runStart, (int)(toTs - runStart), run.Value.Lvl, run.Value.App));

        return spans;
    }
}
