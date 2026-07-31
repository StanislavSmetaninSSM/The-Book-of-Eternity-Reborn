using System.Diagnostics;
using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "ProcessIntegration")]
public sealed class GmWorkerProcessTreeTests
{
    [Fact]
    public void ProcessTreeFactory_UnsupportedPlatformFailsClosedBeforeAttach()
    {
        var factory = new GmWorkerProcessTreeFactory(
            isWindows: () => false,
            windowsFactory: _ => throw new Xunit.Sdk.XunitException("Windows factory must not run."));

        var error = Assert.Throws<PlatformNotSupportedException>(() =>
            factory.Attach(new Process()));

        Assert.Contains("Windows Job Object", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProcessIsolation_DoesNotShipFalseUnixProcessGroupBoundary()
    {
        var assembly = typeof(GmWorkerProcessTreeFactory).Assembly;

        Assert.Null(assembly.GetType("BookOfEternityClient.Services.GmWorkers.ManagedProcessTree"));
        Assert.Null(assembly.GetType("BookOfEternityClient.Services.GmWorkers.UnixProcessGroupController"));
        Assert.Null(assembly.GetType("BookOfEternityClient.Services.GmWorkers.GmWorkerOwnerMonitor"));
    }

    [Fact]
    public async Task StopUnattachedProcessTree_WhenExitCannotBeConfirmed_IsBounded()
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            ArgumentList = { "-NoLogo", "-NoProfile", "-Command", "Start-Sleep -Seconds 30" }
        })!;

        var error = await Assert.ThrowsAsync<TimeoutException>(() =>
            GmWorkerBridgePool.StopUnattachedProcessTreeAsync(
                process,
                TimeSpan.FromMilliseconds(50),
                (_, cancellationToken) => Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken)));

        Assert.Contains("unattached", error.Message, StringComparison.OrdinalIgnoreCase);
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }
}
