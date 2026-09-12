using System.Globalization;
using PcLogger.Core.Localization;
using Xunit;

namespace PcLogger.Core.Tests;

public class StringsTests
{
    [Fact]
    public void EveryLanguageCarriesTheSameKeys()
    {
        var reference = Strings.All["en"].Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();

        foreach (var (lang, dictionary) in Strings.All)
        {
            var keys = dictionary.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
            Assert.True(reference.SequenceEqual(keys),
                $"language '{lang}' differs from 'en': missing [" +
                string.Join(", ", reference.Except(keys)) + "], extra [" +
                string.Join(", ", keys.Except(reference)) + "]");
        }
    }

    [Theory]
    [InlineData("ru-RU", "ru")]
    [InlineData("ru", "ru")]
    [InlineData("en-US", "en")]
    [InlineData("", "en")]
    public void ResolveFallsBackToEnglish(string culture, string expected) =>
        Assert.Equal(expected, Strings.Resolve(CultureInfo.GetCultureInfo(culture)));

    [Fact]
    public void ResolveFallsBackToEnglishForAnUnsupportedCulture()
    {
        // "de" and friends are candidates, not guarantees — the design's own example (§8) adds
        // a "de" entry to Strings.All, so this picks whichever candidate nobody has added yet
        // instead of hardcoding one that may legitimately become supported.
        var unsupported = new[] { "de-DE", "fr-FR", "ja-JP", "zz-ZZ" }
            .First(c => !Strings.All.ContainsKey(CultureInfo.GetCultureInfo(c).TwoLetterISOLanguageName));

        Assert.Equal("en", Strings.Resolve(CultureInfo.GetCultureInfo(unsupported)));
    }

    // NotifyIcon.Text throws above 63 characters and TrayApp.Truncate cuts it mid-word,
    // so the formatted tooltip has to fit at a plausible failure count.
    [Fact]
    public void TheFailureTooltipFitsTheTrayLimit()
    {
        foreach (var dictionary in Strings.All.Values)
            Assert.True(dictionary["tray.failureTip"].Replace("{n}", "9999").Length <= 63);
    }
}
