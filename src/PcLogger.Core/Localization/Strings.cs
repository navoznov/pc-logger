using System.Globalization;

namespace PcLogger.Core.Localization;

/// <summary>
/// The seven strings the tray and the startup failure path need. Lives in Core rather than
/// in App because the WinForms layer can be compiled but not executed off Windows, and the
/// resolution rule is the part worth testing.
/// </summary>
public static class Strings
{
    private const string Fallback = "en";

    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> All { get; } =
        new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["ru"] = new Dictionary<string, string>
            {
                ["tray.report"] = "Отчёт",
                ["tray.dataFolder"] = "Открыть папку данных",
                ["tray.gameFolders"] = "Игровые папки",
                ["tray.quit"] = "Выход",
                ["tray.failureTip"] = "PC Logger — сбой записи ({n})",
                ["tray.failureBalloon"] = "Запись прервана: {message}",
                ["app.startupFailed"] = "PC Logger не смог запуститься:\n\n{message}"
            },
            ["en"] = new Dictionary<string, string>
            {
                ["tray.report"] = "Report",
                ["tray.dataFolder"] = "Open data folder",
                ["tray.gameFolders"] = "Game folders",
                ["tray.quit"] = "Quit",
                ["tray.failureTip"] = "PC Logger — recording failure ({n})",
                ["tray.failureBalloon"] = "Recording stopped: {message}",
                ["app.startupFailed"] = "PC Logger could not start:\n\n{message}"
            }
        };

    /// <summary>The language the whole application speaks, fixed at process start.</summary>
    public static string Current { get; } = Resolve(CultureInfo.CurrentUICulture);

    public static string Resolve(CultureInfo culture)
    {
        var code = culture.TwoLetterISOLanguageName;
        return All.ContainsKey(code) ? code : Fallback;
    }

    /// <summary>
    /// An unknown key returns itself: a key on screen is a visible bug, which is what we
    /// want, rather than a blank label nobody notices.
    /// </summary>
    public static string Get(string key) =>
        All[Current].TryGetValue(key, out var text) ? text : key;
}
