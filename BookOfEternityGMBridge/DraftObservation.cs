using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using BookOfEternityClient.Services.GmRuntime;
using Microsoft.Win32.SafeHandles;

namespace BookOfEternityGMBridge;

// Read-only evidence for an optional editor profile. No expected prompt, file
// mutation, model request, host construction or run authority belongs here.
internal sealed record DraftFileProof(string Path,string Bytes,uint DeviceMajor,uint DeviceMinor,
    ulong Inode,ulong Length,long ModifiedSeconds,uint ModifiedNanoseconds);
internal static class DraftObservation
{
    internal static async Task<int> RunAsync(string[] args)
    {
        try {
            if(args.Length!=2)throw new InvalidDataException("Observer requires exactly one actual editor file.");
            var directory=Environment.GetEnvironmentVariable("BOE_DRAFT_DIRECTORY")??"";
            var binding=Environment.GetEnvironmentVariable("BOE_DRAFT_BINDING")??"";
            var endpoint=Environment.GetEnvironmentVariable("BOE_DRAFT_PIPE")??"";
            if(binding.Length is <1 or >128 || !Path.IsPathFullyQualified(endpoint))throw new InvalidDataException("Original observer binding/absolute endpoint unavailable.");
            var proof=Read(directory,args[1]);
            using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var pipe=new NamedPipeClientStream(".",endpoint,PipeDirection.InOut,PipeOptions.Asynchronous);
            await pipe.ConnectAsync(deadline.Token);
            await MainOperationReader.WriteAsync(pipe,new {command="observeDraft",binding,path=proof.Path},deadline.Token);
            var reader=new MainOperationReader(pipe);
            var hello=await reader.ReadAsync<ObserverAck>(deadline.Token)??throw new IOException("Original operation connection closed.");
            if(!hello.Ok || string.IsNullOrEmpty(hello.Nonce))throw new IOException("Original operation refused observer.");
            await MainOperationReader.WriteAsync(pipe,new {nonce=hello.Nonce,path=proof.Path,bytes=proof.Bytes,
                deviceMajor=proof.DeviceMajor,deviceMinor=proof.DeviceMinor,inode=proof.Inode,length=proof.Length,
                modifiedSeconds=proof.ModifiedSeconds,modifiedNanoseconds=proof.ModifiedNanoseconds},deadline.Token,100000);
            var ack=await reader.ReadAsync<ObserverAck>(deadline.Token)??throw new IOException("Observer acknowledgement lost.");
            if(!ack.Ok)throw new IOException("Original operation rejected draft evidence.");
            return 0;
        } catch(Exception ex) { Console.Error.WriteLine("Read-only draft observation refused: "+ex.GetType().Name);return 2; }
    }
    internal sealed record ObserverAck(bool Ok,string? Nonce=null);

    internal static DraftFileProof Read(string directory,string path)
    {
        if(!OperatingSystem.IsLinux() || !Path.IsPathFullyQualified(directory) || !Path.IsPathFullyQualified(path) ||
            directory!=Path.GetFullPath(directory) || path!=Path.GetFullPath(path) || Path.GetDirectoryName(path)!=directory ||
            !Regex.IsMatch(Path.GetFileName(path),@"\A[0-9]{1,20}\.md\z",RegexOptions.CultureInvariant))
            throw new InvalidDataException("Actual editor file is outside the allowed direct temporary directory.");
        // Walk only this allowed directory's ancestors using original directory
        // descriptors; never follow a renamed/symlinked ancestor into another tree.
        using var parent=OpenDirectory(directory);
        using var handle=Handle(OpenAt(parent.DangerousGetHandle().ToInt32(),Path.GetFileName(path),0x20000|0x80000|0x800)); // NOFOLLOW,CLOEXEC,NONBLOCK,READONLY
        var before=Stat(handle);
        if((before.Mode&0xf000)!=0x8000 || before.Links!=1 || before.Size>65536)throw new InvalidDataException("Editor file is not bounded regular single-link data.");
        using var stream=new FileStream(handle,FileAccess.Read,4096,false);
        byte[] ReadBounded() {
            using var bytes=new MemoryStream();var block=new byte[4096];int count;
            while((count=stream.Read(block))!=0){if(bytes.Length+count>65536)throw new InvalidDataException("Editor file exceeds bound.");bytes.Write(block,0,count);}
            return bytes.ToArray();
        }
        var first=ReadBounded();_ = new UTF8Encoding(false,true).GetString(first);
        stream.Position=0;var second=ReadBounded();var after=Stat(handle);
        if(!first.AsSpan().SequenceEqual(second) || !Same(before,after) || (ulong)first.Length!=before.Size)
            throw new IOException("Actual draft changed during observation.");
        // Reopen the original name against the same directory descriptor to
        // refuse path replacement as well as changes to the held file.
        using var current=Handle(OpenAt(parent.DangerousGetHandle().ToInt32(),Path.GetFileName(path),0x20000|0x80000|0x800));
        if(!Same(after,Stat(current)))throw new IOException("Actual editor path identity changed.");
        return new(path,Convert.ToBase64String(first),after.DevMajor,after.DevMinor,after.Inode,after.Size,after.MtimeSeconds,after.MtimeNanos);
    }
    private static bool Same(Status a,Status b)=>a.Mode==b.Mode && a.Links==b.Links && a.Inode==b.Inode && a.Size==b.Size &&
        a.DevMajor==b.DevMajor && a.DevMinor==b.DevMinor && a.MtimeSeconds==b.MtimeSeconds && a.MtimeNanos==b.MtimeNanos;
    private static SafeFileHandle OpenDirectory(string directory)
    {
        var current=Handle(OpenAt(-100,"/",0x10000|0x20000|0x80000));
        try {foreach(var part in directory.Split('/',StringSplitOptions.RemoveEmptyEntries)) {
            var next=Handle(OpenAt(current.DangerousGetHandle().ToInt32(),part,0x10000|0x20000|0x80000));current.Dispose();current=next;
        } return current;}catch{current.Dispose();throw;}
    }
    private static SafeFileHandle Handle(int fd)=>fd<0?throw new IOException("Cannot acquire allowed read-only descriptor.",new Win32Exception(Marshal.GetLastPInvokeError())):new(new IntPtr(fd),true);
    private static Status Stat(SafeFileHandle h)
    {
        const uint mask=0x345; // TYPE,NLINK,MTIME,INO,SIZE
        if(Statx(h.DangerousGetHandle().ToInt32(),"",0x1100,mask,out var s)!=0 || (s.Mask&mask)!=mask)
            throw new IOException("Actual editor identity metadata unavailable.");
        return s;
    }
    // Kernel UAPI statx has a fixed 256-byte ABI, independent of libc stat layout.
    [StructLayout(LayoutKind.Explicit,Size=256)] private struct Status {
        [FieldOffset(0)]internal uint Mask;[FieldOffset(16)]internal uint Links;[FieldOffset(28)]internal ushort Mode;
        [FieldOffset(32)]internal ulong Inode;[FieldOffset(40)]internal ulong Size;
        [FieldOffset(112)]internal long MtimeSeconds;[FieldOffset(120)]internal uint MtimeNanos;
        [FieldOffset(136)]internal uint DevMajor;[FieldOffset(140)]internal uint DevMinor;
    }
    [DllImport("libc",EntryPoint="openat",SetLastError=true)]private static extern int OpenAt(int directory,string path,int flags);
    [DllImport("libc",EntryPoint="statx",SetLastError=true)]private static extern int Statx(int directory,string path,int flags,uint mask,out Status status);
}
