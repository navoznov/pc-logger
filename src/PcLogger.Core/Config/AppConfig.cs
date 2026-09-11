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
        try
        {
            if (!File.Exists(path))
            {
                var empty = new AppConfig(Array.Empty<string>());
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
                File.WriteAllText(path, JsonSerializer.Serialize(empty, Options));
                return empty;
            }

            // "{}" and {"game_folders": null} both parse successfully and leave the
            // array null, so a null check is needed on top of the JsonException catch.
            var loaded = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path));
            if (loaded?.GameFolders is { } folders) return new AppConfig(folders);
        }
        catch (Exception e) when (
            e is JsonException or IOException or UnauthorizedAccessException
              or ArgumentException or NotSupportedException)
        {
            // Neither a broken config nor an unwritable directory may stop the report.
            // Game folders only colour a context track; the presence data underneath is
            // the answer the user actually came for, and it must never be lost to a typo.
        }

        return new AppConfig(Array.Empty<string>());
    }
}
