using System.Diagnostics;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmRelayReusableTests
{
    [Theory]
    [InlineData("read")]
    [InlineData("incomplete")]
    [InlineData("changed-prompt")]
    [InlineData("wrong-queue")]
    [InlineData("changed-request")]
    [InlineData("closed")]
    [InlineData("duplicate")]
    [InlineData("packet-bound")]
    [InlineData("missing-adapter")]
    [InlineData("atomic-no-replace")]
    [InlineData("partial-publication")]
    [InlineData("close-unconfirmed")]
    public async Task WorkerContract_ExactBytesIdentityPublicationAndClose(string scenario)
    {
        var own=Path.Combine(TestRepoPaths.RepoRoot,"TestResults/relay-reusable",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(own);
        var start=new ProcessStartInfo("python3"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var arg in new[]{Path.Combine(TestRepoPaths.RepoRoot,"tests/fixtures/ProductionMain/relay-reusable-checks.py"),scenario,own})start.ArgumentList.Add(arg);
        using var process=Process.Start(start)!;
        var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(8));
        var log=(await output)+(await error);await File.WriteAllTextAsync(Path.Combine(own,"contract.log"),log);
        Assert.True(process.ExitCode==0,log);
    }

    [Theory]
    [InlineData("production-main-relay-reusable")]
    [InlineData("production-main-relay-consumer-error")]
    public Task ProductionBridge_RelocatedWorkerConsumerAndOriginalRetirement(string mode)=>ProductionMainLinuxFixture.RunAsync(mode);
}
