namespace PcLogger.App;

internal static class Program
{
    private static readonly string DataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PcLogger");

    [STAThread]
    private static int Main(string[] args)
    {
        using var single = new Mutex(true, @"Local\PcLoggerSingleInstance", out var acquired);
        if (!acquired) return 0;

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp(DataDir));
        return 0;
    }
}
