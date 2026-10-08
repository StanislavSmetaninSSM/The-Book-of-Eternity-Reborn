using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

// Original private Bridge dispatch/service/pool under a real neutral main owner.
// Public neutral/Linux worker capability remains refused; no worker is launched.
internal static class MainWorkerDispatchScenario
{
    internal static async Task RunAsync(string boundary,string root,string package,object host,Type hostType,
        GmSessionRunCoordinator owner,Func<Task> stop,Dictionary<string,object?> evidence)
    {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        object? Field(string name)=>hostType.GetField(name,flags)!.GetValue(host);
        var files=new FileSystemManager(root,NullLogger<FileSystemManager>.Instance);
        var profile=GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with {LaunchCommand="exit 74",TimeoutSeconds=5,MaxConcurrentTasks=1};
        var settings=new GameSettings {GmCliLaunchCommand="",GmWorkerBridgeProfiles=[profile]};
        await owner.RunOperationAsync(async()=>{
            await files.WriteFileAtomicAsync("config.json",JsonSerializer.Serialize(settings));
            await files.WriteFileAtomicAsync("game_state/world/weather.json","{\"fixture\":\"dispatch-context\"}");
            return 0;
        });
        evidence["PositiveOriginalMutation"]=true;
        using var admission=new GmWorkerNativePoolAdmission(package,root);
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var waits=0;var reservations=0;var factories=0;var contentions=0;
        FileStream? lockHolder=null;
        IDisposable? occupied=null;Task? dispatch=null,stopping=null;
        var input=Field("_inputLifetime")!;
        bool Revoked()=>(bool)input.GetType().GetField("Revoked")!.GetValue(input)!;
        var hooks=new GmWorkerBridgePoolHooks {
            BeforeWorkerSlotWaitAsync=async()=>{if(Interlocked.Increment(ref waits)==2){entered.TrySetResult();await release.Task;}},
            BeforeTaskReservationAsync=()=>{reservations++;throw new InvalidOperationException("Fixture forbids reservation/process launch.");}
        };
        var factory=(Func<FileSystemManager,GmWorkerAuditLog,GmWorkerBridgePool>)((fs,audit)=>{
            factories++;
            var pool=new GmWorkerBridgePool(fs,null,audit,hooks,GmWorkerProcessTreeFactory.Instance,GmWorkerQuarantineReaper.Shared,admission);
            if(boundary=="slot") {
                var acquisition=(Task)typeof(GmWorkerBridgePool).GetMethod("AcquireWorkerSlotAsync",flags)!.Invoke(pool,[profile,CancellationToken.None])!;
                acquisition.GetAwaiter().GetResult();
                var acquired=acquisition.GetType().GetProperty("Result")!.GetValue(acquisition)!;
                occupied=(IDisposable)acquired.GetType().GetProperty("Lease")!.GetValue(acquired)!;
            }
            return pool;
        });
        hostType.GetField("WorkerDispatchPoolFactory",flags)!.SetValue(host,factory);
        try
        {
            if(boundary=="lease") {
                hostType.GetField("WorkerDispatchFileHooks",flags)!.SetValue(host,new FileSystemManagerHooks {
                    CanonicalWriteLockContendedAsync=()=>{Interlocked.Increment(ref contentions);entered.TrySetResult();return Task.CompletedTask;}
                });
                lockHolder=new FileStream(files.CanonicalWriteLockPath,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
            }
            var requestType=hostType.Assembly.GetType("BookOfEternityGMBridge.BridgeRequest",true)!;
            var request=JsonSerializer.Deserialize(JsonSerializer.Serialize(new {WorkerTaskType="analysis",AnalysisGoal="Bounded cancellation fixture",
                Questions=new[]{"Inspect fixture context"},ContextPaths=new[]{"game_state/world/weather.json"}}),requestType)!;
            dispatch=(Task)hostType.GetMethod("DispatchWorkerTaskAsync",flags)!.Invoke(host,[request])!;
            await Task.WhenAny(dispatch,entered.Task).WaitAsync(TimeSpan.FromSeconds(3));
            Exception? failure=null;
            if(dispatch.IsCompleted)try{await dispatch;}catch(Exception error){failure=error;}
            evidence["Factories"]=factories;evidence["SlotWaitsBeforeStop"]=waits;evidence["ContextLeaseContentions"]=contentions;
            evidence["DispatchFailureBeforeWait"]=failure?.ToString();evidence["BeforeReservation"]=reservations;
            Require(entered.Task.IsCompleted && (boundary=="slot" ? waits==2 : contentions>0) && failure==null,"Original private dispatch failed before its actual occupied "+boundary+" wait.");
            Require(!dispatch.IsCompleted,"Occupied original wait did not retain dispatch.");
            stopping=stop();
            var timer=Stopwatch.StartNew();while(!Revoked() && timer.Elapsed<TimeSpan.FromSeconds(2))await Task.Delay(10);
            Require(Revoked(),"Original stop did not revoke captured input lifetime.");
            Require(!stopping.IsCompleted,"Original stop escaped the live dispatch pin.");
            release.TrySetResult();
            try{await dispatch.WaitAsync(TimeSpan.FromSeconds(3));}catch(Exception error){failure=error;}
            evidence["DispatchFailureAfterRevocation"]=failure?.ToString();
            Require(failure is OperationCanceledException,"Captured input cancellation did not reach original "+boundary+" acquisition.");
            // For the context-lease case, dispatch must settle while the physical
            // holder is still owned. Main Stopping publication can wait for it.
            evidence["DispatchSettledBeforeHolderRelease"]=dispatch.IsCompleted;
            lockHolder?.Dispose();lockHolder=null;
            await stopping.WaitAsync(TimeSpan.FromSeconds(5));
            Require(owner.Record?.Disposition==GmSessionRunDisposition.Stopped && !owner.RetainsAuthority,"Original pin/stop/retirement did not settle.");
            Require(factories==1 && (boundary=="slot" ? waits==2 : waits==0) && reservations==0,"Cancellation admitted a task reservation or replayed dispatch.");
            Require(!Directory.Exists(files.ResolvePath(GmWorkerBridgePool.TaskRoot)) || !Directory.EnumerateFiles(files.ResolvePath(GmWorkerBridgePool.TaskRoot),"*",SearchOption.AllDirectories).Any(),"Cancelled slot wait created task bytes.");
            evidence["InputRevoked"]=true;evidence["OriginalStopped"]=true;evidence["Reservations"]=reservations;
        }
        finally
        {
            release.TrySetResult();occupied?.Dispose();lockHolder?.Dispose();
            if(dispatch!=null){try{await dispatch.WaitAsync(TimeSpan.FromSeconds(6));}catch{}Require(dispatch.IsCompleted,"Original dispatch task escaped fixture cleanup.");}
            if(stopping!=null){try{await stopping.WaitAsync(TimeSpan.FromSeconds(6));}catch{}Require(stopping.IsCompleted,"Original stop task escaped fixture cleanup.");}
            hostType.GetField("WorkerDispatchPoolFactory",flags)!.SetValue(host,null);
            hostType.GetField("WorkerDispatchFileHooks",flags)!.SetValue(host,null);
        }
    }
}
