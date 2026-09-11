namespace PcLogger.Core.Config;

/// <summary>
/// Decides whether an executable belongs to one of the configured game folders.
/// Comparison is case-insensitive and separator-agnostic, and respects
/// directory boundaries so "D:\Games2" is not part of "D:\Games".
/// </summary>
public sealed class GameFolders
{
    private readonly string[] _prefixes;

    public GameFolders(IEnumerable<string> folders)
    {
        _prefixes = folders
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Select(Normalize)
            .Select(f => f.EndsWith('\\') ? f : f + '\\')
            .Distinct()
            .ToArray();
    }

    public bool IsGame(string? exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath)) return false;
        var path = Normalize(exePath);
        return _prefixes.Any(prefix => path.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static string Normalize(string path) =>
        Environment.ExpandEnvironmentVariables(path.Trim())
            .Replace('/', '\\')
            .ToUpperInvariant();
}
