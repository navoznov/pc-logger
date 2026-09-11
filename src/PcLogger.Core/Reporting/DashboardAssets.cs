using System.Reflection;

namespace PcLogger.Core.Reporting;

/// <summary>
/// Reads the dashboard's template and scripts, preferring the copy beside the executable so a
/// report can be re-styled without a rebuild, and falling back to the copy embedded in this
/// assembly. Spec §4 asks for both: the folder is the editable one, the resource is the one
/// that cannot be deleted by accident.
/// </summary>
public static class DashboardAssets
{
    public static string? Read(string name, string? directory = null)
    {
        if (directory is not null)
        {
            var onDisk = Path.Combine(directory, name);
            if (File.Exists(onDisk)) return File.ReadAllText(onDisk);
        }

        using var stream = typeof(DashboardAssets).Assembly
            .GetManifestResourceStream("dashboard/" + name);
        if (stream is null) return null;

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Names of the embedded assets, for diagnostics and tests.</summary>
    public static IReadOnlyList<string> EmbeddedNames() =>
        typeof(DashboardAssets).Assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith("dashboard/", StringComparison.Ordinal))
            .Select(n => n["dashboard/".Length..])
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
}
