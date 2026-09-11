using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PcLogger.App.Win32;

/// <summary>
/// Registers a logon task via schtasks.exe. A scheduled task is used instead of the
/// Run registry key because it restarts the collector if it crashes.
/// </summary>
public static class ScheduledTaskInstaller
{
    private const string TaskName = "PcLogger";
    private const int AttachParentProcess = -1;

    public static int Install(string exePath) =>
        Run($"/Create /F /SC ONLOGON /TN {TaskName} /TR \"\\\"{exePath}\\\"\" /RL LIMITED");

    public static int Uninstall() => Run($"/Delete /F /TN {TaskName}");

    private static int Run(string arguments)
    {
        // This is a WinExe, so it starts with no console of its own. Without attaching to
        // the one it was launched from, `PcLogger.exe --install` would print nothing at
        // all and read as a silent failure.
        AttachConsole(AttachParentProcess);

        using var process = Process.Start(new ProcessStartInfo("schtasks.exe", arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        })!;

        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        Console.WriteLine(output.Trim());
        return process.ExitCode;
    }

    [DllImport("kernel32.dll")] private static extern bool AttachConsole(int processId);
}
