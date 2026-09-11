namespace PcLogger.Core.Sampling;

/// <summary>One instantaneous reading of the system, taken once per second.</summary>
/// <param name="Input">Keyboard or mouse activity since the previous reading.</param>
/// <param name="Gamepad">Gamepad activity since the previous reading.</param>
/// <param name="ForegroundPath">Full path of the foreground process, or null if unknown.</param>
public readonly record struct SystemSnapshot(bool Input, bool Gamepad, string? ForegroundPath);

/// <summary>The only seam between the sampler and the operating system.</summary>
public interface ISystemProbe
{
    SystemSnapshot Sample();
}
