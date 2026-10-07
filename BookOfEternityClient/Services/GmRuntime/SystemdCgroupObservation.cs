namespace BookOfEternityClient.Services.GmRuntime;
internal sealed class SystemdCgroupObservation(ISystemdCgroupSource source) : IDisposable
{
    private SystemdCgroupIdentity? _original;
    private long _sequence;
    internal void Bind(SystemdUnitSnapshot unit,int heldPid) {
        if(_original!=null)throw Invalid();var id=source.BindOriginal(unit,heldPid);
        if(id.ControlGroup!=unit.ControlGroup || id.MountId==0 || id.Device==0 || id.Inode==0)throw Invalid();
        _original=id;Validate();
    }
    private SystemdCgroupSample Read() {
        if(_original==null)throw Invalid();var sequence=checked(++_sequence);var sample=source.ReadOriginal(sequence);
        if(sample.Sequence!=sequence || sample.Identity!=_original || sample.State is SystemdCgroupState.Invalid or SystemdCgroupState.Pruned)throw Invalid();
        return sample;
    }
    internal void Validate()=>Read();
    internal bool ReadFreshEmpty()=>Read().State==SystemdCgroupState.Empty;
    public void Dispose()=>source.Dispose();
    private static IOException Invalid()=>new("Original cgroup evidence unavailable/stale/changed; pinned removal branch unqualified.");
}
