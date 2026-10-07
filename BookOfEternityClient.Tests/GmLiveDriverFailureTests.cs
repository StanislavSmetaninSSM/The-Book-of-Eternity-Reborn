using System.Diagnostics;
using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmLiveDriverFailureTests
{
    [Theory]
    [InlineData("provider")]
    [InlineData("client-error")]
    [InlineData("process-exit")]
    [InlineData("error-pause")]
    [InlineData("unknown-stop")]
    [InlineData("old-provider")]
    [InlineData("old-client")]
    public async Task ActualDriver_CurrentFailureAndCleanup(string mode)
        =>await RunInertAsync(mode);
    [Theory]
    [InlineData("coalesced-pause")]
    [InlineData("early-startup")]
    [InlineData("missing-rollback")]
    [InlineData("post-esc-pause")]
    [InlineData("repeated-pause")]
    public Task ActualDriver_ReviewPhaseBoundaries(string mode)=>RunInertAsync(mode);
    private static async Task RunInertAsync(string mode)
    {
        var start=new ProcessStartInfo("/usr/bin/python3"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
        start.ArgumentList.Add(Path.Combine(TestRepoPaths.RepoRoot,"tests/fixtures/ProductionMain/driver-failure-checks.py"));start.ArgumentList.Add(mode);
        using var process=Process.Start(start)!;
        var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(8));
        Assert.True(process.ExitCode==0,(await output)+(await error));
    }
    [Fact]
    public Task RealProduction_InertRefusalCancelsBeforeOriginalStop()=>ProductionMainLinuxFixture.RunAsync("driver-provider-refusal");
    [Fact]
    public Task HeldOriginalPin_DrainTimeoutRemainsUncertain()=>ProductionMainLinuxFixture.RunAsync("production-main-driver-held-pin");
}
