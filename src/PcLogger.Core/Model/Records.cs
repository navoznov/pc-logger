namespace PcLogger.Core.Model;

/// <summary>A stored 10-second bucket. Times are unix seconds.</summary>
public readonly record struct Bucket(long Ts, int Active, int Pad, long? FgAppId);

/// <summary>A bucket as produced by the sampler, before paths are resolved to ids.</summary>
public readonly record struct RawBucket(long Ts, int Active, int Pad, string? FgPath);

/// <summary>Lifetime of a process that owned a visible window. Ended is null while alive.</summary>
public readonly record struct AppRun(long AppId, long Started, long? Ended);
