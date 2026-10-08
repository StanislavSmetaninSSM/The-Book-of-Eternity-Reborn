using System.IO.Pipes;
using System.Reflection;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

// Body-required native smoke recipe: real WMI, suspended ConPTY/Job, original
// Bridge pipe/status/participating write/close/shutdown. Empty manual CLI; no provider.
public sealed class GmBridgeWindowsOriginalHostTests
{
    [Theory]
    [InlineData("ordinary")]
    [InlineData("extended")]
    [InlineData("drive-case")]
    public async Task NativeOriginalHostPublishesStatusAndRetiresItsExactRun(string spelling)
    {
        Assert.True(OperatingSystem.IsWindows(),"Actual Windows bodies are required; no platform-return PASS.");
        var root=Path.Combine(Path.GetTempPath(),"boe-bridge-native-"+Guid.NewGuid().ToString("N"));
        var session=Path.Combine(root,"game_session");Directory.CreateDirectory(session);
        File.WriteAllText(Path.Combine(session,"config.json"),JsonSerializer.Serialize(new {GmCliLaunchCommand="",GmWorkerBridgeProfiles=Array.Empty<object>()}));
        var admittedRoot=spelling switch {
            "extended"=>PhysicalFileAuthority.ToWindowsExtendedPath(root),
            "drive-case"=>(char.IsUpper(root[0])?char.ToLowerInvariant(root[0]):char.ToUpperInvariant(root[0]))+root[1..],
            _=>root
        };
        if(spelling=="drive-case")Assert.NotEqual(root,admittedRoot);
        var type=GmWindowsShellResolutionTests.LoadBridge().GetType("BookOfEternityGMBridge.BridgeHost",true)!;
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        var pipe="boe-native-main-"+Guid.NewGuid().ToString("N");
        var host=Activator.CreateInstance(type,flags,null,[Path.Combine(admittedRoot,"game_session"),pipe],null)!;
        object? Field(string name)=>type.GetField(name,flags)!.GetValue(host);
        Task Call(string name)=>(Task)type.GetMethod(name,flags)!.Invoke(host,null)!;
        var statusReached=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var publications=0;var closes=0;GmSessionRunCoordinator? original=null;IOwnedTerminalSession? terminal=null;
        type.GetField("BeforeStatusPublication",flags)!.SetValue(host,(Func<Task>)(()=> {
            original=(GmSessionRunCoordinator)Field("_mainRun")!;terminal=(IOwnedTerminalSession)Field("_pty")!;
            Assert.Equal(GmSessionRunDisposition.Running,original.Record!.Disposition);
            Assert.Equal(GmSessionRunBackend.WindowsJob,original.Identity.Backend);
            Assert.Equal(original.Identity.RunId,terminal.Identity.RunId);
            Assert.False(terminal.RootExited.IsCompleted);Assert.False(terminal.AuthorityLost.IsCompleted);
            Interlocked.Increment(ref publications);statusReached.TrySetResult();return Task.CompletedTask;
        }));
        type.GetField("BeforeMainCloseReply",flags)!.SetValue(host,(Func<MainOperationClose,Task>)(receipt=> {
            Assert.True(GmSessionRunValidation.IdentityMatches(original!.Identity,receipt.Identity));
            Interlocked.Increment(ref closes);return Task.CompletedTask;
        }));
        var run=Call("RunAsync");
        try {
            await statusReached.Task.WaitAsync(TimeSpan.FromSeconds(20));
            var first=(TaskCompletionSource)Field("_firstStatus")!;await first.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Null(Field("_productionConfig")); // Existing Windows worker capability is not Linux-disabled.
            var files=new FileSystemManager(root,NullLogger<FileSystemManager>.Instance);
            using var status=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(session,"game_state/control/gm_bridge_status.json")));
            Assert.Equal(original!.Identity.RunId,status.RootElement.GetProperty("terminalRunId").GetString());
            Assert.False(status.RootElement.GetProperty("ready").GetBoolean());
            await SessionOperationContext.RunParticipatingCurrentSessionAsync(files,()=>files.WriteFileAtomicBytesAsync("game_state/control/native-original.bin",[17,42,255]));
            Assert.Equal(1,closes);Assert.Equal(new byte[]{17,42,255},File.ReadAllBytes(Path.Combine(session,"game_state/control/native-original.bin")));
            using var connection=new NamedPipeClientStream(".",pipe,PipeDirection.InOut,PipeOptions.Asynchronous);
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));await connection.ConnectAsync(timeout.Token);
            await MainOperationReader.WriteAsync(connection,new {command="shutdown",rootKey=root,expectedMainIdentity=original.Identity},timeout.Token);
            using var reader=new StreamReader(connection,Encoding.UTF8,leaveOpen:true);
            using var reply=JsonDocument.Parse((await reader.ReadLineAsync(timeout.Token))!);
            Assert.True(reply.RootElement.GetProperty("ok").GetBoolean());
            await run.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(GmSessionRunDisposition.Stopped,original.Record!.Disposition);
            Assert.False(original.RetainsAuthority);Assert.True(terminal!.RootExited.IsCompleted);
            Assert.True(publications>0);Assert.Null(Field("_mainRun"));Assert.Null(Field("_pty"));
            var persisted=GmSessionRunRecordCodec.Decode(GmSessionRunPersistence.Read(root)!);
            Assert.Equal(GmSessionRunDisposition.Stopped,persisted.Disposition);
            Assert.True(GmSessionRunValidation.IdentityMatches(original.Identity,persisted.Identity));
        }
        finally {
            // Retain/join this original host and terminal; no PID reopen or cleanup-by-tree substitute.
            ((CancellationTokenSource)Field("_cts")!).Cancel();
            try {await run;} finally {((IDisposable)host).Dispose();}
            Directory.Delete(root,true);
        }
    }
}
