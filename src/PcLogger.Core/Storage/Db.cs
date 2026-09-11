using Microsoft.Data.Sqlite;

namespace PcLogger.Core.Storage;

/// <summary>Owns the SQLite connection and applies schema migrations on open.</summary>
public sealed class Db : IDisposable
{
    private const int SchemaVersion = 1;

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
              path TEXT UNIQUE NOT NULL
            );
            CREATE TABLE IF NOT EXISTS app_runs (
              app_id  INTEGER NOT NULL,
              started INTEGER NOT NULL,
              ended   INTEGER
            );
            CREATE INDEX IF NOT EXISTS ix_app_runs_started ON app_runs(started);
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
