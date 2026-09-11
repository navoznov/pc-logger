using Microsoft.Data.Sqlite;

namespace PcLogger.Core.Storage;

/// <summary>Owns the SQLite connection and applies schema migrations on open.</summary>
public sealed class Db : IDisposable
{
    private const int SchemaVersion = 2;

    public SqliteConnection Connection { get; }

    public Db(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        Connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString());
        Connection.Open();
        Execute("PRAGMA journal_mode=WAL;");
        // One commit per 10-second bucket is ~8,640 fsyncs a day for data where losing the
        // last bucket to a power cut is harmless. NORMAL is the standard pairing with WAL.
        Execute("PRAGMA synchronous=NORMAL;");
        Migrate();
    }

    private void Migrate()
    {
        Execute("""
            CREATE TABLE IF NOT EXISTS meta (key TEXT PRIMARY KEY, value TEXT);
            CREATE TABLE IF NOT EXISTS buckets (
              ts        INTEGER PRIMARY KEY,
              active    INTEGER NOT NULL,
              pad       INTEGER NOT NULL,
              fg_app_id INTEGER
            );
            CREATE TABLE IF NOT EXISTS apps (
              id   INTEGER PRIMARY KEY AUTOINCREMENT,
              -- NOCASE folds ASCII A-Z only, matching the OrdinalIgnoreCase cache in AppStore
              -- for the paths we actually see. Non-ASCII case variants (e.g. Cyrillic) would
              -- still create separate rows; unreachable in practice, not worth fixing.
              path TEXT UNIQUE NOT NULL COLLATE NOCASE
            );
            CREATE TABLE IF NOT EXISTS app_runs (
              app_id  INTEGER NOT NULL,
              started INTEGER NOT NULL,
              ended   INTEGER
            );
            CREATE INDEX IF NOT EXISTS ix_app_runs_started ON app_runs(started);
            CREATE TABLE IF NOT EXISTS events (
              ts     INTEGER NOT NULL,
              kind   TEXT NOT NULL,
              detail TEXT
            );
            CREATE INDEX IF NOT EXISTS ix_events_ts ON events(ts);
            """);
        Execute($"INSERT OR REPLACE INTO meta(key, value) VALUES('schema_version', '{SchemaVersion}');");
    }

    public void Execute(string sql)
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => Connection.Dispose();
}
