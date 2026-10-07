using Microsoft.Win32.SafeHandles;

namespace BookOfEternityClient.Services.GmRuntime;

internal sealed record SystemdManagerBinding(string BusId, string UniqueOwner, uint UserId, string BootId);
internal sealed record SystemdUnitSnapshot(string UnitPath, string InvocationId, string ControlGroup, string PidfdUnitPath);
internal sealed record SystemdScopeRequest(string Name, SafeFileHandle Pidfd)
{
    internal string Mode => "fail";
    // RED baseline: the missing transient-scope contract is not availability.
    internal IReadOnlyDictionary<string, object> Properties => new Dictionary<string, object>();
}

// One independent connection. Subscribe must precede Start; completion includes
// its matching JobRemoved even when it arrives before the method reply.
internal interface ISystemdBusTransport : IAsyncDisposable
{
    SystemdManagerBinding Manager { get; }
    Task<string> AuthorityLost { get; }
    Task SubscribeAsync(CancellationToken token);
    Task StartScopeAsync(SystemdScopeRequest request, CancellationToken token);
    Task<SystemdUnitSnapshot> ObserveAsync(string name, SafeFileHandle? originalPidfd, CancellationToken token);
    Task StopScopeAsync(string name, CancellationToken token);
}

internal interface ISystemdCgroupSource : IDisposable
{
    void BindOriginal(SystemdUnitSnapshot unit, int originalHeldPid);
    void ValidateOriginal();
    bool ReadFreshEmpty();
}

// No CLI argument/config/environment switch creates this capability. Only the
// fixed neutral technical fixture can consume it; production stays closed.
internal sealed record SystemdControlledFixture(ISystemdBusTransport Transport, ISystemdCgroupSource Cgroup);
