using System.Diagnostics;
using Microsoft.Win32;
using PcLogger.App.Win32;
using PcLogger.Core.Config;
using PcLogger.Core.Recording;
using PcLogger.Core.Reporting;
using PcLogger.Core.Storage;

namespace PcLogger.App;

public sealed class TrayApp : ApplicationContext
{
    private readonly NotifyIcon _icon;
    private readonly ContextMenuStrip _menu;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly RecorderService _recorder;
    private readonly Db _db;

    private int _failures;
    private bool _stopped;

    public TrayApp(string dataDir)
    {
        _menu = new ContextMenuStrip();
        _menu.Items.Add("Отчёт", null, (_, _) => Guarded(() => OpenReport(dataDir)));
        _menu.Items.Add("Открыть папку данных", null, (_, _) => Guarded(() => Open(dataDir)));
        _menu.Items.Add("Игровые папки", null, (_, _) => Guarded(() => OpenConfig(dataDir)));
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Выход", null, (_, _) => Quit());

        _icon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "PC Logger",
            Visible = true,
            ContextMenuStrip = _menu
        };

        Directory.CreateDirectory(dataDir);
        _db = new Db(Path.Combine(dataDir, "pclogger.db"));
        _recorder = new RecorderService(new Win32SystemProbe(), new Win32ProcessProbe(), _db,
                                        onError: OnRecorderError);

        // Windows terminates the process on shutdown and logoff without running any menu item,
        // so without this the tail bucket is lost and every open app_run stays open.
        SystemEvents.SessionEnding += OnSessionEnding;

        _timer = new System.Windows.Forms.Timer { Interval = 1000 };
        _timer.Tick += (_, _) => _recorder.Tick(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        _timer.Start();
    }

    /// <summary>
    /// A failure inside the recorder is invisible by construction: an absent bucket row reads
    /// as "the PC was not running", which is exactly what a healthy evening away from the
    /// computer looks like. The tray icon is the only place a person can learn otherwise.
    /// </summary>
    private void OnRecorderError(Exception e)
    {
        _failures++;
        _icon.Text = Truncate($"PC Logger — сбой записи ({_failures})");
        if (_failures == 1)
        {
            _icon.ShowBalloonTip(10_000, "PC Logger",
                "Запись прервана: " + e.Message, ToolTipIcon.Warning);
        }
    }

    // NotifyIcon.Text throws above 63 characters.
    private static string Truncate(string text) => text.Length <= 63 ? text : text[..63];

    private void Guarded(Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            MessageBox.Show(e.Message, "PC Logger", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OpenReport(string dataDir)
    {
        var config = AppConfig.Load(Path.Combine(dataDir, "config.json"));
        var json = new ReportBuilder(_db, config).BuildJson(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        // The folder beside the exe wins so the report can be re-styled without a rebuild, but
        // deleting it no longer breaks anything: the same files are embedded in the assembly.
        var dashboard = Path.Combine(AppContext.BaseDirectory, "dashboard");
        var template = DashboardAssets.Read("dashboard.template.html", dashboard)
            ?? throw new InvalidOperationException("dashboard template missing");

        var html = ReportBuilder.Render(template, json, name => DashboardAssets.Read(name, dashboard));

        var output = Path.Combine(Path.GetTempPath(), "pclogger-report.html");
        File.WriteAllText(output, html);
        Open(output);
    }

    private static void OpenConfig(string dataDir)
    {
        var path = Path.Combine(dataDir, "config.json");
        AppConfig.Load(path);
        Process.Start(new ProcessStartInfo("notepad.exe", $"\"{path}\"") { UseShellExecute = true });
    }

    private static void Open(string target) =>
        Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });

    private void OnSessionEnding(object sender, SessionEndingEventArgs e) => StopRecording();

    private void StopRecording()
    {
        if (_stopped) return;
        _stopped = true;

        _timer.Stop();
        _recorder.Stop(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        _icon.Visible = false;
    }

    private void Quit()
    {
        StopRecording();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // A SystemEvents subscription is a static root: leaving it attached keeps this
            // object alive for the life of the process.
            SystemEvents.SessionEnding -= OnSessionEnding;
            StopRecording();
            _timer.Dispose();
            // Without disposing the icon, a ghost survives in the notification area until the
            // user hovers over it.
            _icon.Dispose();
            _menu.Dispose();
            _db.Dispose();
        }

        base.Dispose(disposing);
    }
}
