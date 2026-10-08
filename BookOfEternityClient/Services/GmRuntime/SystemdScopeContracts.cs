using Microsoft.Win32.SafeHandles;
namespace BookOfEternityClient.Services.GmRuntime;

internal sealed record SystemdManagerBinding(string BusId,string UniqueOwner,uint UserId,string BootId);
internal sealed record SystemdUnitSnapshot(string UnitPath,string InvocationId,string ControlGroup,string PidfdUnitPath);
internal sealed record SystemdScopeRequest(string Name,SafeFileHandle Pidfd)
{
    internal string Mode=>"fail";
    internal IReadOnlyDictionary<string,object> Properties=>new Dictionary<string,object> {
        ["PIDFDs"]=Pidfd,["AddRef"]=true,["KillMode"]="control-group",
        ["SendSIGKILL"]=true,["TimeoutStopUSec"]=(ulong)2_000_000
    };
}
// One independent connection; subscription precedes Start and matching JobRemoved
// completion includes signals which arrive before the method reply.
internal interface ISystemdBusTransport : IAsyncDisposable
{
    SystemdManagerBinding Manager {get;}
    Task<string> AuthorityLost {get;}
    bool SupportsPidfdScopes {get;}
    Task SubscribeAsync(CancellationToken token);
    Task StartScopeAsync(SystemdScopeRequest request,CancellationToken token);
    Task<SystemdUnitSnapshot> ObserveAsync(string name,SafeFileHandle? originalPidfd,CancellationToken token);
    Task StopScopeAsync(string name,CancellationToken token);
}
internal sealed record SystemdCgroupIdentity(string ControlGroup,ulong MountId,ulong Device,ulong Inode);
internal enum SystemdCgroupState { Populated,Empty,Pruned,Invalid }
internal sealed record SystemdCgroupSample(SystemdCgroupIdentity Identity,long Sequence,SystemdCgroupState State);
// S2 supplies the original read-only kernel descriptors; S1 controlled observations
// never constitute positive manager/cgroup qualification.
internal interface ISystemdCgroupSource : IDisposable
{
    SystemdCgroupIdentity BindOriginal(SystemdUnitSnapshot unit,int originalHeldPid);
    SystemdCgroupSample ReadOriginal(long requestedSequence);
}
// No CLI/config/environment switch creates this capability.
internal sealed class SystemdControlledFixture(ISystemdBusTransport transport,ISystemdCgroupSource cgroup)
{
    internal ISystemdBusTransport Transport {get;}=transport;
    internal ISystemdCgroupSource Cgroup {get;}=cgroup;
    private int _consumed;
    internal void Consume() { RequireAvailable();if(Interlocked.Exchange(ref _consumed,1)!=0)throw new InvalidOperationException("Original controlled scope capability consumed."); }
    internal void RequireAvailable() {
        if(Volatile.Read(ref _consumed)!=0 || !OperatingSystem.IsLinux() || !Transport.SupportsPidfdScopes || Transport.AuthorityLost.IsCompleted)
            throw new PlatformNotSupportedException("Controlled original scope unavailable; no backend switch.");
    }
}
