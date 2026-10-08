using System.Diagnostics;
using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmRelayAcceptanceTests
{
    [Theory]
    [InlineData("bom")]
    [InlineData("plain")]
    [InlineData("missing-prompt")]
    [InlineData("pending")]
    [InlineData("wrong-action")]
    [InlineData("foreign-delivery")]
    public async Task ActualDriver_RealStoryAcceptanceAndNormalExit(string scenario)
    {
        var start=new ProcessStartInfo("/usr/bin/python3"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
        start.ArgumentList.Add(Path.Combine(TestRepoPaths.RepoRoot,"tests/fixtures/ProductionMain/relay-acceptance-checks.py"));start.ArgumentList.Add(scenario);
        using var process=Process.Start(start)!;var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(8));Assert.True(process.ExitCode==0,(await output)+(await error));
    }
}
