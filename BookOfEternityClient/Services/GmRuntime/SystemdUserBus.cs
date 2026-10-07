using Microsoft.Win32.SafeHandles;
using BookOfEternityClient.Services.GmWorkers;
namespace BookOfEternityClient.Services.GmRuntime;
// Original connection/reference authority; snapshots do not construct this object.
internal sealed class SystemdUserBus(ISystemdBusTransport transport)
{
    private SystemdManagerBinding? _manager;
    private Task<string>? _authorityLost;
    private string? _name;
    private SystemdUnitSnapshot? _unit;
    private int _start,_stop;
    internal bool Bound=>_unit!=null;
    internal Task<string> AuthorityLost=>_authorityLost??transport.AuthorityLost;
    internal void CaptureOriginal() {
        if(_manager!=null)throw Invalid();
        _manager=transport.Manager;_authorityLost=transport.AuthorityLost;RequireManager();
    }
    internal SystemdUnitSnapshot Unit=>_unit??throw Invalid();
    internal void RequireManager() {
        if(_manager==null || !ReferenceEquals(_manager,transport.Manager) ||
            !ReferenceEquals(_authorityLost,transport.AuthorityLost) || AuthorityLost.IsCompleted ||
            _manager.BusId.Length!=32 || !_manager.BusId.All(Uri.IsHexDigit) ||
            !_manager.UniqueOwner.StartsWith(':') || _manager.UniqueOwner.Length>128 ||
            _manager.UserId!=GmWorkerProcessHostPeerIdentity.CaptureEffectiveUserId() ||
            _manager.BootId!=File.ReadAllText("/proc/sys/kernel/random/boot_id").Trim())throw Invalid();
    }
    internal async Task AttachAsync(NativeHeldTerminalPidfd held,CancellationToken token) {
        if(Interlocked.Exchange(ref _start,1)!=0)throw Invalid();
        held.ValidateHeld();RequireManager();_name="boe-main-"+held.RunId+".scope";
        await transport.SubscribeAsync(token);RequireManager();held.ValidateHeld();
        await transport.StartScopeAsync(new(_name,held.Descriptor),token);RequireManager();held.ValidateHeld();
        var unit=await transport.ObserveAsync(_name,held.Descriptor,token);
        ValidateSnapshot(unit,true);_unit=unit;RequireManager();held.ValidateHeld();
    }
    internal async Task ValidateOriginalAsync(SafeFileHandle? held,CancellationToken token) {
        RequireManager();var now=await transport.ObserveAsync(_name??throw Invalid(),held,token);ValidateSnapshot(now,held!=null);
        if(now.UnitPath!=Unit.UnitPath || now.InvocationId!=Unit.InvocationId || now.ControlGroup!=Unit.ControlGroup)throw Invalid();
        RequireManager();
    }
    private void ValidateSnapshot(SystemdUnitSnapshot unit,bool pidfd) {
        if(!unit.UnitPath.StartsWith("/org/freedesktop/systemd1/unit/",StringComparison.Ordinal) || unit.UnitPath.Length>1024 ||
            unit.InvocationId.Length!=32 || !unit.InvocationId.All(Uri.IsHexDigit) || unit.InvocationId.All(c=>c=='0') ||
            !unit.ControlGroup.StartsWith('/') || unit.ControlGroup.Length>4096 ||
            unit.ControlGroup.Split('/').Any(p=>p is "." or "..") || !unit.ControlGroup.EndsWith('/'+_name,StringComparison.Ordinal) ||
            (pidfd && unit.PidfdUnitPath!=unit.UnitPath))throw Invalid();
    }
    internal async Task StopAsync(CancellationToken token) {
        if(!Bound)throw Invalid();await ValidateOriginalAsync(null,token);
        if(Interlocked.Exchange(ref _stop,1)!=0)throw Invalid();
        await transport.StopScopeAsync(_name!,token);await ValidateOriginalAsync(null,token);
    }
    internal ValueTask DisposeAsync()=>transport.DisposeAsync();
    private static IOException Invalid()=>new("Original systemd manager/unit binding unavailable or changed.");
}
