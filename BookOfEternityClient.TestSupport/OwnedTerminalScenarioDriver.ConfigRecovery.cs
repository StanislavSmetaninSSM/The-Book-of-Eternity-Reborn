using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

internal static partial class OwnedTerminalScenarioDriver
{
    // Real ordinary config publisher + shipped production Bridge and configured
    // inert CLI. The hook observes original guard contention; it grants no lease.
    private static async Task RunStartupConfigRecoveryAsync(bool unknown,string folder,object host,Type type,
        Dictionary<string,object?> evidence)
    {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        object? Field(string name)=>type.GetField(name,flags)!.GetValue(host);
        object? Invoke(string name)=>type.GetMethod(name,flags)!.Invoke(host,null);
        var root=Path.Combine(folder,"root");
        var configPath=Path.Combine(root,"game_session/config.json");
        var journalPath=Path.Combine(root,".boe_runtime/trusted-local-publication-v1/active.json");
        var recordPath=Path.Combine(root,".boe_runtime/gm-runs/main.json");
        var before=File.ReadAllBytes(configPath);
        var expected=JsonSerializer.Deserialize<GameSettings>(before,new JsonSerializerOptions {PropertyNameCaseInsensitive=true})!;
        Require(expected.GmCliLaunchCommand.Contains("gm-model-sentinel",StringComparison.Ordinal),"Fixture A is not the explicit inert configured CLI.");
        var transient=JsonNode.Parse(before)!;
        transient["GmCliLaunchCommand"]=expected.GmCliLaunchCommand.Replace("gm-model-sentinel","transient-model-sentinel",StringComparison.Ordinal);
        var transientCwd=Path.Combine(root,"transient-work");Directory.CreateDirectory(transientCwd);
        transient["GmBridgeShellWorkingDirectory"]=transientCwd;
        transient["GmCliInputProfile"]!["ObservationTimeoutMilliseconds"]=2700;
        transient["GmWorkerBridgeProfiles"]=JsonSerializer.SerializeToNode(new[]{GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with {LaunchCommand="exit 74"}});
        var after=JsonSerializer.SerializeToUtf8Bytes(transient);
        var third=transient.DeepClone();
        third["GmCliLaunchCommand"]=expected.GmCliLaunchCommand.Replace("gm-model-sentinel","unknown-model-sentinel",StringComparison.Ordinal);
        var unknownBytes=JsonSerializer.SerializeToUtf8Bytes(third);
        var published=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var contended=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release=new ManualResetEventSlim();
        var cuts=0;var guardWaits=0;var metadataStages=0;byte[]? selectedJournal=null;
        type.GetField("ObserveMainGuardContention",flags)!.SetValue(host,(Func<Task>)(()=>{
            Interlocked.Increment(ref guardWaits);contended.TrySetResult();return Task.CompletedTask;
        }));
        type.GetField("ObserveMainMetadata",flags)!.SetValue(host,(Action<MainRunIoStage>)(_=>Interlocked.Increment(ref metadataStages)));
        var files=new FileSystemManager(root,NullLogger<FileSystemManager>.Instance,PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks {LocalPublicationObserver=(phase,index)=>{
                if(phase!=TrustedLocalPublicationPhase.MemberPublished)return;
                using var parsed=JsonDocument.Parse(File.ReadAllBytes(journalPath));
                var members=parsed.RootElement.GetProperty("Members");
                Require(index==0 && members.GetArrayLength()==1 && members[0].GetProperty("Path").GetString()==configPath,
                    "Config fixture selected another actual journal member.");
                Require(File.ReadAllBytes(configPath).SequenceEqual(after),"Selected config after-image was not physically published.");
                Interlocked.Increment(ref cuts);selectedJournal=File.ReadAllBytes(journalPath);published.TrySetResult();
                Require(release.Wait(TimeSpan.FromSeconds(8)),"Fixture failed to release its actual publisher.");
                if(unknown)File.WriteAllBytes(configPath,unknownBytes);
                throw new InvalidOperationException("controlled original config publication cut");
            }});
        Task<Exception?> writer=Task.Run(async()=>{try{await files.WriteFileAtomicBytesAsync("config.json",after);return null;}catch(Exception failure){return failure;}});
        Task? startup=null;
        try
        {
            await published.Task.WaitAsync(TimeSpan.FromSeconds(4));
            Require(!File.Exists(recordPath) && Field("_mainRun")==null && Field("_pty")==null,"Fixture already had a main owner.");
            startup=(Task)Invoke("StartShellAsync")!;
            await contended.Task.WaitAsync(TimeSpan.FromSeconds(4));
            // Diagnostic only: a fixed implementation may not have cached any
            // settings before recovery, but actual guard contention stays causal.
            evidence["PreSettlementCachedCommand"]=(Field("_productionConfig") as GameSettings)?.GmCliLaunchCommand;
            evidence["GuardContentionBeforeSettlement"]=guardWaits;
            Require(!startup.IsCompleted && !File.Exists(recordPath) && metadataStages==0 && Field("_pty")==null,
                "Original startup escaped the still-owned config writer.");
            release.Set();
            var writeFailure=await writer.WaitAsync(TimeSpan.FromSeconds(4));
            Exception? startupFailure=null;try{await startup.WaitAsync(TimeSpan.FromSeconds(8));}catch(Exception failure){startupFailure=failure;}
            evidence["PublicationCuts"]=cuts;evidence["WriterFailure"]=writeFailure?.ToString();
            evidence["StartupFailure"]=startupFailure?.ToString();evidence["MetadataStages"]=metadataStages;
            evidence["JournalRetained"]=File.Exists(journalPath);evidence["MainRecordPresent"]=File.Exists(recordPath);
            Require(cuts==1 && guardWaits>0 && writeFailure!=null,"Actual publication/guard cut was not reached.");
            if(unknown)
            {
                Require(writeFailure is CoordinatedStatePublicationUncertainException,"Actual unknown config publication lost its typed decision.");
                Require(File.ReadAllBytes(configPath).SequenceEqual(unknownBytes) && File.Exists(journalPath) &&
                    File.ReadAllBytes(journalPath).SequenceEqual(selectedJournal!),"Unknown bytes or original journal were changed.");
                Require(startupFailure!=null && Field("_mainRun")==null && Field("_pty")==null && !File.Exists(recordPath) && metadataStages==0,
                    "Unknown config evidence admitted Prepared/child or retained a spurious no-child owner.");
                evidence["NoOwnerCachedConfiguration"]=Field("_productionConfig")!=null;
                Require(Field("_productionConfig")==null,"Refused no-owner startup retained tentative configuration for retry.");
                var cold=new FileSystemManager(root,NullLogger<FileSystemManager>.Instance);
                Exception? refused=null;try{await using var lease=await cold.AcquireCanonicalWriteLeaseAsync();}catch(Exception failure){refused=failure;}
                evidence["FreshAdmissionFailure"]=refused?.ToString();Require(refused!=null,"Fresh admission ignored unknown publication evidence.");
                Require(File.ReadAllBytes(configPath).SequenceEqual(unknownBytes) && File.ReadAllBytes(journalPath).SequenceEqual(selectedJournal!),"Cold refusal erased unknown evidence.");
                return;
            }
            Require(writeFailure is not CoordinatedStatePublicationUncertainException && File.ReadAllBytes(configPath).SequenceEqual(before) && !File.Exists(journalPath),
                "Known config publication did not restore exact A before assessing launch configuration.");
            evidence["ExactOriginalConfigRestored"]=true;
            Require(startupFailure==null,"Original configured startup failed: "+startupFailure);
            var main=(GmSessionRunCoordinator)Field("_mainRun")!;var terminal=(IOwnedTerminalSession)Field("_pty")!;
            Require(main.Record?.Disposition==GmSessionRunDisposition.Running && main.Identity.RunId==terminal.Identity.RunId &&
                !terminal.RootExited.IsCompleted && !terminal.AuthorityLost.IsCompleted,"Actual configured child lacks original Running authority.");
            string output="";
            for(var i=0;i<200;i++){
                var diagnostics=Invoke("SnapshotDiagnostics")!;
                output=(string)diagnostics.GetType().GetProperty("RecentOutputTail")!.GetValue(diagnostics)!;
                if(output.Contains("CONFIGURED_ARG2:",StringComparison.Ordinal) && output.Contains("TTY_READY",StringComparison.Ordinal))break;
                await Task.Delay(10);
            }
            var cached=(GameSettings)Invoke("LoadBridgeConfig")!;
            evidence["ActualOutput"]=output;evidence["CachedCommand"]=cached.GmCliLaunchCommand;
            evidence["CachedCwd"]=cached.GmBridgeShellWorkingDirectory;evidence["CachedInputTimeout"]=cached.GmCliInputProfile.ObservationTimeoutMilliseconds;
            evidence["CachedWorkerProfiles"]=cached.GmWorkerBridgeProfiles;evidence["ActualStatus"]=Invoke("SnapshotStatus");
            evidence["ActualIdentity"]=main.Identity;
            Require(output.Contains("CONFIGURED_ARG2:",StringComparison.Ordinal) && output.Contains("TTY_READY",StringComparison.Ordinal),"Inert original child did not report its actual launch arguments.");
            Require(output.Contains("CONFIGURED_ARG2:gm-model-sentinel",StringComparison.Ordinal) &&
                output.Contains("CONFIGURED_CWD:"+expected.GmBridgeShellWorkingDirectory,StringComparison.Ordinal),
                "Causal RED: original configured child consumed transient B after exact canonical rollback to A.");
            Require(cached.GmCliLaunchCommand==expected.GmCliLaunchCommand && cached.GmBridgeShellWorkingDirectory==expected.GmBridgeShellWorkingDirectory &&
                cached.GmCliInputProfile.ObservationTimeoutMilliseconds==expected.GmCliInputProfile.ObservationTimeoutMilliseconds &&
                cached.GmWorkerBridgeProfiles.Count==expected.GmWorkerBridgeProfiles.Count,"Admitted per-run config/profile/cache disagrees with recovered A.");
            var status=JsonSerializer.SerializeToElement(Invoke("SnapshotStatus"));
            Require(status.GetProperty("CliLaunchCommand").GetString()==expected.GmCliLaunchCommand &&
                status.GetProperty("ShellWorkingDirectory").GetString()==expected.GmBridgeShellWorkingDirectory &&
                status.GetProperty("WorkerStatuses").GetArrayLength()==expected.GmWorkerBridgeProfiles.Count,
                "Original status was derived from transient preflight configuration.");
            // GREEN extension: a later confirmed config publication is for a
            // later epoch, not permission to replace this running snapshot.
            var liveFiles=new FileSystemManager(root,NullLogger<FileSystemManager>.Instance);
            var originalIdentity=main.Identity;
            await main.RunOperationAsync(async()=>{await liveFiles.WriteFileAtomicBytesAsync("config.json",after);return 0;});
            Require(File.ReadAllBytes(configPath).SequenceEqual(after) && !File.Exists(journalPath),"Later config publication did not actually commit B.");
            var stillAdmitted=(GameSettings)Invoke("LoadBridgeConfig")!;
            evidence["LaterConfigBCommitted"]=true;evidence["SameCachedObject"]=ReferenceEquals(cached,stillAdmitted);
            Require(stillAdmitted.GmCliLaunchCommand==expected.GmCliLaunchCommand &&
                stillAdmitted.GmBridgeShellWorkingDirectory==expected.GmBridgeShellWorkingDirectory &&
                stillAdmitted.GmCliInputProfile.ObservationTimeoutMilliseconds==expected.GmCliInputProfile.ObservationTimeoutMilliseconds &&
                stillAdmitted.GmWorkerBridgeProfiles.Count==expected.GmWorkerBridgeProfiles.Count &&
                main.Identity==originalIdentity && ReferenceEquals(terminal,Field("_pty")),
                "Later committed configuration replaced an existing original run/profile snapshot.");
            evidence["RunningSnapshotUnchanged"]=true;
        }
        finally
        {
            release.Set();await writer.WaitAsync(TimeSpan.FromSeconds(5));
            if(startup!=null){try{await startup.WaitAsync(TimeSpan.FromSeconds(8));}catch{}Require(startup.IsCompleted,"Original startup escaped fixture cleanup.");}
            type.GetField("ObserveMainGuardContention",flags)!.SetValue(host,null);
            type.GetField("ObserveMainMetadata",flags)!.SetValue(host,null);
        }
    }
}
