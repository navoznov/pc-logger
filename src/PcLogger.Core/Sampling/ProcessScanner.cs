namespace PcLogger.Core.Sampling;

/// <summary>Processes that appeared and disappeared since the previous scan.</summary>
public readonly record struct ScanDelta(IReadOnlyList<string> Started, IReadOnlyList<string> Ended);

/// <summary>
/// Turns successive process listings into open/close events, so the database holds
/// tens of interval rows per day instead of a flag on every bucket.
/// </summary>
public sealed class ProcessScanner
{
    private HashSet<string> _previous = new(StringComparer.OrdinalIgnoreCase);

    public ScanDelta Scan(IReadOnlyList<string> currentPaths)
    {
        var current = new HashSet<string>(currentPaths, StringComparer.OrdinalIgnoreCase);

        var started = current.Except(_previous, StringComparer.OrdinalIgnoreCase).ToArray();
        var ended = _previous.Except(current, StringComparer.OrdinalIgnoreCase).ToArray();

        _previous = current;
        return new ScanDelta(started, ended);
    }
}
