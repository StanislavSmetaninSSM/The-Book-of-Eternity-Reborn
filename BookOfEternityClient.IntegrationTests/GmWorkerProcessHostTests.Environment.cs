using System.Diagnostics;
using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GmWorkerProcessHostTests
{
    [Theory]
    [InlineData("HTTP_PROXY", "http_proxy")]
    [InlineData("BOE_SYNTHETIC_NAME", "boe_synthetic_name")]
    public async Task Environment_ActualHostAdmitsCaseAliasesThenClosesWithoutRelease(
        string upperName, string lowerName)
    {
        var root = CreateTempRoot();
        var marker = Path.Combine(root, "environment-worker-started");
        Process? host = null;
        try
        {
            // CreateWorker clears only this synthetic worker payload, never the
            // current process or hidden host environment/network settings.
            var worker = CreateWorker(root);
            worker.Environment[upperName] = "SYNTHETIC_UPPER_VALUE";
            worker.Environment[lowerName] = "synthetic_lower_value";
            worker.Environment["BOE_START_MARKER"] = marker;
            worker.ArgumentList.Add("-NoProfile");
            worker.ArgumentList.Add("-NonInteractive");
            worker.ArgumentList.Add("-Command");
            worker.ArgumentList.Add("[IO.File]::WriteAllText($env:BOE_START_MARKER, 'started')");
            Assert.Equal(OperatingSystem.IsWindows() ? 2 : 3, worker.Environment.Count);

            await using (var launch = GmWorkerProcessHostLaunch.Create(worker, root))
            {
                Assert.DoesNotContain(launch.StartInfo.ArgumentList,
                    argument => argument.Contains("SYNTHETIC_UPPER_VALUE", StringComparison.Ordinal) ||
                                argument.Contains("synthetic_lower_value", StringComparison.Ordinal));
                host = Process.Start(launch.StartInfo)!;
                Assert.NotEqual(Environment.ProcessId, host.Id);
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                await launch.WaitUntilReadyAsync(host, deadline.Token);
                Assert.False(host.HasExited);
                Assert.False(File.Exists(marker));
                // Ready proves capture and strict host JSON admission. Rebuilding
                // the worker ProcessStartInfo is checked purely, without Release.
            }
            await host.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(125, host.ExitCode);
            Assert.False(File.Exists(marker));
            var diagnostics = await host.StandardError.ReadToEndAsync();
            Assert.False(diagnostics.Contains("SYNTHETIC_UPPER_VALUE", StringComparison.Ordinal));
            Assert.False(diagnostics.Contains("synthetic_lower_value", StringComparison.Ordinal));
        }
        finally
        {
            await StopOwnedProcessAsync(host);
            CleanupTempRoot(root);
        }
    }
}
