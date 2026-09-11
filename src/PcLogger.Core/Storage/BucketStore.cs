using Microsoft.Data.Sqlite;
using PcLogger.Core.Model;

namespace PcLogger.Core.Storage;

/// <summary>Reads and writes 10-second buckets. Writing the same ts twice replaces the row.</summary>
public sealed class BucketStore
{
    private readonly Db _db;

    public BucketStore(Db db) => _db = db;

    public void Write(Bucket bucket)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO buckets(ts, active, pad, fg_app_id)
            VALUES ($ts, $active, $pad, $app)
            ON CONFLICT(ts) DO UPDATE SET
              active = excluded.active,
              pad = excluded.pad,
              fg_app_id = excluded.fg_app_id;
            """;
        cmd.Parameters.AddWithValue("$ts", bucket.Ts);
        cmd.Parameters.AddWithValue("$active", bucket.Active);
        cmd.Parameters.AddWithValue("$pad", bucket.Pad);
        cmd.Parameters.AddWithValue("$app", (object?)bucket.FgAppId ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<Bucket> Read(long fromTs, long toTs)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT ts, active, pad, fg_app_id FROM buckets
            WHERE ts >= $from AND ts <= $to
            ORDER BY ts;
            """;
        cmd.Parameters.AddWithValue("$from", fromTs);
        cmd.Parameters.AddWithValue("$to", toTs);

        var result = new List<Bucket>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new Bucket(
                reader.GetInt64(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.IsDBNull(3) ? null : reader.GetInt64(3)));
        }
        return result;
    }

    /// <summary>Timestamp of the newest stored bucket, or null when the table is empty.</summary>
    public long? LastTs()
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT MAX(ts) FROM buckets;";
        return cmd.ExecuteScalar() is long ts ? ts : null;
    }
}
