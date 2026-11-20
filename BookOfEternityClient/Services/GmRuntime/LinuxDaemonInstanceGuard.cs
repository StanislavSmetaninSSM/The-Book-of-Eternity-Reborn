using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using Microsoft.Win32.SafeHandles;

namespace BookOfEternityClient.Services.GmRuntime;

/// <summary>Linux-x64 trusted local daemon exclusion only; never canonical publication authority.</summary>
public sealed partial class LinuxDaemonInstanceGuard : IDisposable
{
    private const string FileName = "gm-daemon-instance.lock";
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly string rootPath;
    private readonly SafeFileHandle root, runtime, locks;
    private readonly FileStream stream;
    private byte[] identityBytes;
    private bool settled;
    private bool disposed;
    public string OwnerId { get; }
    public string SessionPath { get; }

    private LinuxDaemonInstanceGuard(string path, SafeFileHandle root, SafeFileHandle runtime,
        SafeFileHandle locks, FileStream stream, string owner, byte[] bytes)
    {
        rootPath=path;this.root=root;this.runtime=runtime;this.locks=locks;this.stream=stream;
        OwnerId=owner;SessionPath=Path.Combine(path,"game_session");identityBytes=bytes;
    }

    public static LinuxDaemonInstanceGuard Acquire(string basePath, string? priorStatusJson) =>
        AcquireWithStatusReader(basePath, () => priorStatusJson);

    // The supplied ordinary admitted reader executes only after exclusive lock acquisition.
    public static LinuxDaemonInstanceGuard AcquireWithStatusReader(string basePath, Func<object?> readStatus)
    {
        LinuxBridgeFifoNative.RequirePlatform();
        ArgumentNullException.ThrowIfNull(readStatus);
        var path=Path.GetFullPath(basePath).TrimEnd(Path.DirectorySeparatorChar);
        SafeFileHandle? root=null,runtime=null,locks=null,guard=null;FileStream? stream=null;
        try
        {
            var scope=new TrustedLocalFileScope([path]);scope.ValidateDirectory(path, false);
            root=LinuxBridgeFifoNative.OpenDirectory(path);
            runtime=LinuxBridgeFifoNative.OpenOrCreateGuardDirectory(root,".boe_runtime");
            locks=LinuxBridgeFifoNative.OpenOrCreateGuardDirectory(runtime,"locks");
            guard=LinuxBridgeFifoNative.OpenGuard(locks,FileName,out var created);
            stream=new FileStream(guard,FileAccess.ReadWrite,4096,false);guard=null;
            var session=Path.Combine(path,"game_session");
            var observed=readStatus();
            var prior=observed switch { null=>null, string text=>text, _=>throw new IOException("Invalid status-reader result.") };
            if(created)
            {
                if(prior is not null) throw new IOException("Fresh guard cannot adopt preexisting daemon status.");
            }
            else
            {
                var old=Parse(Read(stream));
                if(old.Count!=4 || !True(old,"settled") || Number(old,"version")!=1 || Text(old,"sessionPath")!=session ||
                    !Guid.TryParseExact(Text(old,"ownerId"),"N",out _)) throw new IOException("Invalid retained daemon guard.");
                if(prior is null) throw new IOException("Existing daemon guard lost its status.");
                var status=Parse(prior);
                if(!ExactStopped(status,session,Text(old,"ownerId")))
                    throw new IOException("Prior guarded daemon is not an exact settled stop.");
            }
            var owner=Guid.NewGuid().ToString("N");
            var bytes=Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {version=1,sessionPath=session,ownerId=owner,settled=false}));
            // No truncate-on-open, replacement or unlink. A partial failure is not repaired.
            stream.Position=0;stream.Write(bytes);stream.SetLength(bytes.Length);stream.Flush(true);
            var result=new LinuxDaemonInstanceGuard(path,root,runtime,locks,stream,owner,bytes);
            result.Validate();
            root=runtime=locks=null;stream=null;
            return result;
        }
        catch(Exception error) when(error is InvalidDataException or JsonException or ArgumentException or InvalidOperationException)
        { throw new IOException("Daemon singleton admission refused.",error); }
        finally { stream?.Dispose();guard?.Dispose();locks?.Dispose();runtime?.Dispose();root?.Dispose(); }
    }

    public void Validate()
    {
        if(disposed) throw new ObjectDisposedException(nameof(LinuxDaemonInstanceGuard));
        using var current=LinuxBridgeFifoNative.OpenDirectory(rootPath);
        if(LinuxBridgeFifoNative.Stat(current)!=LinuxBridgeFifoNative.Stat(root)) throw new IOException("Daemon root changed.");
        LinuxBridgeFifoNative.RequireNamedIdentity(root,".boe_runtime",runtime);
        LinuxBridgeFifoNative.RequireNamedIdentity(runtime,"locks",locks);
        LinuxBridgeFifoNative.RequireNamedIdentity(locks,FileName,stream.SafeFileHandle);
        LinuxBridgeFifoNative.ValidateGuard(stream.SafeFileHandle);
        if(!Encoding.UTF8.GetBytes(Read(stream)).AsSpan().SequenceEqual(identityBytes)) throw new IOException("Daemon guard identity changed.");
    }

    /// <summary>Records a caller-confirmed canonical terminal operation, not process exit or delivery.</summary>
    public void MarkSettled(string confirmedStatusJson)
    {
        Validate();
        if(settled || !ExactStopped(Parse(confirmedStatusJson),SessionPath,OwnerId))
            throw new IOException("Exact confirmed terminal status required.");
        var bytes=Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {version=1,sessionPath=SessionPath,ownerId=OwnerId,settled=true}));
        // No retry or rollback on partial/unknown write. A later independent owner
        // may admit only a complete exact pair, never a caller's success claim.
        stream.Position=0;stream.Write(bytes);stream.SetLength(bytes.Length);stream.Flush(true);
        identityBytes=bytes;
        Validate();settled=true;
    }

    private static bool ExactStopped(JsonObject status,string session,string owner) =>
        Number(status,"daemonGuardVersion")==1 && Text(status,"sessionPath")==session &&
        Text(status,"daemonGuardOwnerId")==owner && Text(status,"status")=="stopped" && True(status,"terminalSettled");
    private static bool True(JsonObject obj,string key) => obj[key] is JsonValue value && value.TryGetValue<bool>(out var flag) && flag;

    private static string Read(FileStream file)
    {
        if(file.Length is <1 or >4096) throw new IOException("Invalid guard identity size.");
        file.Position=0;var bytes=new byte[(int)file.Length];file.ReadExactly(bytes);
        return new UTF8Encoding(false,true).GetString(bytes);
    }
    private static JsonObject Parse(string text) => StrictJsonAuthority.Deserialize<JsonObject>(text,Json,"Daemon guarded status")
        ?? throw new IOException("Null daemon guarded status.");
    private static string Text(JsonObject obj,string key) => obj[key] is JsonValue value && value.TryGetValue<string>(out var text) &&
        !string.IsNullOrWhiteSpace(text) && text==text.Trim() ? text : throw new IOException("Missing exact daemon identity.");
    private static int Number(JsonObject obj,string key) => obj[key] is JsonValue value && value.TryGetValue<int>(out var number)
        ? number : throw new IOException("Missing daemon schema version.");
    public void Dispose()
    {
        if(disposed)return;disposed=true;
        try {stream.Dispose();} finally {try {locks.Dispose();} finally {try {runtime.Dispose();} finally {root.Dispose();}}}
    }
}
