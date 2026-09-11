using PcLogger.Core.Model;
using PcLogger.Core.Sampling;
using PcLogger.Core.Storage;

namespace PcLogger.Core.Recording;

/// <summary>
/// Drives the 1 Hz presence sampler and the 10-second process scan, and persists what they
/// produce. Lives in Core rather than the Windows project because it needs no Windows API:
/// both probes are interfaces, so the whole wiring is covered by tests on any platform.
/// </summary>
public sealed class RecorderService
{
    private readonly ISystemProbe _system;
    private readonly IProcessProbe _processes;
    private readonly BucketStore _buckets;
    private readonly AppStore _apps;
    private readonly Sampler _sampler;
    private readonly ProcessScanner _scanner = new();
    private readonly int _bucketSeconds;

    public RecorderService(ISystemProbe system, IProcessProbe processes, Db db, int bucketSeconds = 10)
    {
        _system = system;
        _processes = processes;
        _buckets = new BucketStore(db);
        _apps = new AppStore(db);
        _sampler = new Sampler(bucketSeconds);
        _bucketSeconds = bucketSeconds;

        // A previous session may have died without Stop(): power cut, sleep, task manager.
        // Whatever it left open must be closed at the last moment we know the machine was
        // recording, NOT at "now" — an open run covers every slot after its start, so
        // closing at "now" would draw a game as running straight through the outage, and
        // leaving it open would draw it as running forever.
        if (_buckets.LastTs() is { } lastTs) _apps.CloseDangling(lastTs + bucketSeconds);
    }

    public void Tick(long unixSeconds)
    {
        var completed = _sampler.Tick(unixSeconds, _system.Sample());
        if (completed is { } bucket) Persist(bucket);

        if (unixSeconds % _bucketSeconds == 0) ScanProcesses(unixSeconds);
    }

    public void Stop(long unixSeconds)
    {
        if (_sampler.Flush() is { } bucket) Persist(bucket);
        _apps.CloseDangling(unixSeconds);
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
}
