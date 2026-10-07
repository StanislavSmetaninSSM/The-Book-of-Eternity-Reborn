using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using BookOfEternityClient.Services.GmWorkers;
namespace BookOfEternityClient.Services.GmRuntime;

// Source implementation for later explicit S2 qualification only. No production
// selector/factory calls OpenUserUnqualified; presence is not qualification.
internal sealed class SystemdSdBusTransport : ISystemdBusTransport
{
    private const string ManagerPath="/org/freedesktop/systemd1",ManagerInterface="org.freedesktop.systemd1.Manager";
    private const string Library="libsystemd.so.0";
    private readonly object _gate=new();
    private readonly TaskCompletionSource<string> _lost=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource _pumpStop=new();
    private readonly Handler _ownerCallback,_jobCallback;
    private IntPtr _bus,_ownerSlot,_jobSlot;
    private Task? _pump;
    private bool _subscribed,_disposed;
    private string? _scope;
    private SystemdJobReceipts? _receipts;
    public SystemdManagerBinding Manager {get;private set;}=null!;
    public bool SupportsPidfdScopes=>true; // API source only, never production admission.
    public Task<string> AuthorityLost=>_lost.Task;
    private SystemdSdBusTransport(){_ownerCallback=OwnerChanged;_jobCallback=JobRemoved;}
    internal static SystemdSdBusTransport OpenUserUnqualified()
    {
        if(!OperatingSystem.IsLinux())throw new PlatformNotSupportedException("User sd-bus is Linux-only.");
        var result=new SystemdSdBusTransport();
        try {
            Check(OpenUser(out result._bus));Check(SetTimeout(result._bus,2_000_000));
            Check(Match(result._bus,out result._ownerSlot,"org.freedesktop.DBus","/org/freedesktop/DBus","org.freedesktop.DBus","NameOwnerChanged",result._ownerCallback,IntPtr.Zero));
            Check(BusId(result._bus,out var id));var owner=result.GetOwner();
            using var credentials=result.Call("org.freedesktop.DBus","/org/freedesktop/DBus","org.freedesktop.DBus","GetConnectionUnixUser",w=>w.String(owner));
            var uid=credentials.UInt32();
            if(uid!=GmWorkerProcessHostPeerIdentity.CaptureEffectiveUserId())throw new IOException("User manager UID differs from original caller.");
            result.Manager=new(id.ToString("N"),owner,uid,File.ReadAllText("/proc/sys/kernel/random/boot_id").Trim());
            if(result.GetOwner()!=owner || result.AuthorityLost.IsCompleted)throw new IOException("User manager changed during original capture.");
            return result;
        } catch {result.Close();throw;}
    }
    private string GetOwner() {using var reply=Call("org.freedesktop.DBus","/org/freedesktop/DBus","org.freedesktop.DBus","GetNameOwner",w=>w.String("org.freedesktop.systemd1"));return reply.String('s');}
    private Task Run(Action work,CancellationToken token)=>Task.Run(()=>{lock(_gate){token.ThrowIfCancellationRequested();RequireOpen();work();RequireOpen();}},CancellationToken.None).WaitAsync(token);
    private void RequireOpen(){if(_disposed || _bus==IntPtr.Zero || AuthorityLost.IsCompleted)throw new IOException("Original user bus unavailable.");}
    public Task SubscribeAsync(CancellationToken token)=>Run(()=>{
        if(_subscribed)throw new IOException("Original user bus subscription already consumed.");
        Check(Match(_bus,out _jobSlot,Manager.UniqueOwner,ManagerPath,ManagerInterface,"JobRemoved",_jobCallback,IntPtr.Zero));
        using var reply=Call(Manager.UniqueOwner,ManagerPath,ManagerInterface,"Subscribe");_subscribed=true;
        _pump=PumpAsync();
    },token);
    public Task StartScopeAsync(SystemdScopeRequest request,CancellationToken token)=>Run(()=>{
        if(!_subscribed || _scope!=null)throw new IOException("Original scope start already consumed or unsubscribed.");
        _scope=request.Name;_receipts=new(Manager.UniqueOwner,_scope);
        using var reply=Call(Manager.UniqueOwner,ManagerPath,ManagerInterface,"StartTransientUnit",w=>SdBusScopeCodec.Write(request,w));
        WaitJob(reply.String('o'),token);
    },token);
    public async Task<SystemdUnitSnapshot> ObserveAsync(string name,SafeFileHandle? originalPidfd,CancellationToken token)
    {
        SystemdUnitSnapshot? snapshot=null;
        await Run(()=>{
            if(name!=_scope)throw new IOException("Original scope name mismatch.");
            using var reply=Call(Manager.UniqueOwner,ManagerPath,ManagerInterface,"GetUnit",w=>w.String(name));var path=reply.String('o');
            using var inv=Call(Manager.UniqueOwner,path,"org.freedesktop.DBus.Properties","Get",w=>{w.String("org.freedesktop.systemd1.Unit");w.String("InvocationID");});
            var invocation=inv.InvocationVariant();
            using var cg=Call(Manager.UniqueOwner,path,"org.freedesktop.DBus.Properties","Get",w=>{w.String("org.freedesktop.systemd1.Scope");w.String("ControlGroup");});
            var group=cg.StringVariant();var pidPath="";
            if(originalPidfd!=null) {
                using var p=Call(Manager.UniqueOwner,ManagerPath,ManagerInterface,"GetUnitByPIDFD",w=>w.UnixFd(originalPidfd));
                pidPath=p.String('o');var unitName=p.String('s');var pidInvocation=p.InvocationArray();
                if(unitName!=name || pidInvocation!=invocation || pidPath!=path)throw new IOException("Original pidfd unit/invocation mismatch.");
            }
            snapshot=new(path,invocation,group,pidPath);
        },token);return snapshot!;
    }
    public Task StopScopeAsync(string name,CancellationToken token)=>Run(()=>{
        if(name!=_scope)throw new IOException("Original scope stop name mismatch.");
        using var reply=Call(Manager.UniqueOwner,ManagerPath,ManagerInterface,"StopUnit",w=>{w.String(name);w.String("fail");});WaitJob(reply.String('o'),token);
    },token);
    private void WaitJob(string path,CancellationToken token) {
        var deadline=Stopwatch.GetTimestamp()+2*Stopwatch.Frequency;
        while(!_receipts!.Complete(path)) {
            token.ThrowIfCancellationRequested();RequireOpen();if(Stopwatch.GetTimestamp()>=deadline)throw new TimeoutException("Original job receipt unavailable.");
            Check(Process(_bus,out var message));if(message!=IntPtr.Zero)UnrefMessage(message);Thread.Yield();
        }
    }
    private async Task PumpAsync() {
        try {
            while(!_pumpStop.IsCancellationRequested) {
                lock(_gate){if(_disposed)return;Check(Process(_bus,out var message));if(message!=IntPtr.Zero)UnrefMessage(message);}
                await Task.Delay(20,_pumpStop.Token);
            }
        }catch(OperationCanceledException)when(_pumpStop.IsCancellationRequested){}catch{_lost.TrySetResult("systemd-bus-disconnected-or-invalid");}
    }
    private int OwnerChanged(IntPtr message,IntPtr userdata,IntPtr error) {
        try {
            using var borrowed=new Message(message,false);var name=borrowed.String('s');var old=borrowed.String('s');var next=borrowed.String('s');
            if(name=="org.freedesktop.systemd1" && Manager!=null && next!=Manager.UniqueOwner)_lost.TrySetResult("systemd-manager-name-owner-changed");
        }catch{_lost.TrySetResult("systemd-owner-signal-invalid");}return 0;
    }
    private int JobRemoved(IntPtr message,IntPtr userdata,IntPtr error) {
        try {
            using var borrowed=new Message(message,false);var id=borrowed.UInt32();var path=borrowed.String('o');var unit=borrowed.String('s');var result=borrowed.String('s');
            _receipts?.Observe(ReadText(Sender(message)),unit,path,id,result);
        }catch{_lost.TrySetResult("systemd-job-signal-invalid");}return 0;
    }
    private Message Call(string destination,string path,string iface,string method,Action<Message>? append=null) {
        Check(NewMethod(_bus,out var raw,destination,path,iface,method));using var request=new Message(raw);
        Check(AutoStart(raw,0));Check(Interactive(raw,0));append?.Invoke(request);
        var result=NativeCall(_bus,raw,2_000_000,IntPtr.Zero,out var reply);
        if(result<0){if(reply!=IntPtr.Zero)UnrefMessage(reply);Check(result);}return new(reply);
    }
    public async ValueTask DisposeAsync() {
        await _pumpStop.CancelAsync();if(_pump!=null)await _pump;
        lock(_gate)Close();_pumpStop.Dispose();
    }
    private void Close() {
        _disposed=true;if(_ownerSlot!=IntPtr.Zero){UnrefSlot(_ownerSlot);_ownerSlot=IntPtr.Zero;}
        if(_jobSlot!=IntPtr.Zero){UnrefSlot(_jobSlot);_jobSlot=IntPtr.Zero;}
        if(_bus!=IntPtr.Zero){CloseUnref(_bus);_bus=IntPtr.Zero;}
    }
    private static void Check(int result){if(result<0)throw new IOException("sd-bus operation refused (errno "+(-result)+").");}
    private static string ReadText(IntPtr pointer) {
        if(pointer==IntPtr.Zero)throw new IOException("sd-bus string absent.");
        var n=0;while(n<=4096 && Marshal.ReadByte(pointer,n)!=0)n++;
        if(n>4096)throw new IOException("sd-bus string exceeds bound.");return Marshal.PtrToStringUTF8(pointer,n)!;
    }
    private sealed class Message(IntPtr pointer,bool owned=true) : ISdBusMessageWriter,IDisposable
    {
        public void Open(char type,string signature)=>Check(OpenContainer(pointer,(byte)type,signature));
        public void Close()=>Check(CloseContainer(pointer));
        public void String(string value)=>Check(AppendString(pointer,(byte)'s',value));
        public void Boolean(bool value){var n=value?1:0;Check(AppendInt(pointer,(byte)'b',ref n));}
        public void UInt64(ulong value)=>Check(AppendLong(pointer,(byte)'t',ref value));
        public void UnixFd(SafeFileHandle value) {
            var held=false;try{value.DangerousAddRef(ref held);var n=value.DangerousGetHandle().ToInt32();Check(AppendInt(pointer,(byte)'h',ref n));}finally{if(held)value.DangerousRelease();}
        }
        internal string String(char type){var result=ReadBasic(pointer,(byte)type,out var text);if(result<=0)throw new IOException("sd-bus reply signature mismatch.");return ReadText(text);}
        internal uint UInt32(){if(ReadUInt(pointer,(byte)'u',out var value)<=0)throw new IOException("sd-bus uint signature mismatch.");return value;}
        internal string StringVariant(){if(Enter(pointer,(byte)'v',"s")<=0)throw new IOException("sd-bus variant mismatch.");var value=String('s');Check(Exit(pointer));return value;}
        internal string InvocationVariant(){if(Enter(pointer,(byte)'v',"ay")<=0)throw new IOException("sd-bus invocation variant mismatch.");var value=InvocationArray();Check(Exit(pointer));return value;}
        internal string InvocationArray(){if(ReadArray(pointer,(byte)'y',out var bytes,out var size)<=0 || size!=16)throw new IOException("sd-bus invocation size mismatch.");var copy=new byte[16];Marshal.Copy(bytes,copy,0,16);return Convert.ToHexString(copy).ToLowerInvariant();}
        public void Dispose(){if(owned)UnrefMessage(pointer);}
    }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Handler(IntPtr message,IntPtr userdata,IntPtr error);
    [DllImport(Library,EntryPoint="sd_bus_open_user")] private static extern int OpenUser(out IntPtr bus);
    [DllImport(Library,EntryPoint="sd_bus_set_method_call_timeout")] private static extern int SetTimeout(IntPtr bus,ulong timeout);
    [DllImport(Library,EntryPoint="sd_bus_get_bus_id")] private static extern int BusId(IntPtr bus,out Guid id);
    [DllImport(Library,EntryPoint="sd_bus_match_signal")] private static extern int Match(IntPtr bus,out IntPtr slot,[MarshalAs(UnmanagedType.LPUTF8Str)]string sender,[MarshalAs(UnmanagedType.LPUTF8Str)]string path,[MarshalAs(UnmanagedType.LPUTF8Str)]string iface,[MarshalAs(UnmanagedType.LPUTF8Str)]string member,Handler handler,IntPtr data);
    [DllImport(Library,EntryPoint="sd_bus_message_new_method_call")] private static extern int NewMethod(IntPtr bus,out IntPtr message,[MarshalAs(UnmanagedType.LPUTF8Str)]string destination,[MarshalAs(UnmanagedType.LPUTF8Str)]string path,[MarshalAs(UnmanagedType.LPUTF8Str)]string iface,[MarshalAs(UnmanagedType.LPUTF8Str)]string method);
    [DllImport(Library,EntryPoint="sd_bus_message_set_auto_start")] private static extern int AutoStart(IntPtr message,int value);
    [DllImport(Library,EntryPoint="sd_bus_message_set_allow_interactive_authorization")] private static extern int Interactive(IntPtr message,int value);
    [DllImport(Library,EntryPoint="sd_bus_call")] private static extern int NativeCall(IntPtr bus,IntPtr message,ulong timeout,IntPtr error,out IntPtr reply);
    [DllImport(Library,EntryPoint="sd_bus_process")] private static extern int Process(IntPtr bus,out IntPtr message);
    [DllImport(Library,EntryPoint="sd_bus_message_get_sender")] private static extern IntPtr Sender(IntPtr message);
    [DllImport(Library,EntryPoint="sd_bus_message_open_container")] private static extern int OpenContainer(IntPtr message,byte type,[MarshalAs(UnmanagedType.LPUTF8Str)]string signature);
    [DllImport(Library,EntryPoint="sd_bus_message_close_container")] private static extern int CloseContainer(IntPtr message);
    [DllImport(Library,EntryPoint="sd_bus_message_append_basic")] private static extern int AppendString(IntPtr message,byte type,[MarshalAs(UnmanagedType.LPUTF8Str)]string value);
    [DllImport(Library,EntryPoint="sd_bus_message_append_basic")] private static extern int AppendInt(IntPtr message,byte type,ref int value);
    [DllImport(Library,EntryPoint="sd_bus_message_append_basic")] private static extern int AppendLong(IntPtr message,byte type,ref ulong value);
    [DllImport(Library,EntryPoint="sd_bus_message_read_basic")] private static extern int ReadBasic(IntPtr message,byte type,out IntPtr value);
    [DllImport(Library,EntryPoint="sd_bus_message_read_basic")] private static extern int ReadUInt(IntPtr message,byte type,out uint value);
    [DllImport(Library,EntryPoint="sd_bus_message_enter_container")] private static extern int Enter(IntPtr message,byte type,[MarshalAs(UnmanagedType.LPUTF8Str)]string signature);
    [DllImport(Library,EntryPoint="sd_bus_message_exit_container")] private static extern int Exit(IntPtr message);
    [DllImport(Library,EntryPoint="sd_bus_message_read_array")] private static extern int ReadArray(IntPtr message,byte type,out IntPtr buffer,out nuint size);
    [DllImport(Library,EntryPoint="sd_bus_message_unref")] private static extern IntPtr UnrefMessage(IntPtr message);
    [DllImport(Library,EntryPoint="sd_bus_slot_unref")] private static extern IntPtr UnrefSlot(IntPtr slot);
    [DllImport(Library,EntryPoint="sd_bus_close_unref")] private static extern IntPtr CloseUnref(IntPtr bus);
}
