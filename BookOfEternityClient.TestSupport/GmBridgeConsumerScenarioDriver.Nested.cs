using System.Diagnostics;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
namespace BookOfEternityClient.Tests;
internal static partial class GmBridgeConsumerScenarioDriver
{
    private static async Task VerifyNestedParticipationAsync(string status, string session, string output,
        GmSessionRunRecord original, Dictionary<string,object?> result, CancellationToken token)
    {
        if(BridgeTransportConnector.DecodeStatus(status).Kind!="linux-fifo-v1")throw new IOException("Nested control requires actual FIFO.");
        var directory=new DirectoryInfo(AppContext.BaseDirectory);
        while(directory!=null && !File.Exists(Path.Combine(directory.FullName,"AGENTS.md")))directory=directory.Parent;
        var repo=directory?.FullName??throw new IOException("Source root missing.");
        var start=new ProcessStartInfo("pwsh"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var arg in new[]{"-NoProfile","-File",Path.Combine(repo,"tests/fixtures/ProductionMain/nested-participation.ps1"),
            "-GameSessionPath",session,"-Helper",Path.Combine(repo,"BookOfEternityClient/Launcher/gm_main_operation.ps1")})start.ArgumentList.Add(arg);
        using var stdout=File.Create(Path.Combine(output,"nested.stdout.log"));
        using var stderr=File.Create(Path.Combine(output,"nested.stderr.log"));
        using var process=Process.Start(start)??throw new IOException("Nested helper did not start.");
        var outPump=process.StandardOutput.BaseStream.CopyToAsync(stdout);
        var errPump=process.StandardError.BaseStream.CopyToAsync(stderr);
        try {await process.WaitForExitAsync(token);}
        finally
        {
            using var cleanup=new CancellationTokenSource(TimeSpan.FromSeconds(5));
            if(!process.HasExited)process.Kill(entireProcessTree:true);
            await process.WaitForExitAsync(cleanup.Token);
            await Task.WhenAll(outPump,errPump).WaitAsync(cleanup.Token);
            await stdout.FlushAsync(cleanup.Token);await stderr.FlushAsync(cleanup.Token);
        }
        await stdout.DisposeAsync();await stderr.DisposeAsync();
        result["NestedHelperExit"]=process.ExitCode;
        if(process.ExitCode!=0)throw new IOException("Nested helper failed; retain full stdout/stderr.");
        using var data=JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output,"nested.stdout.log"),token));
        var rows=data.RootElement.GetProperty("operations").EnumerateArray().ToArray();
        if(rows.Length!=2)throw new IOException("Expected outer and fresh heartbeat operations.");
        var closes=rows.Select(row=>JsonSerializer.Deserialize<MainOperationClose>(row.GetProperty("terminalClose"),MainOperationReader.Json)!).ToArray();
        var nested=JsonSerializer.Deserialize<MainOperationClose>(data.RootElement.GetProperty("nestedOriginal"),MainOperationReader.Json)!;
        if(closes.Any(c=>c.Identity!=original.Identity) || closes[0].OperationId==closes[1].OperationId ||
            nested.PinId!=closes[0].PinId || nested.CloseId!=closes[0].CloseId || nested.OperationId!=closes[0].OperationId || nested.Identity!=original.Identity)
            throw new IOException("Nested original or fresh heartbeat identity mismatch.");
        foreach(var close in closes)
        {
            var raw=await BridgeTransportConnector.RequestAsync(status,JsonSerializer.Serialize(new{command="mainOperationStatus",mainOperationClose=close}),3000,token);
            var reply=JsonSerializer.Deserialize<MainOperationReply>(raw,MainOperationReader.Json);
            if(reply!=new MainOperationReply(true,MainOperationState.ClosedObserved,close.PinId,close.CloseId,close.OperationId,close.Identity))
                throw new IOException("Original server did not observe exact immutable close.");
        }
        result["NestedParticipation"]=data.RootElement.Clone();
    }
}
