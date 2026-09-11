using PcLogger.App.Win32;

namespace PcLogger.App;

internal static class Program
{
    private static readonly string DataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PcLogger");

    [STAThread]
    private static int Main(string[] args)
    {
        var exePath = Environment.ProcessPath!;

        if (args.Contains("--install")) return ScheduledTaskInstaller.Install(exePath);
        if (args.Contains("--uninstall")) return ScheduledTaskInstaller.Uninstall();

        using var single = new Mutex(true, @"Local\PcLoggerSingleInstance", out var acquired);
        if (!acquired) return 0;

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp(DataDir));
        return 0;
    }
}
