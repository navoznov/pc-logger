using PcLogger.Core.Model;

namespace PcLogger.Core.Storage;

/// <summary>Append-only log of recorder lifecycle moments. See <see cref="RecorderEvent"/>.</summary>
public sealed class EventStore
{
    private readonly Db _db;

    public EventStore(Db db) => _db = db;

    public void Write(long ts, string kind, string? detail = null)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "INSERT INTO events(ts, kind, detail) VALUES ($ts, $kind, $detail);";
        cmd.Parameters.AddWithValue("$ts", ts);
        cmd.Parameters.AddWithValue("$kind", kind);
        cmd.Parameters.AddWithValue("$detail", (object?)detail ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<RecorderEvent> Read(long fromTs, long toTs)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT ts, kind, detail FROM events
            WHERE ts >= $from AND ts < $to
            ORDER BY ts;
            """;
        cmd.Parameters.AddWithValue("$from", fromTs);
        cmd.Parameters.AddWithValue("$to", toTs);

        var result = new List<RecorderEvent>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new RecorderEvent(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2)));
        }
        return result;
    }
}
