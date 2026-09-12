using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using PcLogger.Core.Config;
using PcLogger.Core.Localization;
using PcLogger.Core.Storage;

namespace PcLogger.Core.Reporting;

/// <summary>Turns stored buckets and runs into the single JSON payload the dashboard reads.</summary>
public sealed class ReportBuilder
{
    private const int BucketSeconds = 10;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly Db _db;
    private readonly AppConfig _config;

    public ReportBuilder(Db db, AppConfig config)
    {
        _db = db;
        _config = config;
    }

    public string BuildJson(long nowTs, int historyDays = 90)
    {
        // BucketStore.Read is inclusive on both ends, but SpanBuilder's presence and
        // game-span walks are half-open [from, to): a bucket stored at exactly `to`
        // would reach app_spans (which iterates buckets directly) but never spans or
        // game_spans (which walk the grid). That mismatch is unreachable today only
        // because `to` is rounded to a boundary past nowTs and the collector never
        // writes buckets for the future — a different caller passing an unrounded `to`
        // could resurrect it.
        var from = nowTs - (long)historyDays * 86_400;
        from -= from % BucketSeconds;
        var to = nowTs - nowTs % BucketSeconds + BucketSeconds;

        var buckets = new BucketStore(_db).Read(from, to);
        var apps = new AppStore(_db);
        var paths = apps.AllPaths();
        var runs = apps.ReadRuns(from, to);
        var folders = new GameFolders(_config.GameFolders);

        var payload = new
        {
            generated = nowTs,
            tz_offset_minutes = (int)TimeZoneInfo.Local.GetUtcOffset(DateTimeOffset.FromUnixTimeSeconds(nowTs)).TotalMinutes,
            // The language the report OPENS in. The switcher in the page overrides it and
            // remembers the choice; this is only the default, and it is the same default the
            // tray speaks, so the two cannot disagree on a fresh machine.
            lang = Strings.Current,
            defaults = new { micro_gap = 3, break_minutes = 15, session_minutes = 15, tolerance = 2, day_norm_hours = 2 },
            spans = SpanBuilder.BuildPresence(buckets, from, to, BucketSeconds)
                .Select(x => new { t = x.T, d = x.D, s = x.S }),
            app_spans = SpanBuilder.BuildAppSpans(buckets, paths, BucketSeconds)
                .Select(x => new { t = x.T, d = x.D, app = x.App }),
            game_spans = SpanBuilder.BuildGameSpans(runs, buckets, paths, folders, from, to, BucketSeconds)
                .Select(x => new { t = x.T, d = x.D, lvl = x.Lvl, app = x.App }),
            intensity = SpanBuilder.BuildIntensity(buckets, from, to),
            // Without these the dashboard cannot tell a quiet evening from a dead recorder:
            // both are simply an absence of buckets.
            events = new EventStore(_db).Read(from, to)
                .Select(x => new { t = x.Ts, kind = x.Kind, detail = x.Detail })
        };

        return JsonSerializer.Serialize(payload, Json);
    }

    /// <summary>
    /// Inlines every referenced script and substitutes the data placeholder, so the
    /// produced report is a single file while the sources stay separately testable.
    /// </summary>
    public static string Render(string template, string json, Func<string, string?> readScript)
    {
        var html = Regex.Replace(
            template,
            """<script\s+src="(?<name>[^"]+)"\s*></script>""",
            match =>
            {
                var name = match.Groups["name"].Value;
                var body = readScript(name)
                    ?? throw new InvalidOperationException($"dashboard script not found: {name}");
                return $"<script>{body}</script>";
            });

        // The pattern is deliberately narrow, and a tag it does not match would be left in the
        // output pointing at a file that will not exist beside the report — a silently broken
        // dashboard. Adding `defer`, single quotes, or an attribute before src is enough to miss.
        if (Regex.IsMatch(html, @"<script[^>]*\ssrc=", RegexOptions.IgnoreCase))
            throw new InvalidOperationException("a <script src=...> tag was not inlined");

        if (!html.Contains("/*__DATA__*/"))
            throw new InvalidOperationException("the template has no /*__DATA__*/ placeholder");

        return html.Replace("/*__DATA__*/", json);
    }
}
