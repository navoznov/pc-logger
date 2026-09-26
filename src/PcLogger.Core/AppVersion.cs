namespace PcLogger.Core;

/// <summary>
/// The release number, stamped into the assemblies from Directory.Build.props. One source for
/// the tray and the report, so a report someone sends back names the build that produced it.
/// </summary>
public static class AppVersion
{
    public static string Current { get; } = typeof(AppVersion).Assembly.GetName().Version!.ToString(3);
}
