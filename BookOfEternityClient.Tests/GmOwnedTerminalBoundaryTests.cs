using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmOwnedTerminalBoundaryTests
{
    [Fact]
    public void WindowsMainSource_HoldsJobBeforeReleaseAndOwnedPseudoConsoleClose()
    {
        var source=File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot,"BookOfEternityGMBridge/ConPtySession.cs"));
        Assert.Contains("CREATE_SUSPENDED",source);
        Assert.Contains("WindowsJobProcessTree",source);
        Assert.Contains("ResumeThread",source);
        Assert.DoesNotContain("Kill(entireProcessTree",source);
        Assert.Contains("_closeTask",source);
    }
    [Fact]
    public async Task ActualOwnedView_PartialUtf8AndUnsupportedVtCannotBorrowIdle()
    {
        await using var h=new OwnedHost();
        await h.Feed("\u001b[2J\u001b[HNEUTRAL READY\r\n> \r\n"); Assert.True(h.Reliable);
        await h.FeedBytes([0xf0]); Assert.False(h.Reliable);
        await h.FeedBytes([0x9f,0x98,0x80]); Assert.True(h.Reliable);
        await h.Feed("\u001b[31m"); Assert.False(h.Reliable);
        await h.Feed("\u001b[2"); Assert.False(h.Reliable);
        await h.Feed("J\u001b[HNEUTRAL READY\r\n> \r\n"); Assert.True(h.Reliable);
    }
    [Fact]
    public async Task ActualOwnedView_EraseWithoutHomeCannotCertifyIdle()
    {
        await using var h=new OwnedHost();
        await h.Feed("\u001b[2J\u001b[HNEUTRAL READY\r\n> \r\n");
        await h.Feed("\u001b[2JNEUTRAL READY\r\n> \r\n"); Assert.False(h.Reliable);
        await h.Feed("\u001b[2J\u001b[HNEUTRAL READY\r\n> \r\n"); Assert.True(h.Reliable);
    }
    [Fact]
    public async Task ActualOwnedKeyboard_PairsUnicodeScalarsBeforeWriting()
    {
        await using var h=new OwnedHost();
        var keys=Channel.CreateBounded<ConsoleKeyInfo?>(4);
        var keyboard=(Task)h.Invoke("PumpKeyboardAsync",h.Input,
            (Func<CancellationToken,ValueTask<ConsoleKeyInfo?>>)(t=>keys.Reader.ReadAsync(t)),CancellationToken.None)!;
        h.Set("_keyboardPumpTask",Task.WhenAll((Task)h.Get("_keyboardPumpTask")!,keyboard));
        await keys.Writer.WriteAsync(new ConsoleKeyInfo('\ud83d',ConsoleKey.NoName,false,false,false));
        await keys.Writer.WriteAsync(new ConsoleKeyInfo('\ude00',ConsoleKey.NoName,false,false,false));
        for(var i=0;i<100 && h.Terminal.Written.Length<4;i++)await Task.Delay(5);
        await h.Stop(); Assert.Equal(Encoding.UTF8.GetBytes("😀"),h.Terminal.Written.ToArray());
    }
    [Theory]
    [InlineData("uncertain")]
    [InlineData("incomplete")]
    [InlineData("retained")]
    [InlineData("foreign")]
    public async Task ActualOwnedStop_InvalidProofRetainsOriginalAuthority(string kind)
    {
        await using var h=new OwnedHost(); h.Terminal.ProofKind=kind;
        await Assert.ThrowsAsync<TimeoutException>(h.Stop);
        Assert.Same(h.Terminal,h.Get("_pty")); Assert.False(h.Terminal.Disposed);
        var task=h.Get("_terminalStopTask"); await Assert.ThrowsAsync<TimeoutException>(h.Stop); Assert.Same(task,h.Get("_terminalStopTask"));
        Assert.Throws<TargetInvocationException>(()=>h.Invoke("BeginInputLifetime",new MemoryStream(),new CancellationTokenSource()));
    }
    [Fact]
    public async Task ActualOwnedStop_ScopedProofStillRequiresActualOutputEof()
    {
        await using var h=new OwnedHost(); h.Terminal.HoldEof=true;
        var stop=h.Stop(); await h.Terminal.StopEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(30); Assert.False(stop.IsCompleted); Assert.Same(h.Terminal,h.Get("_pty")); Assert.False(h.Terminal.Disposed);
        h.Terminal.Complete(); await stop.WaitAsync(TimeSpan.FromSeconds(3)); Assert.True(h.Terminal.Disposed); Assert.Null(h.Get("_pty"));
    }
    [Fact]
    public async Task ActualOwnedStop_OutputTimeoutIsAbsorbingWithOriginalOwner()
    {
        await using var h=new OwnedHost();h.Terminal.HoldEof=true;
        await Assert.ThrowsAsync<TimeoutException>(h.Stop);
        Assert.Same(h.Terminal,h.Get("_pty"));Assert.False(h.Terminal.Disposed);
        h.Terminal.Complete();await Assert.ThrowsAsync<TimeoutException>(h.Stop);
        Assert.Same(h.Terminal,h.Get("_pty"));Assert.False(h.Terminal.Disposed);
    }
    private sealed class OwnedHost : IAsyncDisposable
    {
        private readonly Type _type;
        private readonly object _host;
        private readonly string _scratch=Path.Combine(Path.GetTempPath(),"neutral-controlled-"+Guid.NewGuid().ToString("N"));
        internal readonly ControlledTerminal Terminal=new();
        internal object Input=>Get("_inputLifetime")!;
        internal bool Reliable=>(bool)Invoke("CaptureTerminalView")!.GetType().GetProperty("Reliable")!.GetValue(Invoke("CaptureTerminalView"))!;
        internal OwnedHost()
        {
            var configuration=new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
            _type=Assembly.LoadFrom(Path.Combine(TestRepoPaths.RepoRoot,"BookOfEternityGMBridge/bin",configuration,"net8.0/BookOfEternityGMBridge.dll")).GetType("BookOfEternityGMBridge.BridgeHost",true)!;
            _host=Activator.CreateInstance(_type,[_scratch,"unused-"+Guid.NewGuid().ToString("N")])!;
            Invoke("AttachOwnedTerminal",Terminal,new MemoryStream());
        }
        internal object? Get(string n)=>_type.GetField(n,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(_host);
        internal void Set(string n,object? v)=>_type.GetField(n,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(_host,v);
        internal object? Invoke(string n,params object?[] a)=>_type.GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(_host,a);
        internal Task Stop()=>(Task)Invoke("StopShellAsync")!;
        internal Task Feed(string s)=>FeedBytes(Encoding.UTF8.GetBytes(s));
        internal async Task FeedBytes(byte[] bytes)
        {
            var version=(long)Get("_outputVersion")!;await Terminal.Frames.Writer.WriteAsync(bytes);
            for(var i=0;i<100 && (long)Get("_outputVersion")!<=version;i++)await Task.Delay(5);
            Assert.True((long)Get("_outputVersion")!>version,"Actual original output pump did not consume the frame.");
        }
        public async ValueTask DisposeAsync()
        {
            Terminal.Complete(); try{await Stop();}catch(TimeoutException) { }
            ((IDisposable)_host).Dispose();
            await ((Task)Get("_outputPumpTask")! ?? Task.CompletedTask);
            Terminal.Written.Dispose();Directory.Delete(_scratch,true);
        }
    }
    private sealed class ControlledTerminal : IOwnedTerminalSession
    {
        internal Channel<byte[]> Frames=Channel.CreateBounded<byte[]>(16);
        internal MemoryStream Written=new();
        private readonly Stream _output;
        internal bool HoldEof,Disposed;
        internal string ProofKind="valid";
        internal TaskCompletionSource StopEntered=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal ControlledTerminal()=>_output=new FrameStream(Frames.Reader);
        internal void Complete()=>Frames.Writer.TryComplete();
        public TerminalIdentity Identity {get;}=new(Guid.NewGuid().ToString("N"),"controlled","neutral-only",1);
        public Stream InputWriter=>Written;
        public Stream OutputReader=>_output;
        public Task<TerminalRootExit> RootExited {get;}=new TaskCompletionSource<TerminalRootExit>().Task;
        public Task<string> AuthorityLost {get;}=new TaskCompletionSource<string>().Task;
        public ValueTask ResizeAsync(TerminalSize s,CancellationToken t)=>ValueTask.CompletedTask;
        public Task<TerminalStopEvidence> StopAndObserveAsync(CancellationToken t)
        {
            StopEntered.TrySetResult();if(!HoldEof)Complete();
            return Task.FromResult(new TerminalStopEvidence(ProofKind=="foreign"?Identity with{RunId="other"}:Identity,
                ProofKind=="uncertain"?GmWorkerStopState.Uncertain:GmWorkerStopState.StoppedWithinScope,"controlled",ProofKind!="incomplete",ProofKind=="retained"));
        }
        public ValueTask DisposeAsync(){Disposed=true;return ValueTask.CompletedTask;}
    }
    private sealed class FrameStream(ChannelReader<byte[]> reader) : Stream
    {
        public override bool CanRead=>true;public override bool CanSeek=>false;public override bool CanWrite=>false;
        public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override async ValueTask<int> ReadAsync(Memory<byte> bytes,CancellationToken token=default)
        { if(!await reader.WaitToReadAsync(token))return 0;if(!reader.TryRead(out var frame))throw new IOException();frame.CopyTo(bytes);return frame.Length; }
        public override int Read(byte[] b,int o,int c)=>throw new NotSupportedException();public override void Write(byte[] b,int o,int c)=>throw new NotSupportedException();
        public override void Flush(){}public override long Seek(long n,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long n)=>throw new NotSupportedException();
    }
}
