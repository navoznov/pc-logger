namespace PcLogger.Core.Sampling;

/// <summary>Enumerates full paths of processes that own a visible window.</summary>
public interface IProcessProbe
{
    IReadOnlyList<string> WindowedProcessPaths();
}
