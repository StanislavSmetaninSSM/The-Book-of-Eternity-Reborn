using System.Globalization;
using System.Text;
namespace BookOfEternityClient.Services.GmRuntime;
internal sealed record SystemdCgroupMount(ulong MountId,ulong Device,string MountPoint);
internal interface ISystemdCgroupFiles : IDisposable {
    void PinNamespace(); string NamespaceOf(int? pid); string ReadMountInfo(); string ReadMembership(int pid);
    ISystemdCgroupPin Pin(SystemdCgroupMount mount,string controlGroup);
}
internal interface ISystemdCgroupPin : IDisposable {
    SystemdCgroupIdentity Identity {get;} byte[] ReadEventsChecked();
}
internal sealed class SystemdCgroupSource(ISystemdCgroupFiles files) : ISystemdCgroupSource {
    private readonly object _gate=new();
    private ISystemdCgroupPin? _pin;
    private SystemdCgroupIdentity? _original;
    private SystemdCgroupMount? _mount;
    private string? _namespace;
    private long _sequence;
    private bool _bound,_faulted,_disposed;
    internal SystemdCgroupSource():this(new LinuxSystemdCgroupFiles()) { }
    public SystemdCgroupIdentity BindOriginal(SystemdUnitSnapshot unit,int pid) {
        lock(_gate)try {
            RequireLive();if(_bound || pid<=0)throw Invalid();_bound=true;
            CanonicalPath(unit.ControlGroup,false);
            files.PinNamespace();_namespace=files.NamespaceOf(null);
            RequireHeldMembership(unit.ControlGroup,pid);
            _mount=SelectMount(files.ReadMountInfo());
            _pin=files.Pin(_mount,unit.ControlGroup);_original=_pin.Identity;
            if(_original.ControlGroup!=unit.ControlGroup || _original.MountId!=_mount.MountId || _original.Device!=_mount.Device || _original.Inode==0)throw Invalid();
            if(ReadChecked()!=SystemdCgroupState.Populated)throw Invalid();
            RequireHeldMembership(unit.ControlGroup,pid);return _original;
        }catch(Exception ex){_faulted=true;throw Invalid(ex);}
    }
    public SystemdCgroupSample ReadOriginal(long sequence) {
        lock(_gate)try {
            RequireLive();if(_original==null || sequence!=checked(_sequence+1))throw Invalid();
            var state=ReadChecked();_sequence=sequence;return new(_original,sequence,state);
        }catch(Exception ex){_faulted=true;throw Invalid(ex);}
    }
    private SystemdCgroupState ReadChecked() {
        ValidateOriginal();var state=ParseEvents(_pin!.ReadEventsChecked());ValidateOriginal();return state;
    }
    private void ValidateOriginal() {
        if(files.NamespaceOf(null)!=_namespace || SelectMount(files.ReadMountInfo())!=_mount || _pin!.Identity!=_original)throw Invalid();
    }
    private void RequireHeldMembership(string group,int pid) {
        if(files.NamespaceOf(null)!=_namespace || files.NamespaceOf(pid)!=_namespace || Membership(files.ReadMembership(pid))!=group)throw Invalid();
    }
    private void RequireLive(){if(_disposed || _faulted)throw Invalid();}
    public void Dispose() {
        lock(_gate){if(_disposed)return;_disposed=true;try{_pin?.Dispose();}finally{files.Dispose();}}
    }
    private static string Membership(string value) {
        BoundedText(value,65536);var lines=value.Split('\n').Where(l=>l.StartsWith("0::",StringComparison.Ordinal)).ToArray();
        if(lines.Length!=1)throw Invalid();var group=lines[0][3..];CanonicalPath(group,false);return group;
    }
    private static SystemdCgroupMount SelectMount(string text) {
        BoundedText(text,1048576);var mounts=new List<SystemdCgroupMount>();
        foreach(var line in text.Split('\n',StringSplitOptions.RemoveEmptyEntries)) {
            var sections=line.Split(" - ",StringSplitOptions.None);if(sections.Length!=2)throw Invalid();
            var tail=sections[1].Split(' ');if(tail.Length<3)throw Invalid();if(tail[0]!="cgroup2")continue;
            var head=sections[0].Split(' ');if(head.Length<6 || DecodeMountPath(head[3])!="/")throw Invalid();
            var point=DecodeMountPath(head[4]);CanonicalPath(point,true);var dev=head[2].Split(':');
            if(!ulong.TryParse(head[0],NumberStyles.None,CultureInfo.InvariantCulture,out var mount) || mount==0 || dev.Length!=2 ||
               !uint.TryParse(dev[0],NumberStyles.None,CultureInfo.InvariantCulture,out var major) || !uint.TryParse(dev[1],NumberStyles.None,CultureInfo.InvariantCulture,out var minor))throw Invalid();
            var device=((ulong)major<<32)|minor;if(device==0)throw Invalid();mounts.Add(new(mount,device,point));
        }
        if(mounts.Count!=1)throw Invalid();return mounts[0];
    }
    private static string DecodeMountPath(string text) {
        var result=new StringBuilder();for(var i=0;i<text.Length;i++) {
            if(text[i]!='\\'){result.Append(text[i]);continue;}
            if(i+3>=text.Length)throw Invalid();result.Append(text.Substring(i,4) switch {"\\040"=>' ',"\\011"=>'\t',"\\012"=>'\n',"\\134"=>'\\',_=>throw Invalid()});i+=3;
        }return result.ToString();
    }
    internal static void CanonicalPath(string path,bool allowRoot) {
        if(string.IsNullOrEmpty(path) || path[0]!='/' || path.Any(char.IsControl) || path.EndsWith(" (deleted)",StringComparison.Ordinal))throw Invalid();
        if(path=="/"){if(allowRoot)return;throw Invalid();}
        if(path[1..].Split('/').Any(p=>p.Length==0 || p is "." or ".."))throw Invalid();
    }
    private static void BoundedText(string text,int limit){if(new UTF8Encoding(false,true).GetByteCount(text)>limit)throw Invalid();}
    private static SystemdCgroupState ParseEvents(byte[] bytes) {
        if(bytes.Length>4096)throw Invalid();var text=new UTF8Encoding(false,true).GetString(bytes);
        if(!text.EndsWith('\n'))throw Invalid();var values=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var line in text[..^1].Split('\n')) {
            var fields=line.Split(' ');if(fields.Length!=2 || fields[0].Length==0 || fields[0].Any(c=>!char.IsAsciiLetter(c) && c!='_') ||
                fields[1].Length==0 || fields[1].Any(c=>!char.IsAsciiDigit(c)) || !values.TryAdd(fields[0],fields[1]))throw Invalid();
        }
        return values.GetValueOrDefault("populated") switch {"0"=>SystemdCgroupState.Empty,"1"=>SystemdCgroupState.Populated,_=>throw Invalid()};
    }
    private static IOException Invalid(Exception? inner=null)=>new("Original cgroup mapping/descriptor/sample unavailable; uncertainty is retained and pruning is unqualified.",inner);
}
