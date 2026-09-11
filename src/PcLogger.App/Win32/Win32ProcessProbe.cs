using System.Runtime.InteropServices;
using PcLogger.Core.Sampling;

namespace PcLogger.App.Win32;

/// <summary>
/// Lists processes that own a visible window. Enumerating windows rather than all
/// processes keeps the result in the tens rather than the hundreds.
/// </summary>
public sealed class Win32ProcessProbe : IProcessProbe
{
    public IReadOnlyList<string> WindowedProcessPaths()
    {
        var pids = new HashSet<uint>();

        EnumWindows((window, _) =>
        {
            if (IsWindowVisible(window))
            {
                GetWindowThreadProcessId(window, out var pid);
                if (pid != 0) pids.Add(pid);
            }
            return true;
        }, IntPtr.Zero);

        return pids
            .Select(Win32SystemProbe.ProcessPath)
            .Where(path => !string.IsNullOrEmpty(path))
            .Select(path => path!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr param);

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr param);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
}
