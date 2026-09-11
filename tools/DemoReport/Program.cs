using PcLogger.Core.Config;
using PcLogger.Core.Model;
using PcLogger.Core.Reporting;
using PcLogger.Core.Storage;

// Generates a week of synthetic activity so the dashboard can be developed
// and reviewed without a Windows machine. A week, not a day, because the week
// view needs seven populated rows to be reviewable at all.

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

for (var day = 0; day < 7; day++)
{
    var dayStart = now - (long)day * 86_400;
    // A session from 16:00-ish: alternating play and rest of varying honesty.
    var cursor = dayStart - 6 * 3600;
    apps.OpenRun(gameId, cursor);

    for (var round = 0; round < 6; round++)
    {
        // Ranges are chosen so the demo shows BOTH verdicts. Play never exceeds the
        // 15 + 2 limit on its own, so a round separated by a real break renders "ok";
        // rest falls below breakMinutes often enough that some rounds merge into one
        // long block and render "over". A generator that only ever produces violations
        // cannot exercise the dashboard's main visual distinction.
        // Every third day the child keeps the regime: rest always clears the 15-minute
        // break threshold, so no two rounds merge and no block can exceed 15 + 2. The
        // other days mix honest and dishonest rests. Without both kinds the week view
        // has nothing to contrast - a demo where every day is a violation cannot show
        // that the report distinguishes them.
        var disciplined = day % 3 == 0;
        var playMinutes = disciplined ? random.Next(10, 16) : random.Next(10, 17);
        var restMinutes = disciplined ? random.Next(16, 22) : random.Next(12, 21);
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
var json = new ReportBuilder(db, config).BuildJson(now, historyDays: 9);

var dashboard = Path.Combine(AppContext.BaseDirectory, "dashboard");
var html = ReportBuilder.Render(
    File.ReadAllText(Path.Combine(dashboard, "dashboard.template.html")),
    json,
    name => File.ReadAllText(Path.Combine(dashboard, name)));

var output = Path.Combine(root, "report.html");
File.WriteAllText(output, html);
Console.WriteLine(output);
