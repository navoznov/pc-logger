using System.Text.Json;
using System.Text.Json.Serialization;

namespace PcLogger.Core.Config;

/// <summary>
/// Read only when a report is built, never by the collector: the collector records
/// every windowed process regardless of this list, which is what lets a changed
/// folder list re-colour existing history without a restart.
/// </summary>
public sealed record AppConfig(
    [property: JsonPropertyName("game_folders")] string[] GameFolders)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static AppConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            var empty = new AppConfig(Array.Empty<string>());
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(path, JsonSerializer.Serialize(empty, Options));
            return empty;
        }

        try
        {
            return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path))
                ?? new AppConfig(Array.Empty<string>());
        }
        catch (JsonException)
        {
            // A broken config must not stop the report from being built.
            return new AppConfig(Array.Empty<string>());
        }
    }
}
