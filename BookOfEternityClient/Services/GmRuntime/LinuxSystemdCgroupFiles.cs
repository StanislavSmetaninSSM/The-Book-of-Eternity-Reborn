namespace BookOfEternityClient.Services.GmRuntime;
internal sealed class LinuxSystemdCgroupFiles : ISystemdCgroupFiles {
    public void PinNamespace()=>throw new IOException("Namespace pin not implemented.");
    public string NamespaceOf(int? pid)=>throw new IOException("Namespace observation not implemented.");
    public string ReadMountInfo()=>throw new IOException("Mount observation not implemented.");
    public string ReadMembership(int pid)=>throw new IOException("Membership observation not implemented.");
    public ISystemdCgroupPin Pin(SystemdCgroupMount mount,string group)=>throw new IOException("Cgroup descriptor not implemented.");
    public void Dispose() { }
}
internal sealed class LinuxCgroupDirectory : IDisposable {
    internal static LinuxCgroupDirectory Open(string mount,string group)=>throw new IOException("Read-only descriptor pin not implemented.");
    internal byte[] ReadEventsChecked()=>throw new IOException("Read-only descriptor pin not implemented.");
    internal bool IsCgroup2=>false;
    public void Dispose() { }
}
