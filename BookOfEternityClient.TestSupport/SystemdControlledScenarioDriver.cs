using System.Reflection;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32.SafeHandles;

namespace BookOfEternityClient.Tests;

// Real original native child/reaper/PTY beneath the independent guardian; only
// manager/cgroup observations are injected. Each invocation owns a fresh root.
internal static class SystemdControlledScenarioDriver
{
    internal static async Task<int> RunAsync(string mode,string package,string folder)
    {
        var bus=new ControlledBus {FixtureFolder=folder,Mode=mode};var cgroup=new ControlledCgroup(bus) {Mode=mode};var fixture=new SystemdControlledFixture(bus,cgroup);
        if(mode=="terminal-systemd-connected") {
            var code=await OwnedTerminalScenarioDriver.RunSystemdBridgeAsync(package,folder,fixture);
            var record=ReadRecord(folder);var facts=JsonSerializer.Deserialize<Dictionary<string,object?>>(File.ReadAllText(Path.Combine(folder,"scenario.json")))!;
            facts["ActualScopePreparedRunningStopping"]=bus.Stages.Contains(GmSessionRunDisposition.Prepared) && bus.Stages.Contains(GmSessionRunDisposition.Running) && bus.Stages.Contains(GmSessionRunDisposition.Stopping);
            facts["OriginalPidfdTransferred"]=bus.ActualPidfd;facts["OneScopeStartStop"]=bus.StartCount==1 && bus.StopCount==1;
            facts["ActualDisposedAndStoppedAck"]=bus.Disposed && cgroup.Disposed && record.Disposition==GmSessionRunDisposition.Stopped;
            var success=code==0 && bus.ActualPidfd && bus.SubscribedBeforeStart && bus.Stages.Contains(GmSessionRunDisposition.Running) && bus.StopCount==1 && bus.Disposed && cgroup.Disposed && record.Disposition==GmSessionRunDisposition.Stopped;
            facts["Success"]=success;File.WriteAllText(Path.Combine(folder,"scenario.json"),JsonSerializer.Serialize(facts));return success?0:1;
        }
        var ioMode=mode switch {"terminal-systemd-io-drain"=>"terminal-main-output-drain","terminal-systemd-io-fault"=>"terminal-main-output-fault","terminal-systemd-stopped-debt"=>"terminal-main-stopped-debt-epoch","terminal-systemd-native-dispose-fault"=>"terminal-main-native-dispose-fault","terminal-systemd-bus-dispose-fault"=>"terminal-main-bus-dispose-fault",_=>null};
        if(ioMode!=null)return await MainRunFenceScenarioDriver.RunAsync(ioMode,package,folder,fixture);
        var launch=NeutralTerminalLaunch.Create(package,folder);bus.Root=Directory.GetParent(launch.Scratch)!.FullName;
        var files=new FileSystemManager(bus.Root,NullLogger<FileSystemManager>.Instance);files.EnsureDirectoryStructure();
        var owner=await GmSessionRunCoordinator.OpenNeutralAsync(files);IOwnedTerminalSession? terminal=null;int held=0;
        var facts2=new Dictionary<string,object?> {["Mode"]=mode};
        var startFailure=mode is "terminal-systemd-start-lost" or "terminal-systemd-start-cancel" or "terminal-systemd-wrong-fd" or "terminal-systemd-wrong-manager" or "terminal-systemd-release-loss" or "terminal-systemd-release-ack" or "terminal-systemd-bind-deadline";
        using var startCancellation=new CancellationTokenSource();
        bus.CancelStart=startCancellation.Cancel;
        bus.Mode=mode;cgroup.Mode=mode;
        bus.BeforeRelease=()=>{
            var original=(SystemdOwnedTerminalSession)typeof(GmSessionRunCoordinator).GetField("_terminal",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(owner)!;
            var scope=typeof(SystemdOwnedTerminalSession).GetField("_scope",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(original)!;
            var native=(NativeLineageOwner)scope.GetType().GetField("_original",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(scope)!;
            native.SetSyntheticObservationFault(new(GmWorkerNativeObservationFaultKind.MalformedStarted));
        };
        try {
            try {terminal=await owner.LaunchSystemdControlledAsync(launch,fixture,startCancellation.Token,p=>held=p);}
            catch(OwnedTerminalStartException ex){terminal=ex.Owner;facts2["OriginalException"]=ex.InnerException?.GetType().Name;}
            Require(terminal!=null && held>0,"Missing original held terminal.");
            if(startFailure) {
                Require(owner.IsUncertain && owner.RetainsAuthority && terminal!.AuthorityLost.IsCompleted,"Ambiguous start lost original authority.");
                Require(bus.StartCount==1 && owner.Record?.Disposition!=GmSessionRunDisposition.Stopped,"Start replayed/settled.");
            } else {
                Require(owner.Record?.Disposition==GmSessionRunDisposition.Running,"Scope failed before Running.");
                if(mode=="terminal-systemd-native-fault") {
                    var scope=terminal!.GetType().GetField("_scope",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(terminal)!;
                    var native=(NativeLineageOwner)scope.GetType().GetField("_original",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(scope)!;
                    native.SetSyntheticObservationFault(new(GmWorkerNativeObservationFaultKind.MalformedTerminal));
                }
            }
            var output=new MemoryStream();var drain=terminal!.OutputReader.CopyToAsync(output);
            await owner.BeginStopAsync();var proof=await terminal.StopAndObserveAsync(CancellationToken.None);await drain.WaitAsync(TimeSpan.FromSeconds(4));
            Require(proof.State==GmWorkerStopState.Uncertain && !proof.CleanupComplete && proof.AuthorityRetained,"Physical native cleanup promoted to logical systemd stop.");
            Require(owner.RetainsAuthority && owner.Record?.Disposition!=GmSessionRunDisposition.Stopped,"Uncertain scope released durable owner.");
            Require(ReferenceEquals(proof,await terminal.StopAndObserveAsync(CancellationToken.None)),"Stop proof/retry changed.");
            Require(bus.StopCount<=1,"StopUnit replayed after uncertain receipt.");
            if(mode is "terminal-systemd-changed-unit" or "terminal-systemd-changed-invocation")Require(bus.StopCount==0,"Replacement unit received StopUnit.");
            Exception? write=null;try{await terminal.InputWriter.WriteAsync(new byte[]{120});}catch(Exception ex){write=ex;}
            Require(write!=null && terminal.RootExited.IsCompletedSuccessfully,"Uncertain original input open or own root not reaped.");
            var text=System.Text.Encoding.UTF8.GetString(output.ToArray());
            if(mode=="terminal-systemd-bind-deadline")Require(owner.Record?.Disposition!=GmSessionRunDisposition.Running,"Cgroup bind expired but published Running.");
            if(startFailure && mode!="terminal-systemd-release-ack")Require(!text.Contains("TTY_READY",StringComparison.Ordinal),"Held root released after failed admission.");
            facts2["LogicalUncertainRetained"]=true;facts2["OriginalRootReapedAndOutputEof"]=true;facts2["Success"]=true;return 0;
        }catch(Exception ex){facts2["Failure"]=ex.ToString();facts2["Success"]=false;return 1;}
        finally {
            if(terminal!=null)try{await terminal.StopAndObserveAsync(CancellationToken.None);}catch(Exception ex){facts2["CleanupFailure"]=ex.ToString();}
            facts2["ActualHeldPid"]=held;facts2["StartCount"]=bus.StartCount;facts2["StopCount"]=bus.StopCount;
            File.WriteAllText(Path.Combine(folder,"scenario.json"),JsonSerializer.Serialize(facts2));
        }
    }
    private static GmSessionRunRecord ReadRecord(string folder)=>GmSessionRunRecordCodec.Decode(File.ReadAllBytes(Path.Combine(Directory.GetDirectories(folder,"neutral-session-*").Single(),".boe_runtime/gm-runs/main.json")));
    private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    internal sealed class ControlledBus : ISystemdBusTransport
    {
        private readonly TaskCompletionSource<string> _lost=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public SystemdManagerBinding Manager {get;private set;}=new(new string('b',32),":1.42",GmWorkerProcessHostPeerIdentity.CaptureEffectiveUserId(),File.ReadAllText("/proc/sys/kernel/random/boot_id").Trim());
        public Task<string> AuthorityLost=>_lost.Task;
        public bool SupportsPidfdScopes=>true;
        internal int StartCount,StopCount;internal bool SubscribedBeforeStart,ActualPidfd,Disposed;
        internal string? Root,Mode,FixtureFolder;internal Action? BeforeRelease,CancelStart;internal List<GmSessionRunDisposition> Stages=[];
        public Task SubscribeAsync(CancellationToken token){SubscribedBeforeStart=true;return Task.CompletedTask;}
        public Task StartScopeAsync(SystemdScopeRequest request,CancellationToken token){
            StartCount++;Require(SubscribedBeforeStart,"Subscription followed mutation.");
            var fd=request.Pidfd.DangerousGetHandle().ToInt32();var info=File.ReadAllText($"/proc/self/fdinfo/{fd}");
            ActualPidfd=info.Split('\n').Any(s=>s.StartsWith("Pid:",StringComparison.Ordinal) && int.TryParse(s[4..].Trim(),out var pid) && pid>0);
            Require(ActualPidfd,"Not the original held pidfd.");
            // Only own root metadata, never environment/cmdline or foreign process.
            ObserveStage(request.Name);Require(Stages[^1]==GmSessionRunDisposition.Prepared,"Scope creation preceded Prepared.");
            if(Mode=="terminal-systemd-start-lost")throw new IOException("controlled possibly-created start receipt lost");
            if(Mode=="terminal-systemd-start-cancel"){CancelStart!();token.ThrowIfCancellationRequested();}
            return Task.CompletedTask;
        }
        private void ObserveStage(string name) {
            if(Root==null)Root=Directory.GetDirectories(FixtureFolder!,"neutral-session-*").Single();
            if(Root!=null && File.Exists(Path.Combine(Root,".boe_runtime/gm-runs/main.json")))Stages.Add(GmSessionRunRecordCodec.Decode(File.ReadAllBytes(Path.Combine(Root,".boe_runtime/gm-runs/main.json"))).Disposition);
        }
        public Task<SystemdUnitSnapshot> ObserveAsync(string name,SafeFileHandle? fd,CancellationToken token){
            ObserveStage(name);
            if(Mode=="terminal-systemd-wrong-manager")Manager=Manager with {UniqueOwner=":1.43"};
            if(Mode=="terminal-systemd-release-ack" && Stages.LastOrDefault()==GmSessionRunDisposition.Running){BeforeRelease?.Invoke();BeforeRelease=null;}
            if(Mode=="terminal-systemd-release-loss" && Stages.LastOrDefault()==GmSessionRunDisposition.Running)_lost.TrySetResult("controlled before-A1 manager loss");
            if(Mode=="terminal-systemd-late-loss" && StopCount==1 && fd==null && ++_stopObservations==2)_lost.TrySetResult("controlled after-native manager loss");
            return Task.FromResult(new SystemdUnitSnapshot(Mode=="terminal-systemd-changed-unit" && Stages.LastOrDefault()==GmSessionRunDisposition.Stopping?"/org/freedesktop/systemd1/unit/replacement":"/org/freedesktop/systemd1/unit/controlled",new string(Mode=="terminal-systemd-changed-invocation" && Stages.LastOrDefault()==GmSessionRunDisposition.Stopping?'c':'a',32),"/controlled/"+name,Mode=="terminal-systemd-wrong-fd"?"/org/freedesktop/systemd1/unit/other":"/org/freedesktop/systemd1/unit/controlled"));
        }
        private int _stopObservations;
        public Task StopScopeAsync(string name,CancellationToken token){StopCount++;ObserveStage(name);Require(Stages[^1] is GmSessionRunDisposition.Stopping or GmSessionRunDisposition.Uncertain,"StopUnit preceded durable closing.");if(Mode=="terminal-systemd-stop-lost")throw new IOException("controlled stop receipt lost");return Task.CompletedTask;}
        public ValueTask DisposeAsync(){Disposed=true;_lost.TrySetResult("controlled expected original connection closure");if(Mode=="terminal-systemd-bus-dispose-fault")throw new IOException("controlled bus disposal fault");return ValueTask.CompletedTask;}
    }
    internal sealed class ControlledCgroup(ControlledBus bus) : ISystemdCgroupSource
    {
        private SystemdCgroupIdentity? _id;internal string? Mode;internal bool Disposed;
        public SystemdCgroupIdentity BindOriginal(SystemdUnitSnapshot unit,int pid){if(Mode=="terminal-systemd-bind-deadline")Thread.Sleep(5200);return _id=new(unit.ControlGroup,1,1,1);}
        public SystemdCgroupSample ReadOriginal(long sequence) {
            var stopping=bus.StopCount>0;
            if(stopping && Mode=="terminal-systemd-read-error")throw new IOException("controlled events read error");
            return new(stopping && Mode=="terminal-systemd-changed-cgroup"?_id! with {Inode=2}:_id!,stopping && Mode=="terminal-systemd-stale"?sequence-1:sequence,
                !stopping || Mode=="terminal-systemd-populated"?SystemdCgroupState.Populated:Mode=="terminal-systemd-pruned"?SystemdCgroupState.Pruned:SystemdCgroupState.Empty);
        }
        public void Dispose(){Disposed=true;}
    }
}
