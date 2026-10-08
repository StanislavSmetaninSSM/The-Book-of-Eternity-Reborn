using System.Runtime.InteropServices;
using BookOfEternityClient.Services.GmRuntime;
using Microsoft.Win32.SafeHandles;

namespace BookOfEternityClient.Core;

internal enum MainRunIoStage { NamespaceCreated, BeforeNamespaceParentFlush, Staged, FileFlushed, Renamed, DirectoryFlushed, Readback }

// One bounded schema1 metadata CAS, below canonical recovery. The original guard
// lives outside the initialization namespace and is never released on metadata debt.
internal sealed class GmSessionRunPersistence
{
    private readonly GmMainOwnerGuard _guard;
    private readonly Action<MainRunIoStage>? _observe;
    private Pending? _pending;
    private bool _created;
    private sealed record Pending(byte[]? Before, byte[] After, string Stage);
    internal bool HasDebt => _pending != null;
    internal string DirectoryPath => Path.Combine(_guard.Root,".boe_runtime/gm-runs");
    internal string RecordPath => Path.Combine(DirectoryPath,"main.json");
    internal GmSessionRunPersistence(GmMainOwnerGuard guard,Action<MainRunIoStage>? observe=null)
    {
        if(!OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture!=Architecture.X64)
            throw new PlatformNotSupportedException("Main run durability adapter is not qualified on this platform.");
        _guard=guard;_observe=observe;
    }
    internal static byte[]? Read(string root,string? ownStage=null)
    {
        var scope=new TrustedLocalFileScope([root]);var dir=Path.Combine(root,".boe_runtime/gm-runs");
        var observed=scope.ObserveNamespace(dir);
        if(observed.BlockingFileAncestor!=null)throw Invalid();
        if(observed.Kind==TrustedLocalNamespaceKind.Missing)return null;
        scope.ValidateDirectory(dir,false);
        foreach(var p in Directory.EnumerateFileSystemEntries(dir))
            if(p!=Path.Combine(dir,"main.json") && p!=ownStage)throw Invalid();
        return ReadBounded(scope,Path.Combine(dir,"main.json"));
    }
    internal static byte[] ReadBounded(TrustedLocalFileScope scope,string path)
    {
        using var f=new FileStream(scope.ValidateFile(path,false),FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        var length=f.Length;if(length is <1 or >GmSessionRunRecordCodec.MaximumBytes)throw Invalid();
        var bytes=new byte[(int)length];f.ReadExactly(bytes);if(f.ReadByte()!=-1)throw Invalid();
        _=GmSessionRunRecordCodec.Decode(bytes);return bytes;
    }
    internal void Publish(byte[]? before,GmSessionRunRecord after)
    {
        if(_pending!=null)throw Invalid();_guard.Validate();
        var bytes=GmSessionRunRecordCodec.Encode(after);
        var actual=Read(_guard.Root);if(!Equal(actual,before))throw Invalid();
        _pending=new(before?.ToArray(),bytes,Path.Combine(DirectoryPath,"main-"+Guid.NewGuid().ToString("N")+".tmp"));
        Execute();
    }
    internal void Retry(){if(_pending==null)throw Invalid();Execute();}
    private void Execute()
    {
        var p=_pending??throw Invalid();_guard.Validate();var scope=new TrustedLocalFileScope([_guard.Root]);
        if(!Directory.Exists(DirectoryPath))
        {
            // Only this live plan that observed the whole namespace absent can initialize.
            if(p.Before!=null || _created || Mkdir(scope.ValidateDirectory(DirectoryPath),0x1c0)!=0)throw Invalid();
            _created=true;_observe?.Invoke(MainRunIoStage.NamespaceCreated);
        }
        if(_created) {
            Sync(DirectoryPath);_observe?.Invoke(MainRunIoStage.BeforeNamespaceParentFlush);
            Sync(Path.GetDirectoryName(DirectoryPath)!);Sync(_guard.Root);
        }
        scope.ValidateDirectory(DirectoryPath,false);
        byte[]? actual;
        if(!File.Exists(RecordPath))
        {
            if(!_created || p.Before!=null)throw Invalid();
            foreach(var file in Directory.EnumerateFileSystemEntries(DirectoryPath))if(file!=p.Stage)throw Invalid();
            actual=null;
        }
        else actual=Read(_guard.Root,p.Stage);
        if(!Equal(actual,p.Before) && !Equal(actual,p.After))throw Invalid();
        if(!Equal(actual,p.After))
        {
            var stage=scope.ValidateFile(p.Stage);var exists=File.Exists(stage);
            if(exists && !ReadBounded(scope,stage).AsSpan().SequenceEqual(p.After))throw Invalid();
            using(var f=new FileStream(stage,exists?FileMode.Open:FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None))
            {if(!exists)f.Write(p.After);_observe?.Invoke(MainRunIoStage.Staged);f.Flush(true);_observe?.Invoke(MainRunIoStage.FileFlushed);}
            _guard.Validate();File.Move(scope.ValidateFile(stage,false),scope.ValidateFile(RecordPath),p.Before!=null);
            _observe?.Invoke(MainRunIoStage.Renamed);
        }
        else
        {
            if(File.Exists(p.Stage))throw Invalid();
            using var f=new FileStream(scope.ValidateFile(RecordPath,false),FileMode.Open,FileAccess.ReadWrite,FileShare.None);f.Flush(true);
        }
        Sync(DirectoryPath);_observe?.Invoke(MainRunIoStage.DirectoryFlushed);
        _guard.Validate();if(!Read(_guard.Root)!.AsSpan().SequenceEqual(p.After))throw Invalid();
        _observe?.Invoke(MainRunIoStage.Readback);_guard.Validate();_pending=null;
    }
    private static bool Equal(byte[]? a,byte[]? b)=>a==null?b==null:b!=null && a.AsSpan().SequenceEqual(b);
    private static void Sync(string path)
    {
        var fd=Open(path,0x10000|0x80000|0x20000);if(fd<0)throw Invalid();
        using var handle=new SafeFileHandle((IntPtr)fd,true);if(Fsync(handle)!=0)throw Invalid();
    }
    private static readonly object RefusalMarker = new();
    internal static bool IsAdmissionRefusal(Exception failure) => failure.Data.Contains(RefusalMarker);
    internal static IOException Invalid()
    {
        var failure = new IOException("Main run metadata or original owner admission is unavailable.");
        failure.Data[RefusalMarker] = true;
        return failure;
    }
    [DllImport("libc",EntryPoint="open",SetLastError=true)]private static extern int Open(string path,int flags);
    [DllImport("libc",EntryPoint="fsync",SetLastError=true)]private static extern int Fsync(SafeFileHandle handle);
    [DllImport("libc",EntryPoint="mkdir",SetLastError=true)]private static extern int Mkdir(string path,uint mode);
}

internal sealed class GmMainOwnerGuard : IDisposable
{
    private FileStream? _stream;
    private readonly string _path;
    internal string Root {get;}
    private GmMainOwnerGuard(string root,string path,FileStream stream){Root=root;_path=path;_stream=stream;}
    internal static async Task<GmMainOwnerGuard> AcquireAsync(string root,CancellationToken token=default,
        Func<Task>? contended=null,int attempts=40,TimeSpan? retryDelay=null)
    {
        var scope=new TrustedLocalFileScope([root]);var dir=scope.EnsureDirectory(Path.Combine(root,".boe_runtime/locks"));
        var path=scope.ValidateFile(Path.Combine(dir,"gm-main-owner.lock"));
        for(var i=0;;i++)
        {
            token.ThrowIfCancellationRequested();
            FileStream stream;
            try{stream=new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None,1,FileOptions.Asynchronous);}
            catch(IOException) when(i<attempts-1){
                if(contended!=null)await contended();
                await Task.Delay(retryDelay??TimeSpan.FromMilliseconds(25),token);continue;
            }
            var guard=new GmMainOwnerGuard(root,path,stream);
            try{guard.Validate();return guard;}catch{guard.Dispose();throw;}
        }
    }
    internal void Validate()
    {
        var stream=_stream??throw GmSessionRunPersistence.Invalid();
        new TrustedLocalFileScope([Root]).ValidateFile(_path,false);
        PhysicalFileAuthority.EnsureHandleMatchesExpectedPath(stream.SafeFileHandle,_path,"Main original owner guard");
    }
    public void Dispose()=>Interlocked.Exchange(ref _stream,null)?.Dispose();
}
