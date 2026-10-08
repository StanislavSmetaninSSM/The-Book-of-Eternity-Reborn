namespace BookOfEternityClient.Services.GmRuntime;
internal sealed record SystemdCgroupMount(ulong MountId,ulong Device,string MountPoint);
internal interface ISystemdCgroupFiles : IDisposable {
    void PinNamespace(); string NamespaceOf(int? pid); string ReadMountInfo(); string ReadMembership(int pid);
    ISystemdCgroupPin Pin(SystemdCgroupMount mount,string controlGroup);
}
internal interface ISystemdCgroupPin : IDisposable {
    SystemdCgroupIdentity Identity {get;} byte[] ReadEventsChecked();
}
// Causal baseline: source contract only, no observation implementation yet.
internal sealed class SystemdCgroupSource(ISystemdCgroupFiles files) : ISystemdCgroupSource {
    internal SystemdCgroupSource():this(new LinuxSystemdCgroupFiles()) { }
    public SystemdCgroupIdentity BindOriginal(SystemdUnitSnapshot unit,int pid)=>throw new IOException("Original cgroup source not implemented.");
    public SystemdCgroupSample ReadOriginal(long sequence)=>throw new IOException("Original cgroup source not implemented.");
    public void Dispose()=>files.Dispose();
}
