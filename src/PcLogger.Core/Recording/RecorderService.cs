using PcLogger.Core.Model;
using PcLogger.Core.Sampling;
using PcLogger.Core.Storage;

namespace PcLogger.Core.Recording;

/// <summary>
/// Drives the 1 Hz presence sampler and the 10-second process scan, and persists what they
/// produce. Lives in Core rather than the Windows project because it needs no Windows API:
/// both probes are interfaces, so the whole wiring is covered by tests on any platform.
///
/// Nothing in here is allowed to throw. An absent bucket row means "the PC was not running",
/// so a recorder that dies renders as an unbroken rest period — the report would claim perfect
/// compliance exactly when it stopped being able to observe anything. Failures are swallowed to
/// keep the loop alive and recorded as events so the report can say what happened instead.
/// </summary>
public sealed class RecorderService
{
    /// <summary>At most one error row per minute: a full disk fails every single second.</summary>
    private const int ErrorEventInterval = 60;

    private readonly ISystemProbe _system;
    private readonly IProcessProbe _processes;
    private readonly BucketStore _buckets;
    private readonly AppStore _apps;
    private readonly EventStore _events;
    private readonly Sampler _sampler;
    private readonly ProcessScanner _scanner = new();
    private readonly int _bucketSeconds;
    private readonly Action<Exception>? _onError;

    private bool _started;
    private long _lastErrorTs;

    public RecorderService(ISystemProbe system, IProcessProbe processes, Db db,
                           int bucketSeconds = 10, Action<Exception>? onError = null)
    {
        _system = system;
        _processes = processes;
        _buckets = new BucketStore(db);
        _apps = new AppStore(db);
        _events = new EventStore(db);
        _sampler = new Sampler(bucketSeconds);
        _bucketSeconds = bucketSeconds;
        _onError = onError;
    }

    public void Tick(long unixSeconds)
    {
        try
        {
            if (!_started) Begin(unixSeconds);

            var completed = _sampler.Tick(unixSeconds, _system.Sample());
            if (completed is { } bucket)
            {
                Persist(bucket);
                // Scan off the boundary the sampler already crossed, not off observing the exact
                // boundary second. A WinForms timer fires late and coalesces, so seconds go
                // missing; one missed second in ten was a whole lost scan, and a game that
                // started and ended inside that bucket was never recorded at all.
                ScanProcesses(unixSeconds);
            }
        }
        catch (Exception e)
        {
            RecordFailure(unixSeconds, e);
        }
    }

    public void Stop(long unixSeconds)
    {
        try
        {
            if (_sampler.Flush() is { } bucket) Persist(bucket);
            _apps.CloseDangling(unixSeconds);
            _events.Write(unixSeconds, RecorderEventKind.Stop);
        }
        catch (Exception e)
        {
            RecordFailure(unixSeconds, e);
        }
    }

    /// <summary>
    /// Opens a session: recovers from whatever the previous one left behind, records that
    /// recording started, and takes the first process scan. Runs on the first tick rather
    /// than in the constructor because recovery needs a current timestamp.
    /// </summary>
    private void Begin(long unixSeconds)
    {
        // A previous session may have died without Stop(): power cut, sleep, task manager.
        // Close what it left open at the last moment we know the machine was recording, NOT
        // at "now" — an open run covers every slot after its start, so closing at "now" would
        // draw a game as running straight through the outage. With no bucket evidence at all
        // the anchor is 0, and CloseDangling's MAX(ended, started) collapses each run to zero
        // length: we cannot claim it ran. Leaving it open instead would draw it as running for
        // the entire report window AND make every future OpenRun for that app a no-op.
        var anchor = _buckets.LastTs(unixSeconds) is { } lastTs ? lastTs + _bucketSeconds : 0;
        _apps.CloseDangling(anchor);
        _events.Write(unixSeconds, RecorderEventKind.Start);
        ScanProcesses(unixSeconds);
        _started = true;
    }

    private void Persist(RawBucket raw)
    {
        long? appId = raw.FgPath is null ? null : _apps.GetOrCreateAppId(raw.FgPath);
        _buckets.Write(new Bucket(raw.Ts, raw.Active, raw.Pad, appId));
    }

    private void ScanProcesses(long unixSeconds)
    {
        IReadOnlyList<string> paths;
        try
        {
            paths = _processes.WindowedProcessPaths();
        }
        catch (Exception)
        {
            // An unreadable process list (anti-cheat, permissions) must never stop
            // presence recording, which is what the regime verdict depends on.
            return;
        }

        var delta = _scanner.Scan(paths);
        foreach (var path in delta.Started) _apps.OpenRun(_apps.GetOrCreateAppId(path), unixSeconds);
        foreach (var path in delta.Ended) _apps.CloseRun(_apps.GetOrCreateAppId(path), unixSeconds);
    }

    private void RecordFailure(long unixSeconds, Exception e)
    {
        _onError?.Invoke(e);

        if (_lastErrorTs != 0 && unixSeconds - _lastErrorTs < ErrorEventInterval) return;
        _lastErrorTs = unixSeconds;

        try
        {
            _events.Write(unixSeconds, RecorderEventKind.Error, e.Message);
        }
        catch (Exception)
        {
            // The database is usually the thing that broke. The callback already fired, and
            // losing the marker must not take the recorder down with it.
        }
    }
}
