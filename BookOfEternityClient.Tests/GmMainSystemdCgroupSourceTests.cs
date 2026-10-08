using System.Text;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using BookOfEternityClient.Services.GmRuntime;
using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmMainSystemdCgroupSourceTests {
    private static readonly SystemdUnitSnapshot Unit=new("/unit/own",new string('a',32),"/own.scope","/unit/own");
    [Fact] public void PopulatedBindingThenFreshEmpty_UsesOriginalSource() {
        var files=new Frames();using var source=new SystemdCgroupSource(files);
        var id=source.BindOriginal(Unit,123);Assert.Equal(Unit.ControlGroup,id.ControlGroup);
        Assert.Equal(SystemdCgroupState.Populated,source.ReadOriginal(1).State);
        files.Events="populated 0\nfrozen 0\n";var sample=source.ReadOriginal(2);
        Assert.Equal(id,sample.Identity);Assert.Equal(2,sample.Sequence);Assert.Equal(SystemdCgroupState.Empty,sample.State);
        Assert.Equal(123,files.LastPid);Assert.True(files.NamespacePinned);
    }
    [Fact] public void EscapedUnicodeMount_MapsWithoutChangingBytes() {
        var files=new Frames {MountInfo="9 1 0:42 / /own\\040Δ rw - cgroup2 none rw\n"};using var source=new SystemdCgroupSource(files);
        source.BindOriginal(Unit,123);Assert.Equal("/own Δ",files.Mount!.MountPoint);
    }
    [Theory]
    [InlineData("namespace")][InlineData("membership")][InlineData("deleted")][InlineData("duplicate")]
    [InlineData("subtree")][InlineData("ambiguous")][InlineData("traversal")][InlineData("oversize")][InlineData("empty")]
    public void InvalidInitialMapping_DoesNotPinBoundary(string reason) {
        var f=new Frames();switch(reason) {
        case "namespace":f.HeldNamespace="cgroup:[99]";break;
        case "membership":f.Membership="0::/foreign.scope\n";break;
        case "deleted":f.Membership="0::/own.scope (deleted)\n";break;
        case "duplicate":f.Membership+="0::/own.scope\n";break;
        case "subtree":f.MountInfo=f.MountInfo.Replace("0:42 / ","0:42 /subtree ");break;
        case "ambiguous":f.MountInfo+="10 1 0:43 / /other rw - cgroup2 none rw\n";break;
        case "traversal":f.Membership="0::/x/../own.scope\n";break;
        case "oversize":f.MountInfo=new string('x',1048577);break;
        case "empty":f.Events="populated 0\n";break;
        }
        using var source=new SystemdCgroupSource(f);Assert.Throws<IOException>(()=>source.BindOriginal(Unit,123));
        if(reason!="empty")Assert.Equal(0,f.PinCount);
        f.Events="populated 0\n";Assert.Throws<IOException>(()=>source.ReadOriginal(1));
    }
    [Theory]
    [InlineData("namespace")][InlineData("mount")][InlineData("inode")][InlineData("io")]
    [InlineData("duplicate")][InlineData("missing")][InlineData("malformed")][InlineData("oversize")][InlineData("utf8")]
    public void FailedSample_IsStickyAndCannotBecomeEmpty(string failure) {
        var f=new Frames();using var source=new SystemdCgroupSource(f);source.BindOriginal(Unit,123);
        switch(failure) {
        case "namespace":f.SelfNamespace="cgroup:[100]";break;
        case "mount":f.MountInfo=f.MountInfo.Replace("9 1","11 1");break;
        case "inode":f.Changed=true;break;
        case "io":f.Error=true;break;
        case "duplicate":f.Events="populated 0\npopulated 0\n";break;
        case "missing":f.Events="frozen 0\n";break;
        case "malformed":f.Events="populated 2\n";break;
        case "oversize":f.Events=new string('x',4097);break;
        case "utf8":f.Bytes=new byte[]{255};break;
        }
        Assert.Throws<IOException>(()=>source.ReadOriginal(1));
        f.SelfNamespace="cgroup:[10]";f.MountInfo=Frames.MountText;f.Changed=f.Error=false;f.Bytes=null;f.Events="populated 0\n";
        Assert.Throws<IOException>(()=>source.ReadOriginal(2));
    }
    [Fact] public void SingleBindAndStrictSequence_CannotRemintOrReplay() {
        var f=new Frames();using var s=new SystemdCgroupSource(f);s.BindOriginal(Unit,123);s.ReadOriginal(1);
        Assert.Throws<IOException>(()=>s.ReadOriginal(1));Assert.Throws<IOException>(()=>s.BindOriginal(Unit,123));Assert.Equal(1,f.PinCount);
    }
    [Fact] public void DisposedSource_ClosesDescriptorsAndRefusesSamples() {
        var f=new Frames();var s=new SystemdCgroupSource(f);s.BindOriginal(Unit,123);s.Dispose();s.Dispose();
        Assert.True(f.Disposed);Assert.Equal(1,f.PinDisposals);Assert.Throws<IOException>(()=>s.ReadOriginal(1));
    }
    [Fact] public void ActualReadonlyPin_UnicodeFreshBytesAndDisposal() {
        WithFolder(root=>{using var pin=LinuxCgroupDirectory.Open(root,"/Δ folder");
            Assert.False(pin.IsCgroup2);Assert.Equal("populated 1\n",Encoding.UTF8.GetString(pin.ReadEventsChecked()));
            File.WriteAllText(Path.Combine(root,"Δ folder/cgroup.events"),"populated 0\n");Assert.Equal("populated 0\n",Encoding.UTF8.GetString(pin.ReadEventsChecked()));
            pin.Dispose();Assert.Throws<IOException>(()=>pin.ReadEventsChecked());});
    }
    [Theory][InlineData("directory")][InlineData("events")][InlineData("link")][InlineData("missing")][InlineData("oversize")]
    public void ActualReadonlyPin_RefusesReplacedMissingLinkedOrUnboundedFiles(string kind) {
        WithFolder(root=>{using var pin=LinuxCgroupDirectory.Open(root,"/Δ folder");var group=Path.Combine(root,"Δ folder");var events=Path.Combine(group,"cgroup.events");
            switch(kind) {
            case "directory":Directory.Move(group,Path.Combine(root,"old"));Directory.CreateDirectory(group);File.WriteAllText(events,"populated 0\n");break;
            case "events":File.Move(events,events+".old");File.WriteAllText(events,"populated 0\n");break;
            case "link":File.Delete(events);File.CreateSymbolicLink(events,Path.Combine(root,"other"));break;
            case "missing":File.Delete(events);break;
            case "oversize":File.WriteAllText(events,new string('x',4097));break;
            }
            Assert.Throws<IOException>(()=>pin.ReadEventsChecked());});
    }
    [Fact] public void NativeWrapper_RejectsOrdinaryDirectoryAsCgroup() {
        WithFolder(root=>{using var files=new LinuxSystemdCgroupFiles();Assert.Throws<IOException>(()=>files.Pin(new(9,42,root),"/Δ folder"));});
    }
    [Fact] public void NativeSelfNamespace_PinIdentityAndOwnedFdClosure() {
        Assert.True(OperatingSystem.IsLinux());using var files=new LinuxSystemdCgroupFiles();files.PinNamespace();
        var handle=OwnedHandle(files,"_namespaceFd");var raw=handle.DangerousGetHandle().ToInt32();
        var original=files.NamespaceOf(null);Assert.StartsWith("cgroup:[",original);Assert.Equal(original,files.NamespaceOf(null));
        Assert.Throws<IOException>(()=>files.PinNamespace());Assert.False(handle.IsClosed);
        files.Dispose();AssertClosed(handle,raw);Assert.Throws<IOException>(()=>files.NamespaceOf(null));
    }
    [Fact] public void ActualReadonlyPin_DisposalClosesAllOwnedDescriptors() {
        WithFolder(root=>{using var pin=LinuxCgroupDirectory.Open(root,"/Δ folder");
            var handles=new[]{"_mount","_directory","_events"}.Select(n=>OwnedHandle(pin,n)).ToArray();
            var numbers=handles.Select(h=>h.DangerousGetHandle().ToInt32()).ToArray();pin.ReadEventsChecked();pin.Dispose();
            for(var i=0;i<handles.Length;i++)AssertClosed(handles[i],numbers[i]);});
    }
    [Theory][InlineData("directory")][InlineData("events")]
    public void InitialLinkedBoundary_RefusesAndClosesPartialDescriptors(string kind) {
        WithFolder(root=>{using(var warm=LinuxCgroupDirectory.Open(root,"/Δ folder"))warm.ReadEventsChecked();
            var group=Path.Combine(root,"Δ folder");var events=Path.Combine(group,"cgroup.events");
            if(kind=="directory") {Directory.Move(group,Path.Combine(root,"target"));Directory.CreateSymbolicLink(group,Path.Combine(root,"target"));}
            else {File.Delete(events);File.CreateSymbolicLink(events,Path.Combine(root,"other"));}
            // Own process descriptor count only; never read foreign FD targets.
            var before=Directory.GetFiles("/proc/self/fd").Length;
            Assert.Throws<IOException>(()=>LinuxCgroupDirectory.Open(root,"/Δ folder"));
            Assert.Equal(before,Directory.GetFiles("/proc/self/fd").Length);
        });
    }
    private static SafeFileHandle OwnedHandle(object owner,string name)=>(SafeFileHandle)owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(owner)!;
    private static void AssertClosed(SafeFileHandle handle,int fd) {
        Assert.True(handle.IsClosed);Assert.Equal(-1,fcntl(fd,1));Assert.Equal(9,Marshal.GetLastPInvokeError());
    }
    [DllImport("libc",SetLastError=true)]private static extern int fcntl(int fd,int command);
    private static void WithFolder(Action<string> run) {
        Assert.True(OperatingSystem.IsLinux());var root=Path.Combine(Path.GetTempPath(),"cgroup-pin-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(root,"Δ folder"));
        File.WriteAllText(Path.Combine(root,"Δ folder/cgroup.events"),"populated 1\n");File.WriteAllText(Path.Combine(root,"other"),"populated 0\n");
        try{run(root);}finally{Directory.Delete(root,true);}
    }
    private sealed class Frames : ISystemdCgroupFiles {
        internal const string MountText="9 1 0:42 / /controlled rw - cgroup2 none rw\n";
        internal string MountInfo=MountText,Membership="0::/own.scope\n",SelfNamespace="cgroup:[10]",HeldNamespace="cgroup:[10]",Events="populated 1\nfrozen 0\n";
        internal byte[]? Bytes;internal bool Changed,Error,NamespacePinned,Disposed;internal int PinCount,PinDisposals,LastPid;internal SystemdCgroupMount? Mount;
        public SystemdCgroupIdentity Identity=>new("/own.scope",9,42,Changed?2UL:1UL);
        public void PinNamespace()=>NamespacePinned=true;
        public string NamespaceOf(int? pid)=>pid==null?SelfNamespace:HeldNamespace;
        public string ReadMountInfo()=>MountInfo;
        public string ReadMembership(int pid){LastPid=pid;return Membership;}
        public ISystemdCgroupPin Pin(SystemdCgroupMount mount,string group){PinCount++;Mount=mount;return new PinOwner(this);}
        public byte[] ReadEventsChecked()=>Error?throw new IOException("controlled read failure"):Bytes??Encoding.UTF8.GetBytes(Events);
        private sealed class PinOwner(Frames f):ISystemdCgroupPin {
            public SystemdCgroupIdentity Identity=>f.Identity;
            public byte[] ReadEventsChecked()=>f.ReadEventsChecked();
            public void Dispose()=>f.PinDisposals++;
        }
        public void Dispose()=>Disposed=true;
    }
}
