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
    public async Task ActualDriver_CurrentFailureAndCleanup(string mode)
    {
        var start=new ProcessStartInfo("/usr/bin/python3"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
        start.ArgumentList.Add(Path.Combine(TestRepoPaths.RepoRoot,"tests/fixtures/ProductionMain/driver-failure-checks.py"));start.ArgumentList.Add(mode);
        using var process=Process.Start(start)!;
        var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(8));
        Assert.True(process.ExitCode==0,(await output)+(await error));
    }
}
