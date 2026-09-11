using PcLogger.Core.Config;
using Xunit;

namespace PcLogger.Core.Tests;

public class GameFoldersTests
{
    [Fact]
    public void MatchesFileInsideFolder()
    {
        var folders = new GameFolders(new[] { @"D:\Steam\steamapps\common" });
        Assert.True(folders.IsGame(@"D:\Steam\steamapps\common\DeepRock\game.exe"));
    }

    [Fact]
    public void IgnoresCaseAndSeparatorStyle()
    {
        var folders = new GameFolders(new[] { @"D:\Steam\steamapps\common" });
        Assert.True(folders.IsGame(@"d:/STEAM/steamapps/Common/x/game.exe"));
    }

    [Fact]
    public void DoesNotMatchSiblingSharingPrefix()
    {
        var folders = new GameFolders(new[] { @"D:\Games" });
        Assert.False(folders.IsGame(@"D:\Games2\game.exe"));
    }

    [Fact]
    public void DoesNotMatchPathOutsideFolders()
    {
        var folders = new GameFolders(new[] { @"D:\Games" });
        Assert.False(folders.IsGame(@"C:\Windows\notepad.exe"));
    }

    [Fact]
    public void ExpandsEnvironmentVariables()
    {
        Environment.SetEnvironmentVariable("PCLOG_TEST_ROOT", @"D:\Games");
        var folders = new GameFolders(new[] { @"%PCLOG_TEST_ROOT%\Steam" });
        Assert.True(folders.IsGame(@"D:\Games\Steam\a\game.exe"));
    }

    [Fact]
    public void TreatsTrailingSeparatorInConfigAsEquivalent()
    {
        var folders = new GameFolders(new[] { @"D:\Games\" });
        Assert.True(folders.IsGame(@"D:\Games\a\game.exe"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsMissingPaths(string? path)
    {
        var folders = new GameFolders(new[] { @"D:\Games" });
        Assert.False(folders.IsGame(path));
    }

    [Fact]
    public void EmptyConfigurationMatchesNothing()
    {
        var folders = new GameFolders(Array.Empty<string>());
        Assert.False(folders.IsGame(@"D:\Games\a\game.exe"));
    }
}
