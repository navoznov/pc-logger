namespace PcLogger.Core.Model;

/// <summary>A stored 10-second bucket. Times are unix seconds.</summary>
public readonly record struct Bucket(long Ts, int Active, int Pad, long? FgAppId);

/// <summary>A bucket as produced by the sampler, before paths are resolved to ids.</summary>
public readonly record struct RawBucket(long Ts, int Active, int Pad, string? FgPath);

/// <summary>Lifetime of a process that owned a visible window. Ended is null while alive.</summary>
public readonly record struct AppRun(long AppId, long Started, long? Ended);

/// <summary>Presence interval. S is "active", "away" or "off".</summary>
public readonly record struct PresenceSpan(long T, int D, string S);

/// <summary>Foreground application interval. App is a file name, not a path.</summary>
public readonly record struct AppSpan(long T, int D, string App);

/// <summary>Game interval. Lvl is "fg" (focused) or "bg" (running in background).</summary>
public readonly record struct GameSpan(long T, int D, string Lvl, string App);

/// <summary>Per-minute activity, 0..100 percent. Null means the PC was off.</summary>
public sealed record Intensity(long T0, int Step, int?[] V);
