using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

internal static partial class MainRunCrashScenarioDriver
{
    private static async Task<int> WitnessPinAsync(string cut,string package,string folder,Dictionary<string,object?> result)
    {
        await using var owner=Child(Prefix+"owner-pin-"+cut,package,folder);
        await WaitFileAsync(Path.Combine(folder,"host-ready.json"),owner.Process);
        await using var client=Child(Prefix+"client-"+cut,package,folder);
        var boundary=cut=="receipt"?"receipt.json":cut=="reply"?"client-reply.json":"client-before-close.json";
        await WaitFileAsync(Path.Combine(folder,boundary),client.Process);
        await client.KillAsync();result["OriginalClientKilled"]=true;
        File.WriteAllText(Path.Combine(folder,"reply-release"),"continue original handler only");
        File.WriteAllText(Path.Combine(folder,"stop-request"),"stop original scope");
        await WaitFileAsync(Path.Combine(folder,"pin-settled.json"),owner.Process);
        var settled=JsonSerializer.Deserialize<JsonElement>(File.ReadAllBytes(Path.Combine(folder,"pin-settled.json")));
        Require(settled.GetProperty("State").GetString()==(cut=="before"?"Unresolved":"ClosedObserved"),"Client death changed original receipt retirement.");
        Require(settled.GetProperty("LogicalUncertain").GetBoolean()==(cut=="before"),"Client death uncertainty differs from receipt boundary.");
        Require(settled.GetProperty("PhysicalScopedEmpty").GetBoolean(),"Original scope physical cleanup did not complete.");
        result["OriginalPinSettlement"]=settled;var info=ReadInfo(folder);var before=Snapshot(info.Root);
        await owner.KillAsync();result["OriginalApplicationKilled"]=true;
        await using var cold=Child(Prefix+"cold-"+(cut=="before"?"refuse":"fresh"),package,folder);
        await cold.SuccessAsync();
        Require(File.ReadAllText(Path.Combine(folder,"callback-count"))=="1","Original callback was replayed.");
        Require(File.ReadAllText(Path.Combine(info.Root,"game_session/game_state/world/f3-client.txt"))=="executed once 🌌","Established original canonical effect was lost/replayed.");
        if(cut=="before")Require(Equal(before,Snapshot(info.Root)),"Unresolved pin death changed retained evidence/canonical bytes.");
        result["ColdActor"]=JsonSerializer.Deserialize<JsonElement>(File.ReadAllBytes(Path.Combine(folder,"cold.json")));
        result["CallbackExecutions"]=1;result["Success"]=true;return 0;
    }

    private static async Task<int> OwnerPinAsync(string cut,string package,string folder)
    {
        try {
            var launch=NeutralTerminalLaunch.Create(package,folder);var root=Directory.GetParent(launch.Scratch)!.FullName;
            var files=Files(root);var state=PortableSaveFixture.Seed(files);
            Require(await new SaveLoadService(files,state,NullLogger<SaveLoadService>.Instance).SaveGameAsync("pin-source","isolated synthetic pin crash"),"Preparation: archive creation failed.");
            var archive=Directory.GetFiles(files.ResolvePath("saves/manual_saves"),"*.zip").Single();
            using(var generation=JsonDocument.Parse(File.ReadAllBytes(files.SessionGenerationPath)))WriteJson(Path.Combine(folder,"root.json"),new FixtureInfo(root,archive,generation.RootElement.GetProperty("generationId").GetString()!));
            using var output=new FileStream(Path.Combine(folder,"owner-output.bin"),FileMode.Create,FileAccess.Write,FileShare.ReadWrite,4096,FileOptions.Asynchronous);
            var host=new FixtureHost(launch,output);MainOperationClose? receipt=null;
            host.Set("BeforeMainCloseReply",(Func<MainOperationClose,Task>)(async close=>{
                receipt=close;WriteJson(Path.Combine(folder,"receipt.json"),close);
                if(cut=="receipt")await WaitFileAsync(Path.Combine(folder,"reply-release"));
            }));
            await host.CallAsync("StartShellAsync");using var serverLifetime=new CancellationTokenSource();var server=host.StartServer(serverLifetime.Token);
            var original=host.Owner;var terminal=host.Terminal;
            WriteJson(Path.Combine(folder,"host-ready.json"),new{original.Identity});
            await WaitFileAsync(Path.Combine(folder,"stop-request"));
            var close=JsonSerializer.Deserialize<MainOperationClose>(File.ReadAllBytes(Path.Combine(folder,"client-before-close.json")))!;
            if(cut=="before") {
                await WaitAsync(()=>original.IsUncertain);
                _=host.CallAsync("StopShellAsync");
                await WaitAsync(()=>ReadRecord(root).Disposition==GmSessionRunDisposition.Uncertain);
                // Independent original physical cleanup does not retire logical pin/owner.
                var proof=await terminal.StopAndObserveAsync(CancellationToken.None);
                Require(proof.CleanupComplete && terminal.RootExited.IsCompletedSuccessfully,"Original pin-loss cleanup failed.");
                var query=original.QueryRemoteOperation(close);
                Require(original.RetainsAuthority && query.State==MainOperationState.Unresolved,"Missing receipt retired unresolved pin.");
                WriteJson(Path.Combine(folder,"pin-settled.json"),new{State=query.State.ToString(),LogicalUncertain=original.IsUncertain,
                    PhysicalScopedEmpty=proof.CleanupComplete,OriginalRetained=original.RetainsAuthority,Close=close,Record=ReadRecord(root)});
            } else {
                Require(receipt==close && receipt.Outcome==MainOperationOutcome.Completed && !receipt.ClosingFailed,"Original immutable completion was not received.");
                await host.CallAsync("StopShellAsync");
                var query=original.QueryRemoteOperation(close);
                Require(query.State==MainOperationState.ClosedObserved && !original.IsUncertain && ReadRecord(root).Disposition==GmSessionRunDisposition.Stopped,"Observed receipt did not permit confirmed original stop.");
                WriteJson(Path.Combine(folder,"pin-settled.json"),new{State=query.State.ToString(),LogicalUncertain=original.IsUncertain,
                    PhysicalScopedEmpty=terminal.RootExited.IsCompletedSuccessfully,OriginalRetained=original.RetainsAuthority,Close=close,Record=ReadRecord(root)});
            }
            using var bounded=new ManualResetEventSlim();bounded.Wait(TimeSpan.FromSeconds(6));return 65;
        } catch(Exception failure){WriteJson(Path.Combine(folder,"owner-failure.json"),new{Failure=failure.ToString()});return 1;}
    }

    private static async Task<int> ClientPinAsync(string cut,string folder)
    {
        try {
            var info=ReadInfo(folder);var files=Files(info.Root);
            var observed=(AsyncLocal<FileSystemManager.MainAdmission>)typeof(FileSystemManager).GetField("MainAdmissions",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
            MainOperationClose? close=null;
            files=new FileSystemManager(info.Root,NullLogger<FileSystemManager>.Instance,PhysicalLoadTransactionOperations.Instance,new FileSystemManagerHooks {
                SessionOperationClosingAsync=async()=>{
                    var original=observed.Value;while(original!=null&&!original.OwnsRemote)original=original.Parent;
                    close=original?.DescribeClose(MainOperationOutcome.Completed,false)??throw new InvalidOperationException("Actual original connection not present at finalization.");
                    WriteJson(Path.Combine(folder,"client-before-close.json"),close);
                    if(cut=="before")await WaitFileAsync(Path.Combine(folder,"client-release"));
                }});
            var value=await SessionOperationContext.RunBoundAsync(files,info.Generation,async()=>{
                Require(!File.Exists(Path.Combine(folder,"callback-count")),"Original operation executed twice.");
                await files.WriteFileAtomicAsync("game_state/world/f3-client.txt","executed once 🌌");
                File.WriteAllText(Path.Combine(folder,"callback-count"),"1");return 42;
            });
            Require(value==42 && close!=null,"Validated original completion lost result/identity.");
            WriteJson(Path.Combine(folder,"client-reply.json"),new{Value=value,Close=close,ValidatedReply=true});
            using var bounded=new ManualResetEventSlim();bounded.Wait(TimeSpan.FromSeconds(6));return 65;
        } catch(Exception failure){WriteJson(Path.Combine(folder,"client-failure.json"),new{Failure=failure.ToString()});return 1;}
    }

    private static async Task<int> FreshAsync(string folder)
    {
        var result=new Dictionary<string,object?>{["Mode"]="fresh",["Pid"]=Environment.ProcessId};
        try {
            var info=ReadInfo(folder);var old=ReadRecord(info.Root);Require(old.Disposition==GmSessionRunDisposition.Stopped && old.StopEvidence!=null,"Fresh actor lacks sealed original stop.");
            var launch=NeutralTerminalLaunch.CreateForFixtureRoot(folder,folder,info.Root);
            using var output=new FileStream(Path.Combine(folder,"fresh-output.bin"),FileMode.Create,FileAccess.Write,FileShare.ReadWrite,4096,FileOptions.Asynchronous);
            var host=new FixtureHost(launch,output);await host.CallAsync("StartShellAsync");var fresh=ReadRecord(info.Root);
            await WaitAsync(()=>ReadOutput(folder,"fresh-output.bin").Contains("TTY_READY owned=1",StringComparison.Ordinal));
            Require(fresh.Identity.Epoch==old.Identity.Epoch+1 && fresh.Identity.RunId!=old.Identity.RunId && fresh.Identity.HostInstanceId!=old.Identity.HostInstanceId &&
                fresh.Identity.RootKey==old.Identity.RootKey && host.Terminal.Identity.RunId==fresh.Identity.RunId,"Fresh process reused/restored old original identity.");
            Require(!ReadOutput(folder,"fresh-output.bin").Contains("RESULT",StringComparison.Ordinal),"Fresh process replayed old input.");
            await host.CallAsync("StopShellAsync");Require(ReadRecord(info.Root).Disposition==GmSessionRunDisposition.Stopped,"Fresh original stop did not ACK.");
            result["Old"]=old;result["Fresh"]=fresh;result["Final"]=ReadRecord(info.Root);result["NoOldInputReplay"]=true;result["Success"]=true;return 0;
        } catch(Exception failure){result["Failure"]=failure.ToString();return 1;}
        finally{WriteJson(Path.Combine(folder,"cold.json"),result);}
    }

    internal static void CleanupAfterGuardian(string folder)
    {
        var report=Path.Combine(folder,"guardian.json");if(!File.Exists(report))return;
        using var guardian=JsonDocument.Parse(File.ReadAllBytes(report));
        if(!guardian.RootElement.GetProperty("echild").GetBoolean())return;
        var owned=new HashSet<string>(StringComparer.Ordinal);
        void Scan(JsonElement value) {
            if(value.ValueKind==JsonValueKind.Object)foreach(var property in value.EnumerateObject()) {
                if(property.Name=="RunId" && property.Value.ValueKind==JsonValueKind.String && Guid.TryParseExact(property.Value.GetString(),"N",out _))
                    owned.Add(Path.Combine(Path.GetTempPath(),"boe-native-"+property.Value.GetString()));
                Scan(property.Value);
            } else if(value.ValueKind==JsonValueKind.Array)foreach(var item in value.EnumerateArray())Scan(item);
        }
        // Only driver-owned witnesses carry bootstrap identities. Canonical JSON
        // may contain a BOM; the journal named active.json is a binary codec.
        foreach(var path in Directory.EnumerateFiles(folder,"*.json",SearchOption.TopDirectoryOnly)) {
            using var value=JsonDocument.Parse(File.ReadAllBytes(path));Scan(value.RootElement);
        }
        var removed=new List<string>();foreach(var path in owned)if(Directory.Exists(path)){Directory.Delete(path,true);removed.Add(path);}
        WriteJson(Path.Combine(folder,"fixture-cleanup.json"),new{GuardianEchild=true,OnlyKnownSyntheticBootstrap=true,Removed=removed,
            CanonicalAndLogicalEvidenceRetained=true,NoRemainingKnownBootstrap=owned.All(p=>!Directory.Exists(p))});
    }
}
