using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmRuntime;

namespace BookOfEternityClient.Tests;

internal static class GmDaemonCurrentScenario
{
    internal const string Manifest="game_state/control/pending_turn_snapshot.json";
    internal const string Snapshot="game_state/control/pending_turn_snapshot/output/daemon-declared.json";
    internal const string Notes="game_state/control/gm_live_test_notes.jsonl";
    internal const string PositiveReady="ready/daemon-positive.json";
    internal const string Ready="ready/turn_complete.json";
    internal const string Repair="game_state/control/validation_repair_request.json";
    internal const string Terminal="game_state/control/terminal_protocol_failure_request.json";
    internal const string Keys="game_state/control/gm_observed_terminal_requests.json";
    internal static readonly byte[] CompetingNote=Encoding.UTF8.GetBytes("{\"requestId\":\"request\",\"recordId\":\"unknown\"}\nmalformed-manual-line\n{\"gmAppend\":true}\n");
    internal static readonly byte[] CompetingReady=Encoding.UTF8.GetBytes("{\"sessionId\":\"replacement\",\"requestId\":\"new\",\"turnNumber\":2}");
    internal static async Task SeedAsync(FileSystemManager files)
    {
        await using(var lease=await files.AcquireCanonicalWriteLeaseAsync())
        {
            foreach(var pair in new Dictionary<string,string>{
                ["input/turn_request.json"]="{\"sessionId\":\"session\",\"requestId\":\"request\",\"turnNumber\":1,\"playerAction\":\"inert\"}",
                [Snapshot]="{\"baseline\":true}",["output/daemon-declared.rollback"]="{\"baseline\":true}",
                [Repair]="{\"sessionId\":\"session\",\"requestId\":\"request\",\"turnNumber\":1}",
                [Terminal]="{\"sessionId\":\"session\",\"requestId\":\"request\",\"turnNumber\":1}",
                [PositiveReady]="{\"positive\":true}",
                [Ready]="{\"sessionId\":\"stale\",\"requestId\":\"stale\",\"turnNumber\":0}",
                [Notes]="{\"requestId\":\"request\",\"recordId\":\"unknown\"}\nmalformed-manual-line\n",
                [Keys]="{\"keys\":[\"saved-key\"]}",
                ["output/daemon-A.json"]="A",["output/daemon-a.json"]="a",
                [@"output/daemon-literal\leaf.json"]="literal",["output/daemon-literal/leaf.json"]="slash",
                ["output/daemon-large.json"]=JsonSerializer.Serialize(new{value=new string('Ж',80000)})
            })await files.WriteFileAtomicAsync(lease,pair.Key,pair.Value);
            var hash=PendingTurnSnapshotAuthority.ComputeSha256(Encoding.UTF8.GetBytes("{\"baseline\":true}"));
            var manifest=new JsonObject{
                ["sessionId"]="session",["requestId"]="request",["turnNumber"]=1,["sourceLabel"]="daemon-current-fixture",
                ["files"]=new JsonObject{["output/daemon-declared.json"]=Snapshot},
                ["snapshotFileHashes"]=new JsonObject{["output/daemon-declared.json"]=hash},
                ["clientOwnedValidationHashes"]=new JsonObject(),
                ["rollbackBackups"]=new JsonObject{["output/daemon-declared.json"]="output/daemon-declared.rollback"},
                ["rollbackBaselineFiles"]=new JsonArray("output/daemon-declared.json"),["manifestPayloadHash"]=""
            };
            manifest["manifestPayloadHash"]=PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);
            await files.WriteFileAtomicAsync(lease,Manifest,manifest.ToJsonString());
        }
        // Existing test authority calls the production portable envelope creator
        // over these actual bytes; it does not forge an accepted reader receipt.
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(files);
    }

    internal static Action<string,string> CreateHook(FileSystemManager files,string folder,Dictionary<string,object?> report)
    {
        var modeFile=Path.Combine(folder,"current-mode.json");
        var mode=File.Exists(modeFile)?JsonSerializer.Deserialize<string>(File.ReadAllText(modeFile)):null;
        var count=0;var manifestReads=0;var targetReads=0;var initialTurnCut=false;
        return (phase,path)=>{
            if(mode==null)return;
            if(mode=="ready-race"&&phase=="member"&&path==files.ResolvePath(PositiveReady)){
                if(report.ContainsKey("PositiveConditionalPublication"))throw new InvalidOperationException("Positive conditional publication repeated.");
                if(File.Exists(path))throw new InvalidOperationException("Positive deletion did not publish absence.");
                report["PositiveConditionalPublication"]=new{Path=path,Index=0,Count=1,Absent=true};return;
            }
            if(mode=="cohort-read-refusal"&&phase=="read"&&path=="input/turn_request.json"&&!initialTurnCut){
                initialTurnCut=true;report["InitialTurnReadRefusal"]=true;
                throw new InvalidDataException("Actual initial admitted turn read refusal.");
            }
            string? target=mode switch {
                "config-refusal"=>"config.json","cache-refusal"=>Keys,"repair-refusal"=>Repair,"terminal-refusal"=>Terminal,
                "post-send-refusal"=>"input/turn_request.json","cohort-read-refusal"=>Snapshot,"notes-race"=>Notes,"ready-race"=>Ready,"cohort-change"=>Manifest,_=>null};
            if(target!=path)return;
            if(mode=="post-send-refusal"&&!File.Exists(Path.Combine(folder,"post-send-armed")))return;
            if(mode=="cache-refusal"&&!File.Exists(Path.Combine(folder,"cache-armed")))return;
            if(mode is "repair-refusal" or "terminal-refusal"){if(phase!="read"||++targetReads!=2)return;}
            if(mode=="cohort-change"){
                if(phase!="read"||++manifestReads!=2)return;
            }else if(phase!=(mode is "notes-race" or "ready-race"?"publish":"read"))return;
            if(Interlocked.Increment(ref count)!=1)throw new InvalidOperationException("Current daemon cut repeated.");
            report["CurrentCut"]=new{Mode=mode,Phase=phase,Path=path,Count=count};
            if(mode=="notes-race")File.WriteAllBytes(files.ResolvePath(path),CompetingNote);
            else if(mode=="ready-race")File.WriteAllBytes(files.ResolvePath(path),CompetingReady);
            else if(mode=="cohort-change")File.AppendAllText(files.ResolvePath(path)," ");
            else throw new InvalidDataException("Actual admitted daemon read fixture refusal.");
        };
    }

    internal static async Task ExerciseAsync(string mode,FileSystemManager files,GmSessionRunCoordinator owner,string folder,Func<Task> stop,Dictionary<string,object?> evidence)
    {
        await owner.RunOperationAsync(async()=>{await SeedAsync(files);return 0;});
        await File.WriteAllTextAsync(Path.Combine(folder,"current-mode.json"),JsonSerializer.Serialize(mode));
        if(mode is "malformed-response" or "request-end-loss"){await stop();evidence["StoppedBeforeLocalTransportAdmission"]=owner.Record.Disposition==GmSessionRunDisposition.Stopped&&!owner.RetainsAuthority;if(!(bool)evidence["StoppedBeforeLocalTransportAdmission"]!)throw new InvalidOperationException("Original main did not retire before local transport admission.");} // Local transport-loss proof: no remote receipt is invented.
        Process? child=null;Task<string>? stdout=null,stderr=null;
        try
        {
            var start=new ProcessStartInfo("pwsh"){UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
            foreach(var arg in new[]{"-NoProfile","-NonInteractive","-File",Path.Combine(TestRepoPaths.RepoRoot,"tests/fixtures/GmDaemonStorage/qte.ps1"),"-RepoRoot",TestRepoPaths.RepoRoot,"-SessionPath",files.GameSessionPath,"-Folder",folder,"-TestSupport",typeof(GmDaemonCurrentScenario).Assembly.Location,"-Mode",mode})start.ArgumentList.Add(arg);
            child=Process.Start(start)??throw new InvalidOperationException("Original current daemon process not started.");
            stdout=child.StandardOutput.ReadToEndAsync();stderr=child.StandardError.ReadToEndAsync();
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));await Task.WhenAll(stdout,stderr);
            evidence["PowerShellExitCode"]=child.ExitCode;evidence["PowerShellStdOut"]=await stdout;evidence["PowerShellStdErr"]=await stderr;
            if(child.ExitCode!=0)throw new InvalidOperationException("Current daemon PowerShell fixture failed.");
            using var ps=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder,"powershell.json")));evidence["PowerShell"]=ps.RootElement.Clone();
            using var helper=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder,"daemon-helper.json")));evidence["Helper"]=helper.RootElement.Clone();
            var p=ps.RootElement;var h=helper.RootElement;
            var localLoss=mode is "malformed-response" or "request-end-loss";
            if(!p.GetProperty("helperExitedBeforeDispose").GetBoolean()||!p.GetProperty("disposed").GetBoolean()||
                p.GetProperty("helperPid").GetInt32()!=h.GetProperty("ProcessId").GetInt32()||p.GetProperty("helperExitCode").GetInt32()!=h.GetProperty("ExitCode").GetInt32())throw new InvalidOperationException("Current daemon original helper join missing.");
            if(localLoss){if(h.GetProperty("ExitCode").GetInt32()!=2||!h.TryGetProperty("Failure",out _)||h.TryGetProperty("Completed",out _))throw new InvalidOperationException("Expected natural local EOF failure missing.");}
            else if(!h.GetProperty("Completed").GetBoolean()||h.GetProperty("ExitCode").GetInt32()!=0)throw new InvalidOperationException("Original successful helper completion missing.");
            if(mode is not ("malformed-response" or "request-end-loss")&&(!p.GetProperty("closeObserved").GetBoolean()||p.GetProperty("lost").GetBoolean()))throw new InvalidOperationException("Current daemon original receipt missing.");
            if(!mode.EndsWith("refusal",StringComparison.Ordinal)&&mode is not ("malformed-response" or "request-end-loss")&&p.GetProperty("failure").ValueKind!=JsonValueKind.Null)throw new InvalidOperationException("Known current row failed unexpectedly.");
            if(mode.EndsWith("refusal",StringComparison.Ordinal)&&(!p.GetProperty("daemonReadRefused").GetBoolean()||p.GetProperty("terminalClose").GetProperty("outcome").GetInt32()!=1||p.GetProperty("terminalClose").GetProperty("closingFailed").GetBoolean()))throw new InvalidOperationException("Read refusal did not preserve Failed/ACK.");
            if(mode is "post-send-refusal" or "config-refusal" or "cache-refusal" or "repair-refusal" or "terminal-refusal" or "cohort-read-refusal" or "cohort-change" or "notes-race" or "ready-race")
                if(h.GetProperty("CurrentCut").GetProperty("Count").GetInt32()!=1)throw new InvalidOperationException("Actual current daemon boundary not reached.");
            if(mode is "malformed-response" or "request-end-loss"){
                if(p.GetProperty("terminalClose").ValueKind!=JsonValueKind.Null||p.GetProperty("closeObserved").GetBoolean()||p.GetProperty("localScopeCompleted").GetBoolean()||p.GetProperty("establishedOutcome").ValueKind!=JsonValueKind.Null||!p.GetProperty("lost").GetBoolean())throw new InvalidOperationException("Lost local read fabricated a completed outcome or remote receipt.");
            }
            if(mode=="cohort-read-refusal"&&!h.GetProperty("InitialTurnReadRefusal").GetBoolean())throw new InvalidOperationException("Initial turn refusal was not reached.");
            if(mode=="notes-race"&&!File.ReadAllBytes(files.ResolvePath(Notes)).SequenceEqual(CompetingNote))throw new InvalidOperationException("Competing GM append was overwritten.");
            if(mode=="ready-race"&&h.GetProperty("PositiveConditionalPublication").GetProperty("Count").GetInt32()!=1)throw new InvalidOperationException("Actual conditional success not reached.");
            if(mode=="ready-race"&&!File.ReadAllBytes(files.ResolvePath(Ready)).SequenceEqual(CompetingReady))throw new InvalidOperationException("Replacement Ready was deleted.");
            if(!p.GetProperty("currentPassed").GetBoolean())throw new InvalidOperationException("Current daemon semantic oracle failed.");
            evidence["CurrentPassed"]=true;
        }
        finally
        {
            var failures=new List<string>();
            try {if(child!=null){try{child.StandardInput.Close();}catch{}if(!child.HasExited){evidence["ForcedPowerShellTermination"]=true;child.Kill();await child.WaitForExitAsync();}if(stdout!=null&&stderr!=null)await Task.WhenAll(stdout,stderr);evidence["PowerShellExited"]=child.HasExited;child.Dispose();}}catch(Exception ex){failures.Add(ex.ToString());}
            try {await stop();evidence["OriginalDaemonOwnerRetired"]=owner.Record.Disposition==GmSessionRunDisposition.Stopped&&!owner.RetainsAuthority;}catch(Exception ex){failures.Add(ex.ToString());}
            if(failures.Count!=0)evidence["DaemonCleanupFailures"]=failures;
        }
    }
}
