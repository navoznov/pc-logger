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

        // Without these the process dies on the first escaped exception, and because an absent
        // bucket row reads as "the PC was not running", the report then shows the outage as an
        // unbroken rest period — perfect compliance, produced by a recorder that is gone.
        // Handling ThreadException also keeps the message loop alive instead of ending it.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Log(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log(e.ExceptionObject as Exception);

        try
        {
            using var app = new TrayApp(DataDir);
            Application.Run(app);
            return 0;
        }
        catch (Exception e)
        {
            // Startup failed, so there is no tray icon to complain through and nothing has
            // been recorded. Say so rather than vanishing.
            Log(e);
            MessageBox.Show("PC Logger не смог запуститься:\n\n" + e.Message,
                "PC Logger", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    private static void Log(Exception? e)
    {
        if (e is null) return;
        try
        {
            Directory.CreateDirectory(DataDir);
            File.AppendAllText(Path.Combine(DataDir, "error.log"),
                $"{DateTimeOffset.Now:u} {e}{Environment.NewLine}");
        }
        catch (Exception)
        {
            // Logging a failure must never itself take the process down.
        }
    }
}
