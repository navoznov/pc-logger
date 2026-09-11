namespace PcLogger.Core.Model;

/// <summary>
/// A moment in the recorder's own life, as opposed to the child's. Without these rows an
/// absent bucket means "the PC was not running" and nothing else, so a recorder that died
/// renders as an unbroken rest period — the report would claim perfect compliance exactly
/// when it stopped being able to observe anything.
/// </summary>
/// <param name="Kind">One of <see cref="RecorderEventKind"/>.</param>
/// <param name="Detail">Free text for <c>error</c>; null otherwise.</param>
public readonly record struct RecorderEvent(long Ts, string Kind, string? Detail);

public static class RecorderEventKind
{
    public const string Start = "start";
    public const string Stop = "stop";
    public const string Error = "error";
}
