using System.Diagnostics;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

// Test-only same-process consumer across local admission and another process's
// original main. No game action, supplied authority, reflection or recovery bypass.
internal sealed class GmLocalRemoteConsumerScenario : IAsyncDisposable
{
    private readonly Process child;
    private readonly Task<string> errors;
    private readonly string folder;
    private readonly bool fresh;
    private GmLocalRemoteConsumerScenario(Process child,string folder,bool fresh)
    { this.child=child;this.folder=folder;this.fresh=fresh;errors=child.StandardError.ReadToEndAsync(); }
    internal static async Task<GmLocalRemoteConsumerScenario> StartAsync(string folder,bool fresh=false)
    {
        var start=new ProcessStartInfo("dotnet") {UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var arg in new[]{Path.Combine(folder,"ship/BookOfEternityClient.TestSupport/BookOfEternityClient.TestSupport.dll"),"local-remote-consumer",Path.Combine(folder,"root"),fresh?"fresh":"warm"})start.ArgumentList.Add(arg);
        var peer=new GmLocalRemoteConsumerScenario(Process.Start(start)!,folder,fresh);
        try {
            var ready=await peer.child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(6));
            if(ready!=(fresh?"FRESH_READY":"LOCAL_CLOSED"))throw new InvalidOperationException("Local admission premise failed: "+ready);
            return peer;
        } catch {await peer.DisposeAsync();throw;}
    }
    internal async Task CompleteRemoteAsync(Dictionary<string,object?> evidence)
    {
        await child.StandardInput.WriteLineAsync("REMOTE");await child.StandardInput.FlushAsync();child.StandardInput.Close();
        var output=child.StandardOutput.ReadToEndAsync();
        await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(8));
        var text=await output;var error=await errors;
        await File.WriteAllTextAsync(Path.Combine(folder,fresh?"fresh-remote-consumer.log":"local-remote-consumer.log"),text+"\n"+error);
        if(child.ExitCode!=0)throw new InvalidOperationException("Same client local-to-remote admission failed: "+error);
        using var result=JsonDocument.Parse(text);
        if(!result.RootElement.GetProperty("remote").GetBoolean() || !result.RootElement.GetProperty("closed").GetBoolean())
            throw new InvalidOperationException("Original remote close was not observed.");
        evidence[fresh?"FreshClientRemoteControl":"SameClientLocalThenRemote"]=result.RootElement.Clone();
    }
    public async ValueTask DisposeAsync()
    {
        child.StandardInput.Close();
        if(!child.HasExited){child.Kill(true);await child.WaitForExitAsync();}
        await errors;child.Dispose();
    }
    internal static async Task<int> RunChildAsync(string root,bool fresh)
    {
        try {
            var files=new FileSystemManager(root,NullLogger<FileSystemManager>.Instance);
            if(!fresh) {
            // Initial test setup only: existing durable inventory with no tasks.
            await using(var inventory=await GmWorkerRunLedger.OpenCoordinatorAsync(new(root))) {
                if(inventory==null || await inventory.InitializeAsync() is not (WorkerLedgerMutationKind.Applied or WorkerLedgerMutationKind.AlreadyExact))
                    throw new InvalidOperationException("Fresh empty worker inventory unavailable.");
            }
            bool localRemote=true;
            await SessionOperationContext.RunParticipatingCurrentSessionAsync(files,()=>Task.FromResult(true),
                observeOriginalClose:(_,_,remote)=>localRemote=remote);
            if(localRemote)throw new InvalidOperationException("Premise was not local admission.");
            var retired=files.CanonicalRootAuthorityIdentity.WorkerContext ?? throw new InvalidOperationException("Local inventory was not attached.");
            try {retired.RequireOpen();throw new IOException("Local inventory did not retire.");}
            catch(InvalidOperationException) { }
            }
            Console.WriteLine(fresh?"FRESH_READY":"LOCAL_CLOSED");Console.Out.Flush();
            if(await Console.In.ReadLineAsync()!="REMOTE")throw new InvalidOperationException("Missing explicit fixture continuation.");
            var configBefore=File.ReadAllBytes(files.ResolvePath("config.json"));
            var generationBefore=File.ReadAllBytes(files.SessionGenerationPath);
            bool remoteObserved=false,closed=false;MainOperationClose? close=null;
            await SessionOperationContext.RunParticipatingCurrentSessionAsync(files,()=>Task.FromResult(true),
                observeOriginalClose:(reply,observed,remote)=>{close=reply;closed=observed;remoteObserved=remote;});
            if(!configBefore.AsSpan().SequenceEqual(File.ReadAllBytes(files.ResolvePath("config.json"))) ||
               !generationBefore.AsSpan().SequenceEqual(File.ReadAllBytes(files.SessionGenerationPath)))
                throw new InvalidOperationException("Readmission changed canonical config or generation.");
            Console.WriteLine(JsonSerializer.Serialize(new{remote=remoteObserved,closed,close,configAndGenerationUnchanged=true}));
            return 0;
        } catch(Exception failure) {Console.Error.WriteLine(failure.ToString());return 2;}
    }
}
