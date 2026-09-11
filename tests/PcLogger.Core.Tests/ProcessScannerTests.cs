using PcLogger.Core.Sampling;
using Xunit;

namespace PcLogger.Core.Tests;

public class ProcessScannerTests
{
    [Fact]
    public void FirstScanReportsEverythingAsStarted()
    {
        var scanner = new ProcessScanner();

        var delta = scanner.Scan(new[] { @"D:\a.exe", @"D:\b.exe" });

        Assert.Equal(new[] { @"D:\a.exe", @"D:\b.exe" }, delta.Started.Order());
        Assert.Empty(delta.Ended);
    }

    [Fact]
    public void UnchangedScanReportsNothing()
    {
        var scanner = new ProcessScanner();
        scanner.Scan(new[] { @"D:\a.exe" });

        var delta = scanner.Scan(new[] { @"D:\a.exe" });

        Assert.Empty(delta.Started);
        Assert.Empty(delta.Ended);
    }

    [Fact]
    public void DisappearedProcessIsReportedAsEnded()
    {
        var scanner = new ProcessScanner();
        scanner.Scan(new[] { @"D:\a.exe", @"D:\b.exe" });

        var delta = scanner.Scan(new[] { @"D:\a.exe" });

        Assert.Empty(delta.Started);
        Assert.Equal(new[] { @"D:\b.exe" }, delta.Ended);
    }

    [Fact]
    public void NewProcessIsReportedAsStarted()
    {
        var scanner = new ProcessScanner();
        scanner.Scan(new[] { @"D:\a.exe" });

        var delta = scanner.Scan(new[] { @"D:\a.exe", @"D:\c.exe" });

        Assert.Equal(new[] { @"D:\c.exe" }, delta.Started);
        Assert.Empty(delta.Ended);
    }

    [Fact]
    public void CollapsesDuplicatePathsFromMultipleInstances()
    {
        var scanner = new ProcessScanner();

        var delta = scanner.Scan(new[] { @"D:\a.exe", @"D:\a.exe" });

        Assert.Single(delta.Started);
    }

    [Fact]
    public void TreatsPathsAsCaseInsensitive()
    {
        var scanner = new ProcessScanner();
        scanner.Scan(new[] { @"D:\a.exe" });

        var delta = scanner.Scan(new[] { @"d:\A.EXE" });

        Assert.Empty(delta.Started);
        Assert.Empty(delta.Ended);
    }

    [Fact]
    public void EmptyScanEndsEverything()
    {
        var scanner = new ProcessScanner();
        scanner.Scan(new[] { @"D:\a.exe" });

        var delta = scanner.Scan(Array.Empty<string>());

        Assert.Equal(new[] { @"D:\a.exe" }, delta.Ended);
    }
}
