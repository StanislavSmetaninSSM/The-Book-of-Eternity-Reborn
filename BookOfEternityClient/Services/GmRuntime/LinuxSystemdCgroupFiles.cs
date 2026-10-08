using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
namespace BookOfEternityClient.Services.GmRuntime;
internal sealed class LinuxSystemdCgroupFiles : ISystemdCgroupFiles {
    private SafeFileHandle? _namespaceFd;
    private LinuxCgroupFileIdentity? _namespace;
    private bool _disposed;
    public void PinNamespace() {
        RequireLive();if(_namespaceFd!=null)throw LinuxCgroupDirectory.Invalid();
        // Intentional known proc ns magic link; retained FD prevents inode reuse.
        _namespaceFd=LinuxCgroupDirectory.OpenNamespace(null);_namespace=LinuxCgroupDirectory.Stat(_namespaceFd);
    }
    public string NamespaceOf(int? pid) {
        RequireLive();using var fd=LinuxCgroupDirectory.OpenNamespace(pid);var id=LinuxCgroupDirectory.Stat(fd);
        if(pid==null && _namespace!=null && (id!=_namespace || LinuxCgroupDirectory.Stat(_namespaceFd!)!=_namespace))throw LinuxCgroupDirectory.Invalid();
        return $"cgroup:[{id.Device}:{id.Inode}]";
    }
    public string ReadMountInfo(){RequireLive();return ReadProc("/proc/self/mountinfo",1048576);}
    public string ReadMembership(int pid){RequireLive();if(pid<=0)throw LinuxCgroupDirectory.Invalid();return ReadProc($"/proc/{pid}/cgroup",65536);}
    public ISystemdCgroupPin Pin(SystemdCgroupMount mount,string group) {
        RequireLive();var pin=LinuxCgroupDirectory.Open(mount.MountPoint,group);
        try {
            var id=pin.DirectoryIdentity;
            if(!pin.IsCgroup2 || id.MountId!=mount.MountId || id.Device!=mount.Device)throw LinuxCgroupDirectory.Invalid();
            return new CgroupPin(pin,new(group,id.MountId,id.Device,id.Inode));
        }catch{pin.Dispose();throw;}
    }
    private sealed class CgroupPin(LinuxCgroupDirectory pin,SystemdCgroupIdentity id):ISystemdCgroupPin {
        public SystemdCgroupIdentity Identity {get;}=id;
        public byte[] ReadEventsChecked()=>pin.ReadEventsChecked();
        public void Dispose()=>pin.Dispose();
    }
    private static string ReadProc(string path,int limit) {
        using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);var bytes=new byte[limit+1];var count=0;
        while(count<bytes.Length){var n=stream.Read(bytes,count,bytes.Length-count);if(n==0)break;count+=n;}
        if(count>limit)throw LinuxCgroupDirectory.Invalid();return new UTF8Encoding(false,true).GetString(bytes,0,count);
    }
    private void RequireLive(){LinuxCgroupDirectory.RequirePlatform();if(_disposed)throw LinuxCgroupDirectory.Invalid();}
    public void Dispose(){if(_disposed)return;_disposed=true;_namespaceFd?.Dispose();}
}
internal sealed record LinuxCgroupFileIdentity(ulong MountId,ulong Device,ulong Inode,ushort Type);
// Dedicated primitive: ordinary-file tests prove FD behavior only. Native wrapper
// additionally requires actual cgroup2 before supplying any scope observations.
internal sealed class LinuxCgroupDirectory : IDisposable {
    private readonly SafeFileHandle _mount,_directory,_events;
    private readonly string _relative;
    private readonly LinuxCgroupFileIdentity _mountId,_directoryId,_eventsId;
    private bool _disposed;
    private LinuxCgroupDirectory(SafeFileHandle mount,SafeFileHandle directory,SafeFileHandle events,string relative) {
        _mount=mount;_directory=directory;_events=events;_relative=relative;
        _mountId=Stat(mount);_directoryId=Stat(directory);_eventsId=Stat(events);
        if(_mountId.Type!=0x4000 || _directoryId.Type!=0x4000 || _eventsId.Type!=0x8000 ||
           _mountId.MountId!=_directoryId.MountId || _mountId.Device!=_directoryId.Device ||
           _eventsId.MountId!=_directoryId.MountId || _eventsId.Device!=_directoryId.Device)throw Invalid();
    }
    internal LinuxCgroupFileIdentity DirectoryIdentity=>_directoryId;
    internal bool IsCgroup2 {
        get {if(_disposed)throw Invalid();var buffer=Marshal.AllocHGlobal(256);
            try{if(fstatfs(_directory.DangerousGetHandle().ToInt32(),buffer)!=0)throw Invalid();return Marshal.ReadInt64(buffer)==0x63677270;}
            finally{Marshal.FreeHGlobal(buffer);}}
    }
    internal static LinuxCgroupDirectory Open(string mountPath,string group) {
        RequirePlatform();SystemdCgroupSource.CanonicalPath(mountPath,true);SystemdCgroupSource.CanonicalPath(group,false);
        SafeFileHandle? mount=null,directory=null,events=null;
        try {
            using var root=OpenAt(-100,"/",true);mount=Walk(root,mountPath[1..]);directory=Walk(mount,group[1..]);
            events=OpenAt(directory.DangerousGetHandle().ToInt32(),"cgroup.events",false);
            return new(mount,directory,events,group[1..]);
        }catch{events?.Dispose();directory?.Dispose();mount?.Dispose();throw;}
    }
    internal byte[] ReadEventsChecked() {
        if(_disposed)throw Invalid();Validate();var bytes=new byte[4097];var count=0;
        while(count<bytes.Length) {
            var chunk=new byte[bytes.Length-count];var n=pread(_events.DangerousGetHandle().ToInt32(),chunk,(nuint)chunk.Length,count);
            if(n<0){if(Marshal.GetLastPInvokeError()==4)continue;throw Invalid();}if(n==0)break;
            Array.Copy(chunk,0,bytes,count,(int)n);count+=(int)n;
        }
        if(count>4096)throw Invalid();Validate();return bytes[..count];
    }
    private void Validate() {
        if(Stat(_mount)!=_mountId || Stat(_directory)!=_directoryId || Stat(_events)!=_eventsId)throw Invalid();
        using var current=Walk(_mount,_relative);using var events=OpenAt(current.DangerousGetHandle().ToInt32(),"cgroup.events",false);
        if(Stat(current)!=_directoryId || Stat(events)!=_eventsId)throw Invalid();
    }
    private static SafeFileHandle Walk(SafeFileHandle anchor,string relative) {
        SafeFileHandle? current=null;
        try {
            if(relative.Length==0)return OpenAt(anchor.DangerousGetHandle().ToInt32(),".",true);
            foreach(var component in relative.Split('/')) {
                var next=OpenAt((current??anchor).DangerousGetHandle().ToInt32(),component,true);current?.Dispose();current=next;
            }return current!;
        }catch{current?.Dispose();throw;}
    }
    private static SafeFileHandle OpenAt(int parent,string name,bool directory) {
        var fd=openat(parent,name,0x80000|0x20000|0x800|(directory?0x10000:0));if(fd<0)throw Invalid();return new((IntPtr)fd,true);
    }
    internal static SafeFileHandle OpenNamespace(int? pid) {
        RequirePlatform();if(pid is <=0)throw Invalid();var fd=openat(-100,pid==null?"/proc/self/ns/cgroup":$"/proc/{pid}/ns/cgroup",0x80000);
        if(fd<0)throw Invalid();return new((IntPtr)fd,true);
    }
    internal static LinuxCgroupFileIdentity Stat(SafeFileHandle fd) {
        const uint mask=0x1101;
        if(fd.IsClosed || statx(fd.DangerousGetHandle().ToInt32(),"",0x1000,mask,out var s)!=0 || (s.Mask&mask)!=mask || s.Inode==0 || s.MountId==0)throw Invalid();
        return new(s.MountId,((ulong)s.DeviceMajor<<32)|s.DeviceMinor,s.Inode,(ushort)(s.Mode&0xf000));
    }
    internal static void RequirePlatform(){if(!OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture!=Architecture.X64)throw new IOException("Readonly cgroup ABI unqualified on this platform.");}
    internal static IOException Invalid()=>new("Original readonly cgroup descriptor unavailable/changed; no empty evidence.");
    public void Dispose(){if(_disposed)return;_disposed=true;_events.Dispose();_directory.Dispose();_mount.Dispose();}
    [StructLayout(LayoutKind.Explicit,Size=256)]private struct Statx {
        [FieldOffset(0)]internal uint Mask;[FieldOffset(28)]internal ushort Mode;[FieldOffset(32)]internal ulong Inode;
        [FieldOffset(136)]internal uint DeviceMajor;[FieldOffset(140)]internal uint DeviceMinor;[FieldOffset(144)]internal ulong MountId;
    }
    [DllImport("libc",SetLastError=true)]private static extern int openat(int fd,string path,int flags);
    [DllImport("libc",SetLastError=true)]private static extern int statx(int fd,string path,int flags,uint mask,out Statx value);
    [DllImport("libc",SetLastError=true)]private static extern int fstatfs(int fd,IntPtr buffer);
    [DllImport("libc",SetLastError=true)]private static extern long pread(int fd,byte[] bytes,nuint size,long offset);
}
