using PcLogger.Core.Config;
using PcLogger.Core.Model;
using PcLogger.Core.Reporting;
using PcLogger.Core.Storage;

// Generates three days of synthetic activity so the dashboard can be developed
// and reviewed without a Windows machine.

var root = Path.Combine(Path.GetTempPath(), "pclogger-demo");
Directory.CreateDirectory(root);
var dbPath = Path.Combine(root, "demo.db");
if (File.Exists(dbPath)) File.Delete(dbPath);

var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
now -= now % 10;

using var db = new Db(dbPath);
var buckets = new BucketStore(db);
var apps = new AppStore(db);

var gameId = apps.GetOrCreateAppId(@"D:\Games\deeprock\game.exe");
var browserId = apps.GetOrCreateAppId(@"C:\Program Files\Chrome\chrome.exe");
var random = new Random(42);

for (var day = 0; day < 3; day++)
{
    var dayStart = now - (long)day * 86_400;
    // A session from 16:00-ish: alternating play and rest of varying honesty.
    var cursor = dayStart - 6 * 3600;
    apps.OpenRun(gameId, cursor);

    for (var round = 0; round < 6; round++)
    {
        var playMinutes = random.Next(12, 26);
        var restMinutes = random.Next(4, 18);
        var focused = round % 3 != 2;

        for (var s = 0; s < playMinutes * 60; s += 10)
            buckets.Write(new Bucket(cursor + s, random.Next(4, 11), 0, focused ? gameId : browserId));
        cursor += playMinutes * 60;

        for (var s = 0; s < restMinutes * 60; s += 10)
            buckets.Write(new Bucket(cursor + s, 0, 0, gameId));
        cursor += restMinutes * 60;
    }

    apps.CloseRun(gameId, cursor);
}

var config = new AppConfig(new[] { @"D:\Games" });
var json = new ReportBuilder(db, config).BuildJson(now, historyDays: 5);

var dashboard = Path.Combine(AppContext.BaseDirectory, "dashboard");
var html = ReportBuilder.Render(
    File.ReadAllText(Path.Combine(dashboard, "dashboard.template.html")),
    json,
    name => File.ReadAllText(Path.Combine(dashboard, name)));

var output = Path.Combine(root, "report.html");
File.WriteAllText(output, html);
Console.WriteLine(output);
