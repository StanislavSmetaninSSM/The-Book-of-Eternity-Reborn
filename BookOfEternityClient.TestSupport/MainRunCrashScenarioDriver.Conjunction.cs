using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.Services.GmWorkers;

namespace BookOfEternityClient.Tests;

internal static partial class MainRunCrashScenarioDriver
{
    private static async Task<int> WitnessConjunctionAsync(string kind,string package,string folder,Dictionary<string,object?> result)
    {
        await using var original=Child(Prefix+"owner-stop-ack",package,folder);
        await WaitFileAsync(Path.Combine(folder,"cut.json"),original.Process);await original.KillAsync();
        var info=ReadInfo(folder);Require(ReadRecord(info.Root).Disposition==GmSessionRunDisposition.Stopped,"Conjunction lacks actual original stopped ACK.");
        await using var seed=Child(Prefix+"cold-seed-"+kind,package,folder);
        await WaitFileAsync(Path.Combine(folder,"debt-seed.json"),seed.Process);var before=Snapshot(info.Root);
        await seed.KillAsync();Require(Equal(before,Snapshot(info.Root)),"Controlled seed process death changed retained decision evidence.");
        await using var cold=Child(Prefix+"cold-debt-"+kind,package,folder);await cold.SuccessAsync();
        result["OriginalStoppedApplicationKilled"]=true;result["SeedProcessKilled"]=true;
        result["Seed"]=JsonSerializer.Deserialize<JsonElement>(File.ReadAllBytes(Path.Combine(folder,"debt-seed.json")));
        result["ColdActor"]=JsonSerializer.Deserialize<JsonElement>(File.ReadAllBytes(Path.Combine(folder,"debt.json")));
        result["Success"]=true;return 0;
    }

    private static async Task<int> SeedDebtAsync(string kind,string folder)
    {
        try {
            var info=ReadInfo(folder);var files=Files(info.Root);
            Require(ReadRecord(info.Root).Disposition==GmSessionRunDisposition.Stopped,"Seed lacks original terminal retirement.");
            if(kind=="worker") {
                await using var ledger=await GmWorkerRunLedger.OpenCoordinatorAsync(new WorkerLedgerTarget(info.Root));
                Require(ledger!=null && await ledger.InitializeAsync()==WorkerLedgerMutationKind.Applied,"Preparation: actual worker ledger unavailable.");
                var workspace=Path.Combine(folder,"worker-workspace");Directory.CreateDirectory(workspace);
                var prepared=await ledger!.PrepareAsync(new WorkerRunPreparation(Guid.NewGuid().ToString("N"),"worker","task",new string('a',64),
                    WorkerRunBackend.LinuxNativeLineage,WorkerRunScope.OrdinarySamePidNamespace,workspace),ledger.Sequence);
                Require(prepared.Kind==WorkerLedgerMutationKind.Applied && await ledger.MarkUncertainAsync(prepared.Entry!,ledger.Sequence)==WorkerLedgerMutationKind.Applied,
                    "Preparation: worker inventory decision not durable.");
                WriteJson(Path.Combine(folder,"debt-seed.json"),new{Kind=kind,Pid=Environment.ProcessId,ActualDurableInventory=true,MetadataOnlyReuse=true,NoWorkerProcessQualification=true});
                HoldSeed();return 65;
            }
            await using var lease=await files.AcquireCanonicalWriteLeaseAsync();
            var marker=files.ResolvePath(ReplacementMarker);var before=File.ReadAllBytes(marker);
            var publication=new TrustedLocalFilePublication(files,new TrustedLocalFileScope([files.BasePath]));
            var outcome=publication.PublishWithOutcome(lease,TrustedLocalGeneration.Existing(info.Generation),[new(marker,before,Encoding.UTF8.GetBytes("attempted publication"))],
                (phase,_)=>{if(phase==TrustedLocalPublicationPhase.IntentPublished){File.WriteAllBytes(marker,Encoding.UTF8.GetBytes("unresolved storage third value 🌌"));throw new IOException("Controlled retained storage conflict seed.");}});
            Require(kind=="storage" && outcome.Disposition==TrustedLocalPublicationDisposition.Uncertain && File.Exists(Path.Combine(files.RuntimeRootPath,"trusted-local-publication-v1/active.json")),
                "Preparation: actual storage decision not retained uncertain.");
            WriteJson(Path.Combine(folder,"debt-seed.json"),new{Kind=kind,Pid=Environment.ProcessId,Disposition=outcome.Disposition.ToString(),ActualPublisher=true,OriginalLeaseHeld=true,Failure=outcome.Failure?.ToString()});
            HoldSeed();return 65;
        } catch(Exception failure){WriteJson(Path.Combine(folder,"seed-failure.json"),new{Failure=failure.ToString()});return 1;}
    }
    private static void HoldSeed()
    {using var bounded=new ManualResetEventSlim();if(!bounded.Wait(TimeSpan.FromSeconds(6)))throw new TimeoutException("Witness did not kill exact seed process.");}
}
