using System.Diagnostics;
using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class MortalDaemonSettlementOracleTests
{
    [Theory]
    [InlineData("continuation-valid")][InlineData("continuation-guard")][InlineData("hash-tamper")]
    [InlineData("continuation-running")][InlineData("continuation-owner")][InlineData("valid")][InlineData("wrong-owner")][InlineData("wrong-session")]
    [InlineData("missing")][InlineData("malformed")][InlineData("null")][InlineData("duplicates")]
    [InlineData("running")][InlineData("false")][InlineData("wrong-type")][InlineData("original-settled")][InlineData("lying-success")]
    public async Task ActualPhaseAcceptanceRequiresOriginalAndFinalDaemonPair(string scenario)
    {
        var start=new ProcessStartInfo("/usr/bin/python3"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var arg in new[]{Path.Combine(TestRepoPaths.RepoRoot,"tests/fixtures/DaemonSettlement/check.py"),Path.Combine(TestRepoPaths.RepoRoot,"tests/fixtures/ProductionMain/live-mortal-phase.py"),scenario})start.ArgumentList.Add(arg);
        using var process=Process.Start(start)!;var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
        try{await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));}
        finally{if(!process.HasExited){process.Kill(true);await process.WaitForExitAsync();}await Task.WhenAll(output,error);}
        Assert.True(process.ExitCode==0,(await output)+(await error));
    }
}
