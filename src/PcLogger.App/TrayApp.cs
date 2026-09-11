using System.Diagnostics;
using PcLogger.App.Win32;
using PcLogger.Core.Config;
using PcLogger.Core.Recording;
using PcLogger.Core.Reporting;
using PcLogger.Core.Storage;

namespace PcLogger.App;

public sealed class TrayApp : ApplicationContext
{
    private readonly NotifyIcon _icon;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly RecorderService _recorder;
    private readonly Db _db;

    public TrayApp(string dataDir)
    {
        Directory.CreateDirectory(dataDir);
        _db = new Db(Path.Combine(dataDir, "pclogger.db"));
        _recorder = new RecorderService(new Win32SystemProbe(), new Win32ProcessProbe(), _db);

        var menu = new ContextMenuStrip();
        menu.Items.Add("Отчёт", null, (_, _) => OpenReport(dataDir));
        menu.Items.Add("Открыть папку данных", null, (_, _) => OpenFolder(dataDir));
        menu.Items.Add("Игровые папки", null, (_, _) => OpenConfig(dataDir));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) => Quit());

        _icon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "PC Logger",
            Visible = true,
            ContextMenuStrip = menu
        };

        _timer = new System.Windows.Forms.Timer { Interval = 1000 };
        _timer.Tick += (_, _) => _recorder.Tick(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        _timer.Start();
    }

    private void OpenReport(string dataDir)
    {
        var config = AppConfig.Load(Path.Combine(dataDir, "config.json"));
        var json = new ReportBuilder(_db, config).BuildJson(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        var dashboard = Path.Combine(AppContext.BaseDirectory, "dashboard");
        var html = ReportBuilder.Render(
            File.ReadAllText(Path.Combine(dashboard, "dashboard.template.html")),
            json,
            name => File.ReadAllText(Path.Combine(dashboard, name)));

        var output = Path.Combine(Path.GetTempPath(), "pclogger-report.html");
        File.WriteAllText(output, html);
        Open(output);
    }

    private static void OpenFolder(string dataDir) => Open(dataDir);

    private static void OpenConfig(string dataDir)
    {
        var path = Path.Combine(dataDir, "config.json");
        AppConfig.Load(path);
        Process.Start(new ProcessStartInfo("notepad.exe", $"\"{path}\"") { UseShellExecute = true });
    }

    private static void Open(string target) =>
        Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });

    private void Quit()
    {
        _timer.Stop();
        _recorder.Stop(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        _icon.Visible = false;
        _db.Dispose();
        ExitThread();
    }
}
