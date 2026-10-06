using System.Text;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

internal static partial class MainRunCrashScenarioDriver
{
    private const string ReplacementMarker="game_state/world/test_fixture_state.json";
    private static async Task<int> WitnessReplacementAsync(string decision,string package,string folder,Dictionary<string,object?> result)
    {
        await using var original=Child(Prefix+"owner-stop-ack",package,folder);
        await WaitFileAsync(Path.Combine(folder,"cut.json"),original.Process);
        var info=ReadInfo(folder);Require(ReadRecord(info.Root).Disposition==GmSessionRunDisposition.Stopped,"Replacement lacks actual stopped original.");
        await original.KillAsync();result["OriginalStoppedApplicationKilled"]=true;
        await using var actor=Child(Prefix+"cold-replace-"+decision,package,folder);await actor.SuccessAsync();
        result["Replacement"]=JsonSerializer.Deserialize<JsonElement>(File.ReadAllBytes(Path.Combine(folder,"replacement.json")));
        if(decision=="uncertain") {
            await using var cold=Child(Prefix+"cold-debt-storage",package,folder);await cold.SuccessAsync();
            result["FurtherColdRefusal"]=JsonSerializer.Deserialize<JsonElement>(File.ReadAllBytes(Path.Combine(folder,"debt.json")));
        } else {
            await using var fresh=Child(Prefix+"cold-fresh",package,folder);await fresh.SuccessAsync();
            result["FreshEpoch"]=JsonSerializer.Deserialize<JsonElement>(File.ReadAllBytes(Path.Combine(folder,"cold.json")));
        }
        result["Success"]=true;return 0;
    }

    private static async Task<int> ReplacementAsync(string decision,string folder)
    {
        var result=new Dictionary<string,object?>{["Decision"]=decision,["Pid"]=Environment.ProcessId};
        try {
            var info=ReadInfo(folder);var ordinary=Files(info.Root);var recordBytes=File.ReadAllBytes(Path.Combine(info.Root,".boe_runtime/gm-runs/main.json"));
            Require(ReadRecord(info.Root).Disposition==GmSessionRunDisposition.Stopped,"New process has no sealed stop.");
            var archived=File.ReadAllBytes(ordinary.ResolvePath(ReplacementMarker));
            await ordinary.WriteFileAtomicAsync(ReplacementMarker,"changed before new-process Load 🌌");
            await ordinary.WriteFileAtomicAsync("lore/f3-absent-after-load.txt","not in archive");
            var before=CanonicalSnapshot(info.Root);var archiveHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(info.Archive)));
            var generation=File.ReadAllBytes(ordinary.SessionGenerationPath);var cuts=0;var replacing=false;
            FileSystemManager? files=null;
            files=new(info.Root,NullLogger<FileSystemManager>.Instance,PhysicalLoadTransactionOperations.Instance,new FileSystemManagerHooks {
                LocalPublicationObserver=(phase,_)=>{
                    if(phase==TrustedLocalPublicationPhase.IntentPublished) {
                        using var journal=File.OpenRead(Path.Combine(files!.RuntimeRootPath,"trusted-local-publication-v1/active.json"));
                        Span<byte> magic=stackalloc byte[8];journal.ReadExactly(magic);replacing=magic.SequenceEqual("BOELP3\r\n"u8);
                    }
                    if(!replacing || phase!=TrustedLocalPublicationPhase.CommitStaged || decision=="committed")return;
                    cuts++;if(decision=="uncertain")File.WriteAllBytes(files!.ResolvePath(ReplacementMarker),Encoding.UTF8.GetBytes("controlled third value 🌌"));
                    throw new InvalidOperationException("Controlled actual new-process namespace Load cut.");
                }
            });
            var state=new StateManager(files,new GameSettings(),NullLogger<StateManager>.Instance);
            var load=await new SaveLoadService(files,state,NullLogger<SaveLoadService>.Instance).LoadGameWithOutcomeAsync(info.Archive);
            var expected=decision=="committed"?LoadReplacementDisposition.Committed:decision=="rollback"?LoadReplacementDisposition.RolledBack:LoadReplacementDisposition.Uncertain;
            Require(load.Disposition==expected && load.SelectedSourcePath==info.Archive,"Actual Load typed decision/source differs: "+load);
            Require(recordBytes.SequenceEqual(File.ReadAllBytes(Path.Combine(info.Root,".boe_runtime/gm-runs/main.json"))),"Load replaced original main stop evidence.");
            Require(archiveHash==Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(info.Archive))),"Load changed its source archive.");
            if(decision=="committed") {
                Require(load.EstablishedGeneration!=info.Generation && load.EstablishedGeneration==CurrentGeneration(files) && !load.ContinuationBlocked &&
                    archived.SequenceEqual(File.ReadAllBytes(files.ResolvePath(ReplacementMarker))) && !File.Exists(files.ResolvePath("lore/f3-absent-after-load.txt")),"Committed exact bytes/absence/new generation not established.");
                var expectedCanonical=new Dictionary<string,string>(before,StringComparer.Ordinal);
                expectedCanonical.Remove("game_session/lore/f3-absent-after-load.txt");
                expectedCanonical["game_session/"+ReplacementMarker]=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(archived));
                using(var archive=System.IO.Compression.ZipFile.OpenRead(info.Archive))
                using(var metadata=archive.GetEntry("save_metadata.json")!.Open())
                    expectedCanonical["game_session/save_metadata.json"]=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(metadata));
                Require(Equal(expectedCanonical,CanonicalSnapshot(info.Root)),"Committed Load changed unexpected canonical members.");
            } else if(decision=="rollback") {
                Require(cuts==1 && load.EstablishedGeneration==info.Generation && !load.ContinuationBlocked && generation.SequenceEqual(File.ReadAllBytes(files.SessionGenerationPath)) &&
                    Equal(before,CanonicalSnapshot(info.Root)),"RolledBack did not restore complete exact canonical before namespace/generation.");
            } else {
                Require(cuts==1 && load.EstablishedGeneration==null && load.NeedsFollowUp && load.ContinuationBlocked &&
                    File.ReadAllText(files.ResolvePath(ReplacementMarker))=="controlled third value 🌌" &&
                    File.Exists(Path.Combine(files.RuntimeRootPath,"trusted-local-publication-v1/active.json")),"Uncertain lost conflict/debt or admitted continuation.");
            }
            result["BeforeCanonical"]=before;result["AfterCanonical"]=CanonicalSnapshot(info.Root);result["BeforeGenerationSha256"]=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(generation));
            result["AfterGenerationSha256"]=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(files.SessionGenerationPath)));
            result["Load"]=DescribeLoad(load);result["ActualPublicationCuts"]=cuts;result["SourceArchiveUnchanged"]=true;result["MainStoppedUnchanged"]=true;result["Success"]=true;return 0;
        } catch(Exception failure){result["Failure"]=failure.ToString();return 1;}
        finally{WriteJson(Path.Combine(folder,"replacement.json"),result);}
    }

    private static string CurrentGeneration(FileSystemManager files)
    {return System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllBytes(files.SessionGenerationPath))!.AsObject().Single(p=>p.Key.Equals("generationId",StringComparison.OrdinalIgnoreCase)).Value!.GetValue<string>();}
    private static Dictionary<string,string> CanonicalSnapshot(string root)=>Snapshot(root).Where(p=>p.Key.StartsWith("game_session/",StringComparison.Ordinal) &&
        !p.Key.StartsWith("game_session/saves/",StringComparison.Ordinal) && !p.Key.StartsWith("game_session/rollback/",StringComparison.Ordinal)).ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal);

    private static async Task<int> DebtAsync(string kind,string folder)
    {
        var result=new Dictionary<string,object?>{["Kind"]=kind,["Pid"]=Environment.ProcessId};GmWorkerRootContext? workers=null;
        try {
            var info=ReadInfo(folder);var files=Files(info.Root);var before=Snapshot(info.Root);
            Require(ReadRecord(info.Root).Disposition==GmSessionRunDisposition.Stopped,"Independent debt probe lacks real main stop.");
            if(kind=="worker") {
                var inventory=await GmWorkerRunLedger.ObserveAsync(new WorkerLedgerTarget(info.Root));
                Require(inventory.Kind==WorkerRunObservationKind.Uncertain && inventory.Entries.Count==1,"Actual durable worker inventory not unresolved.");
                workers=GmWorkerRootContext.Attach(files,true,null);result["WorkerObservation"]=inventory;
            }
            async Task Refuse(Func<Task> action,string label) {
                Exception? failure=null;try{await action();}catch(Exception error){failure=error;}
                Require(failure!=null,"Valid main Stopped bypassed "+kind+" for "+label);result[label]=failure!.ToString();
            }
            await Refuse(async()=>{await using var lease=await files.AcquireCanonicalWriteLeaseAsync();},"CanonicalLease");
            await Refuse(()=>files.ClearGameStateAsync(),"Clear");
            Require(Equal(before,Snapshot(info.Root)),"Canonical/Clear refusal changed pre-existing authority evidence.");
            var state=new StateManager(files,new GameSettings(),NullLogger<StateManager>.Instance);string? privateStage=null;
            var load=await new SaveLoadService(files,state,NullLogger<SaveLoadService>.Instance,new SaveLoadServiceHooks {
                AfterLoadArchiveExtractedAsync=path=>{privateStage=path;return Task.CompletedTask;}
            }).LoadGameWithOutcomeAsync(info.Archive);
            Require(load.Disposition==(kind=="worker"?LoadReplacementDisposition.NotLoaded:LoadReplacementDisposition.Uncertain),"Independent debt lost typed Load decision.");
            if(kind=="storage")Require(load.EstablishedGeneration==null && load.ContinuationBlocked,"Storage conflict admitted continuation.");
            var afterLoad=Snapshot(info.Root);
            var held=false;var prepared=false;
            var owner=await GmSessionRunCoordinator.OpenNeutralAsync(files,stage=>{if(stage==MainRunIoStage.Readback)prepared=true;});
            await Refuse(async()=>{_ =await owner.LaunchNeutralAsync(NeutralTerminalLaunch.CreateForFixtureRoot(folder,folder,info.Root),CancellationToken.None,_=>held=true);},"NewLaunch");
            result["Before"]=before;result["After"]=Snapshot(info.Root);result["PreparedObserved"]=prepared;result["HeldObserved"]=held;result["OwnerRetainsAuthority"]=owner.RetainsAuthority;
            var after=Snapshot(info.Root);var stagePrefix=privateStage==null?null:Path.GetRelativePath(info.Root,privateStage)+"/";
            Require(privateStage!=null && Path.GetDirectoryName(privateStage)==Path.Combine(files.RuntimeRootPath,"load-staging") && Guid.TryParseExact(Path.GetFileName(privateStage),"N",out _),"Load preparation escaped its actual private scratch.");
            var authorityAfter=after.Where(p=>!p.Key.StartsWith(stagePrefix!,StringComparison.Ordinal)).ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal);
            Require(!held && !prepared && !owner.RetainsAuthority && Equal(before,authorityAfter) && Equal(afterLoad,after),"Independent debt allowed launch/recovery authority side effects before admission.");
            if(kind=="worker")Require(!Directory.Exists(privateStage) && Equal(before,afterLoad),"Worker NotLoaded retained or changed preparation.");
            else {
                var candidate=Directory.EnumerateFiles(privateStage!,"*",SearchOption.AllDirectories).ToDictionary(p=>Path.GetRelativePath(privateStage!,p),p=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(p))),StringComparer.Ordinal);
                using var archive=System.IO.Compression.ZipFile.OpenRead(info.Archive);
                var expected=new Dictionary<string,string>(StringComparer.Ordinal);
                foreach(var entry in archive.Entries.Where(e=>!e.FullName.EndsWith('/') && e.FullName!="save_manifest.json")) {
                    using var stream=entry.Open();expected.Add(entry.FullName,Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)));
                }
                Require(Equal(expected,candidate),"Retained private preparation is not exact selected fixed archive images.");
                result["ExactPrivateArchiveImages"]=candidate;
            }
            result["ActualPrivateLoadPreparation"]=privateStage;result["PrivatePreparationRetained"]=Directory.Exists(privateStage);result["AuthorityEvidenceUnchanged"]=true;
            result["Load"]=DescribeLoad(load);result["Before"]=before;result["After"]=Snapshot(info.Root);result["NoPreparedOrCreation"]=true;result["Success"]=true;return 0;
        } catch(Exception failure){result["Failure"]=failure.ToString();return 1;}
        finally{workers?.ReleaseClient();WriteJson(Path.Combine(folder,"debt.json"),result);}
    }
}
