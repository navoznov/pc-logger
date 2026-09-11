using PcLogger.Core.Model;

namespace PcLogger.Core.Storage;

/// <summary>
/// Stores executable paths in a lookup table and tracks the lifetime of processes
/// that own a visible window. Storing full paths is what makes game classification
/// replayable: changing the folder list re-colours existing history.
/// </summary>
public sealed class AppStore
{
    private readonly Db _db;
    private readonly Dictionary<string, long> _idCache = new(StringComparer.OrdinalIgnoreCase);

    public AppStore(Db db) => _db = db;

    public long GetOrCreateAppId(string path)
    {
        if (_idCache.TryGetValue(path, out var cached)) return cached;

        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO apps(path) VALUES ($path) ON CONFLICT(path) DO NOTHING;
            SELECT id FROM apps WHERE path = $path;
            """;
        cmd.Parameters.AddWithValue("$path", path);
        var id = (long)cmd.ExecuteScalar()!;
        _idCache[path] = id;
        return id;
    }

    public string? GetPath(long appId)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT path FROM apps WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", appId);
        return cmd.ExecuteScalar() as string;
    }

    public IReadOnlyDictionary<long, string> AllPaths()
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT id, path FROM apps;";
        var result = new Dictionary<long, string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) result[reader.GetInt64(0)] = reader.GetString(1);
        return result;
    }

    /// <summary>
    /// Opens a run for an app. Runs are keyed by executable path, not by process, so an
    /// app has at most one open run at a time: the scanner reports a path as started only
    /// when it was absent from the previous scan. The WHERE NOT EXISTS makes that invariant
    /// structural, so CloseRun below can never close a second, still-running row.
    /// </summary>
    public void OpenRun(long appId, long startedTs)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO app_runs(app_id, started, ended)
            SELECT $id, $started, NULL
            WHERE NOT EXISTS (SELECT 1 FROM app_runs WHERE app_id = $id AND ended IS NULL);
            """;
        cmd.Parameters.AddWithValue("$id", appId);
        cmd.Parameters.AddWithValue("$started", startedTs);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Closes the app's open run. At most one exists — see OpenRun.</summary>
    public void CloseRun(long appId, long endedTs)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "UPDATE app_runs SET ended = $ended WHERE app_id = $id AND ended IS NULL;";
        cmd.Parameters.AddWithValue("$id", appId);
        cmd.Parameters.AddWithValue("$ended", endedTs);
        cmd.ExecuteNonQuery();
    }

    public void CloseDangling(long endedTs)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "UPDATE app_runs SET ended = $ended WHERE ended IS NULL;";
        cmd.Parameters.AddWithValue("$ended", endedTs);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<AppRun> ReadRuns(long fromTs, long toTs)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT app_id, started, ended FROM app_runs
            WHERE started <= $to AND (ended IS NULL OR ended >= $from)
            ORDER BY started, app_id;
            """;
        cmd.Parameters.AddWithValue("$from", fromTs);
        cmd.Parameters.AddWithValue("$to", toTs);

        var result = new List<AppRun>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new AppRun(
                reader.GetInt64(0),
                reader.GetInt64(1),
                reader.IsDBNull(2) ? null : reader.GetInt64(2)));
        }
        return result;
    }
}
