using PcLogger.Core.Model;

namespace PcLogger.Core.Sampling;

/// <summary>
/// Folds one-second snapshots into fixed 10-second buckets. Emits a bucket when
/// the clock crosses a boundary, so missed seconds (sleep, scheduler hiccups)
/// simply produce a bucket with fewer counted seconds.
/// </summary>
public sealed class Sampler
{
    private readonly int _bucketSeconds;
    private readonly Dictionary<string, (int Count, int LastSeen)> _foreground = new(StringComparer.OrdinalIgnoreCase);

    private long _currentTs = -1;
    private int _active;
    private int _pad;
    private int _tickIndex;

    public Sampler(int bucketSeconds = 10) => _bucketSeconds = bucketSeconds;

    public RawBucket? Tick(long unixSeconds, SystemSnapshot snapshot)
    {
        var bucketTs = unixSeconds - unixSeconds % _bucketSeconds;

        RawBucket? completed = null;
        if (_currentTs < 0)
        {
            _currentTs = bucketTs;
        }
        else if (bucketTs != _currentTs)
        {
            completed = Build();
            Reset(bucketTs);
        }

        if (snapshot.Input) _active++;
        if (snapshot.Gamepad) _pad++;
        if (!string.IsNullOrEmpty(snapshot.ForegroundPath))
        {
            var seen = _foreground.GetValueOrDefault(snapshot.ForegroundPath);
            _foreground[snapshot.ForegroundPath] = (seen.Count + 1, _tickIndex);
        }
        _tickIndex++;

        return completed;
    }

    /// <summary>Emits the partially filled bucket, if any. Used on shutdown.</summary>
    public RawBucket? Flush()
    {
        if (_currentTs < 0) return null;
        var bucket = Build();
        Reset(-1);
        return bucket;
    }

    private RawBucket Build()
    {
        string? dominant = null;
        var best = (Count: 0, LastSeen: -1);
        foreach (var (path, seen) in _foreground)
        {
            if (seen.Count > best.Count || (seen.Count == best.Count && seen.LastSeen > best.LastSeen))
            {
                best = seen;
                dominant = path;
            }
        }
        return new RawBucket(_currentTs, _active, _pad, dominant);
    }

    private void Reset(long newTs)
    {
        _currentTs = newTs;
        _active = 0;
        _pad = 0;
        _tickIndex = 0;
        _foreground.Clear();
    }
}
