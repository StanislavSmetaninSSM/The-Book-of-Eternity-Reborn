using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityGMBridge;
using BookOfEternityClient.Services.GmRuntime;

namespace BookOfEternityClient.Tests;
internal sealed partial class GmLocalRemoteConsumerScenario
{
    internal static async Task<int> RunScenarioAsync(string folder)
    {
        var evidence=new Dictionary<string,object?>();
        var root=Path.Combine(folder,"root");var session=Path.Combine(root,"game_session");
        var statusPath=Path.Combine(session,"game_state/control/gm_bridge_status.json");
        BridgeHost? host=null;Task<int>? running=null;GmLocalRemoteConsumerScenario? warm=null;
        GmSessionRunRecord? original=null;string? capturedStatus=null;var exit=1;
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(24));
        try {
            warm=await StartAsync(folder);
            evidence["LocalClosedBeforeExternalMain"]=true;
            host=new BridgeHost(session,"readmission-"+Guid.NewGuid().ToString("N"));
            running=host.RunAsync();
            while(original==null) {
                deadline.Token.ThrowIfCancellationRequested();
                if(running.IsCompleted)throw new IOException("External main ended before original publication.");
                if(File.Exists(statusPath)) {
                    var raw=await File.ReadAllTextAsync(statusPath,deadline.Token);
                    using var status=JsonDocument.Parse(raw);
                    if(status.RootElement.TryGetProperty("terminalRunId",out var id) && !string.IsNullOrEmpty(id.GetString()) &&
                       status.RootElement.TryGetProperty("transportEndpoint",out var endpoint) && endpoint.ValueKind==JsonValueKind.Object) {
                        var record=GmSessionRunRecordCodec.Decode(GmSessionRunPersistence.Read(root) ?? throw new IOException("Missing published main record."));
                        if(record.Disposition!=GmSessionRunDisposition.Running || record.Identity.RunId!=id.GetString())throw new IOException("Original Running publication mismatch.");
                        original=record;capturedStatus=raw;evidence["OriginalRunning"]=record;evidence["Transport"]=endpoint.Clone();
                    }
                }
                if(original==null)await Task.Delay(10,deadline.Token);
            }
            await using(var fresh=await StartAsync(folder,fresh:true))await fresh.CompleteRemoteAsync(evidence);
            evidence["FreshControlPassedBeforeWarm"]=true;
            await warm.CompleteRemoteAsync(evidence);
            exit=0;
        } catch(Exception failure) {evidence["Failure"]=failure.ToString();}
        finally {
            if(warm!=null)try{await warm.DisposeAsync();}catch(Exception failure){evidence["ConsumerCleanupFailure"]=failure.ToString();exit=1;}
            if(original!=null && capturedStatus!=null)try {
                using var cleanup=new CancellationTokenSource(TimeSpan.FromSeconds(8));
                var text=await BridgeTransportConnector.RequestAsync(capturedStatus,JsonSerializer.Serialize(new{
                    command="shutdown",rootKey=original.Identity.RootKey,expectedMainIdentity=original.Identity}),8000,cleanup.Token);
                using var ack=JsonDocument.Parse(text);
                if(!ack.RootElement.GetProperty("ok").GetBoolean() ||
                   ack.RootElement.GetProperty("status").GetProperty("terminalRunId").GetString()!=original.Identity.RunId)
                    throw new IOException("Original shutdown ACK mismatch.");
                evidence["OriginalShutdownAck"]=ack.RootElement.Clone();
                var hostExit=await running!.WaitAsync(cleanup.Token);evidence["HostExit"]=hostExit;
                var stopped=GmSessionRunRecordCodec.Decode(GmSessionRunPersistence.Read(root) ?? throw new IOException("Missing stopped record."));
                if(hostExit!=0 || stopped.Identity!=original.Identity || stopped.Disposition!=GmSessionRunDisposition.Stopped)
                    throw new IOException("Original main did not stop cleanly.");
                evidence["OriginalStopped"]=stopped;evidence["ScopedRetired"]=true;
            } catch(Exception failure){evidence["CleanupFailure"]=failure.ToString();exit=1;}
            if(host!=null)try{host.Dispose();}catch(Exception failure){evidence["HostDisposeFailure"]=failure.ToString();exit=1;}
            if(running!=null)try{await running.WaitAsync(TimeSpan.FromSeconds(5));}catch(Exception failure){evidence["HostJoinFailure"]=failure.ToString();exit=1;}
            evidence["Success"]=exit==0;
            await File.WriteAllTextAsync(Path.Combine(folder,"scenario.json"),JsonSerializer.Serialize(evidence));
        }
        return exit;
    }
}
