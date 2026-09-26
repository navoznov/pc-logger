using Xunit;

namespace PcLogger.Core.Tests;

public class AppVersionTests
{
    // 1.0.0 is what the SDK stamps when no <Version> reaches the project: a moved or renamed
    // Directory.Build.props would otherwise go unnoticed and every build would claim to be 1.0.0.
    [Fact]
    public void ComesFromTheSharedBuildProps()
    {
        Assert.Matches(@"^\d+\.\d+\.\d+$", AppVersion.Current);
        Assert.NotEqual("1.0.0", AppVersion.Current);
    }
}
