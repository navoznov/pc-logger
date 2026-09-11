using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace PcLogger.App.Win32;

/// <summary>
/// Registers a logon task via schtasks.exe.
///
/// The task is registered from an XML definition rather than from the positional switches,
/// because the defaults schtasks applies are actively hostile to a recorder that is supposed
/// to run whenever the machine is on: ExecutionTimeLimit defaults to 72 hours, after which
/// Task Scheduler terminates the process, and a logon trigger does not fire again on resume
/// from sleep — so on a machine that is slept rather than shut down, recording would stop for
/// as long as it takes the user to sign out again. DisallowStartIfOnBatteries and
/// StopIfGoingOnBatteries also default to true, which on a laptop means it never starts at
/// all. None of those can be set from the command line.
/// </summary>
public static class ScheduledTaskInstaller
{
    private const string TaskName = "PcLogger";
    private const int AttachParentProcess = -1;

    public static int Install(string exePath)
    {
        AttachConsole(AttachParentProcess);

        // Environment.ProcessPath is the HOST process: launched through `dotnet run`, it is
        // dotnet.exe, and the task would then start dotnet.exe with no arguments at every
        // logon, print a usage banner to a console nobody sees, exit 0, and record nothing
        // — while schtasks reports SUCCESS and the task history stays clean.
        if (!Path.GetFileName(exePath).Equals("PcLogger.exe", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(
                $"--install must be run from PcLogger.exe, not through the dotnet host (got {exePath}).");
            return 1;
        }

        var xmlPath = Path.Combine(Path.GetTempPath(), $"pclogger-task-{Guid.NewGuid():N}.xml");
        try
        {
            // schtasks requires UTF-16 for /XML.
            File.WriteAllText(xmlPath, BuildTaskXml(exePath), Encoding.Unicode);
            return Run($"/Create /F /TN {TaskName} /XML \"{xmlPath}\"");
        }
        finally
        {
            if (File.Exists(xmlPath)) File.Delete(xmlPath);
        }
    }

    public static int Uninstall()
    {
        AttachConsole(AttachParentProcess);
        return Run($"/Delete /F /TN {TaskName}");
    }

    private static string BuildTaskXml(string exePath)
    {
        var user = SecurityElement.Escape($@"{Environment.UserDomainName}\{Environment.UserName}");
        var command = SecurityElement.Escape(exePath);

        return $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo>
                <Description>PC Logger: records presence to check the 15/15 regime.</Description>
              </RegistrationInfo>
              <Triggers>
                <LogonTrigger>
                  <Enabled>true</Enabled>
                  <UserId>{user}</UserId>
                </LogonTrigger>
              </Triggers>
              <Principals>
                <Principal id="Author">
                  <UserId>{user}</UserId>
                  <LogonType>InteractiveToken</LogonType>
                  <RunLevel>LeastPrivilege</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <AllowHardTerminate>true</AllowHardTerminate>
                <StartWhenAvailable>true</StartWhenAvailable>
                <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
                <IdleSettings>
                  <StopOnIdleEnd>false</StopOnIdleEnd>
                  <RestartOnIdle>false</RestartOnIdle>
                </IdleSettings>
                <AllowStartOnDemand>true</AllowStartOnDemand>
                <Enabled>true</Enabled>
                <Hidden>false</Hidden>
                <RunOnlyIfIdle>false</RunOnlyIfIdle>
                <WakeToRun>false</WakeToRun>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Priority>7</Priority>
                <RestartOnFailure>
                  <Interval>PT1M</Interval>
                  <Count>3</Count>
                </RestartOnFailure>
              </Settings>
              <Actions Context="Author">
                <Exec>
                  <Command>{command}</Command>
                </Exec>
              </Actions>
            </Task>
            """;
    }

    private static int Run(string arguments)
    {
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

    // This is a WinExe, so it starts with no console of its own. Without attaching to the one
    // it was launched from, `PcLogger.exe --install` prints nothing and reads as a silent
    // failure.
    [DllImport("kernel32.dll")] private static extern bool AttachConsole(int processId);
}
