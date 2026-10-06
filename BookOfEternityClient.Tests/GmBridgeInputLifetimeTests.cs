using System.Reflection;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Actual bridge consumer; no native terminal, provider or process is created.</summary>
public sealed class GmBridgeInputLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualWriter_PreservesUnicodeMultilineAndAppendEnter(bool append)
    {
        await using var host = new HostFixture();
        using var stream = new ControlledStream();
        var binding = host.Attach(stream);
        const string text = "Ж😀e\u0301\r\n\u001b[200~x\u001b[201~";
        await host.Write(binding, text, append);
        Assert.Equal(Encoding.UTF8.GetBytes(text + (append ? "\r" : "")), stream.Bytes);
        Assert.Equal(1, stream.Writes);
        Assert.Equal(1, stream.Flushes);
        Assert.False(stream.Disposed);
    }

    [Theory]
    [InlineData("caller")]
    [InlineData("shell")]
    [InlineData("host")]
    [InlineData("revoke")]
    public async Task ActualWriter_QueuedCancellationOrRevocationWritesNothing(string source)
    {
        await using var host = new HostFixture();
        using var stream = new ControlledStream();
        using var caller = new CancellationTokenSource();
        var binding = host.Attach(stream);
        var gate = host.WriteGate;
        await gate.WaitAsync();
        var write = host.Write(binding, "never", false, caller.Token);
        Assert.False(write.IsCompleted);
        if (source == "caller") caller.Cancel();
        else if (source == "shell") host.ShellTokenSource.Cancel();
        else if (source == "host") host.HostTokenSource.Cancel();
        else host.Revoke(binding);
        gate.Release();
        var error = await Capture(write);
        Assert.Empty(stream.Bytes);
        Assert.Equal(0, stream.Writes);
        Assert.NotNull(error);
        Assert.Null(host.InputError);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualWriter_StaleOriginCannotWriteToReplacementEvenWithReusedStream(bool reuse)
    {
        await using var host = new HostFixture();
        using var a = new ControlledStream();
        using var b = new ControlledStream();
        var first = host.Attach(a);
        await host.Stop();
        var next = host.Attach(reuse ? a : b);
        var stale = await Capture(host.Write(first, "stale", true));
        Assert.NotNull(stale);
        Assert.Empty(a.Bytes);
        Assert.Empty(b.Bytes);
        await host.Write(next, "fresh", false);
        Assert.Equal(Encoding.UTF8.GetBytes("fresh"), (reuse ? a : b).Bytes);
    }

    [Theory]
    [InlineData("write")]
    [InlineData("flush")]
    [InlineData("cancel-write")]
    [InlineData("cancel-flush")]
    public async Task ActualWriter_StartedFaultOrIgnoredCancellationRetainsUncertainty(string phase)
    {
        await using var host = new HostFixture();
        using var cancel = new CancellationTokenSource();
        using var stream = new ControlledStream
        {
            FailWrite = phase == "write", FailFlush = phase == "flush",
            OnWrite = phase == "cancel-write" ? cancel.Cancel : null,
            OnFlush = phase == "cancel-flush" ? cancel.Cancel : null
        };
        var binding = host.Attach(stream);
        var error = await Capture(host.Write(binding, "abc", false, cancel.Token));
        Assert.NotNull(error);
        Assert.Equal("PtyInputWriteException", error!.GetType().Name);
        Assert.NotEmpty(stream.Bytes);
        Assert.NotNull(host.InputError);
        Assert.Contains("may have been delivered", host.InputError, StringComparison.Ordinal);
        var before = stream.Bytes;
        Assert.NotNull(await Capture(host.Write(binding, "retry", false)));
        Assert.Equal(before, stream.Bytes);
        Assert.Equal(1, stream.Writes);
    }

    [Fact]
    public async Task ActualWriter_CancelBeforeEntryIsZeroBytesAndDoesNotPoisonActiveBinding()
    {
        await using var host = new HostFixture();
        using var stream = new ControlledStream();
        using var cancel = new CancellationTokenSource();
        var binding = host.Attach(stream);
        cancel.Cancel();
        Assert.NotNull(await Capture(host.Write(binding, "never", false, cancel.Token)));
        Assert.Empty(stream.Bytes);
        Assert.Null(host.InputError);
        await host.Write(binding, "next", false);
        Assert.Equal(Encoding.UTF8.GetBytes("next"), stream.Bytes);
    }

    [Fact]
    public async Task ActualStop_RetainsStartedAndQueuedWritesUntilSettledBeforeNewBinding()
    {
        await using var host = new HostFixture();
        using var a = new ControlledStream { BlockWrite = true, IgnoreCancellation = true };
        using var b = new ControlledStream();
        var binding = host.Attach(a);
        var first = host.Write(binding, "one", false);
        await a.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var queued = host.Write(binding, "two", false);
        var stop = host.Stop();
        var blocked = !stop.IsCompleted;
        var attachError = Record.Exception(() => host.Attach(b));
        a.Release.TrySetResult(true);
        var firstError = await Capture(first);
        var queuedError = await Capture(queued);
        await stop.WaitAsync(TimeSpan.FromSeconds(8));
        Assert.True(blocked, "Stop must observe actual in-flight I/O even when native PTY is null.");
        Assert.NotNull(attachError);
        Assert.Equal("PtyInputWriteException", firstError?.GetType().Name);
        Assert.NotNull(queuedError);
        Assert.Equal(Encoding.UTF8.GetBytes("one"), a.Bytes);
        var next = host.Attach(b);
        Assert.Null(host.InputError);
        await host.Write(next, "fresh", false);
        Assert.Equal(Encoding.UTF8.GetBytes("fresh"), b.Bytes);
    }

    [Fact]
    public async Task ActualStop_TimeoutRetainsSameDrainAndBlocksReplacementUntilLateCompletion()
    {
        await using var host = new HostFixture();
        using var a = new ControlledStream { BlockWrite = true, IgnoreCancellation = true };
        using var b = new ControlledStream();
        var binding = host.Attach(a);
        var write = host.Write(binding, "partial", false);
        await a.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var error = await Capture(host.Stop(), 9);
        var rejected = Record.Exception(() => host.Attach(b));
        var retryStop = host.Stop();
        var retained = !retryStop.IsCompleted;
        a.Release.TrySetResult(true);
        await Capture(write);
        await retryStop.WaitAsync(TimeSpan.FromSeconds(8));
        Assert.IsType<TimeoutException>(error);
        Assert.NotNull(rejected);
        Assert.True(retained, "A repeated stop must observe the retained actual task.");
        await host.Write(host.Attach(b), "B", false);
        Assert.Equal(Encoding.UTF8.GetBytes("B"), b.Bytes);
    }

    [Fact]
    public async Task ActualDispose_ClosesAdmissionAndDoesNotDisposeGateUnderUnsettledWrite()
    {
        await using var host = new HostFixture();
        using var stream = new ControlledStream { BlockWrite = true, IgnoreCancellation = true };
        using var b = new ControlledStream();
        var binding = host.Attach(stream);
        var write = host.Write(binding, "A", false);
        await stream.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Run(host.DisposeHost).WaitAsync(TimeSpan.FromSeconds(9));
        var rejected = Record.Exception(() => host.Attach(b));
        stream.Release.TrySetResult(true);
        var error = await Capture(write);
        host.DisposeHost();
        Assert.NotNull(rejected);
        Assert.Equal("PtyInputWriteException", error?.GetType().Name);
        Assert.NotNull(host.InputError);
        Assert.Equal(1, stream.Writes);
        Assert.Empty(b.Bytes);
    }

    [Fact]
    public async Task ActualStop_ConcurrentStopsJoinOneRetiringLifetime()
    {
        await using var host = new HostFixture();
        using var stream = new ControlledStream { BlockFlush = true, IgnoreCancellation = true };
        var binding = host.Attach(stream);
        var write = host.Write(binding, "A", false);
        await stream.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var stop1 = host.Stop();
        var stop2 = host.Stop();
        var bothWaiting = !stop1.IsCompleted && !stop2.IsCompleted;
        stream.Release.TrySetResult(true);
        await Capture(write);
        await Task.WhenAll(stop1, stop2).WaitAsync(TimeSpan.FromSeconds(8));
        Assert.True(bothWaiting);
        Assert.Equal(1, stream.Writes);
    }

    [Theory]
    [InlineData('a', ConsoleKey.A, false, "a")]
    [InlineData('\r', ConsoleKey.Enter, false, "\r")]
    [InlineData('\0', ConsoleKey.C, true, "\u0003")]
    [InlineData('\0', ConsoleKey.UpArrow, false, "\u001b[A")]
    public async Task ActualKeyboard_PreservesExistingKeyBytes(char keyChar, ConsoleKey key, bool control, string expected)
    {
        await using var host = new HostFixture();
        using var stream = new ControlledStream();
        var binding = host.Attach(stream);
        var reads = 0;
        async ValueTask<ConsoleKeyInfo?> Read(CancellationToken token)
        {
            if (Interlocked.Increment(ref reads) == 1) return new ConsoleKeyInfo(keyChar, key, false, false, control);
            await Task.Delay(Timeout.Infinite, token);
            return null;
        }
        var pump = host.Keyboard(binding, Read);
        await stream.Flushed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        host.ShellTokenSource.Cancel();
        await Capture(pump);
        Assert.Equal(Encoding.UTF8.GetBytes(expected), stream.Bytes);
        Assert.Equal(1, stream.Writes);
    }

    [Fact]
    public async Task ActualKeyboard_NullPtyStopWaitsForLateKeyReadBeforeReplacementConsumesKeys()
    {
        await using var host = new HostFixture();
        using var a = new ControlledStream();
        using var b = new ControlledStream();
        var binding = host.Attach(a);
        var entered = Signal();
        var lateKey = new TaskCompletionSource<ConsoleKeyInfo?>(TaskCreationOptions.RunContinuationsAsynchronously);
        async ValueTask<ConsoleKeyInfo?> Read(CancellationToken token)
        {
            entered.TrySetResult(true);
            return await lateKey.Task; // Deliberately ignores cancellation until the owned read finishes.
        }
        var pump = host.Keyboard(binding, Read);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        host.Revoke(binding); // Same managed revocation seam used by root-exit handling.
        var stop = host.Stop();
        var waiting = !stop.IsCompleted;
        var reject = Record.Exception(() => host.Attach(b));
        lateKey.TrySetResult(new ConsoleKeyInfo('x', ConsoleKey.X, false, false, false));
        // Also release the original token in the old-source adapter so a failing RED fixture cannot spin.
        host.CancelAllShells();
        await Capture(pump);
        await stop.WaitAsync(TimeSpan.FromSeconds(8));
        Assert.True(waiting);
        Assert.NotNull(reject);
        Assert.Empty(a.Bytes);
        Assert.Empty(b.Bytes);
        var next = host.Attach(b);
        var calls = 0;
        async ValueTask<ConsoleKeyInfo?> ReadB(CancellationToken token)
        {
            if (Interlocked.Increment(ref calls) == 1) return new ConsoleKeyInfo('b', ConsoleKey.B, false, false, false);
            await Task.Delay(Timeout.Infinite, token);
            return null;
        }
        var nextPump = host.Keyboard(next, ReadB);
        await b.Flushed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        host.ShellTokenSource.Cancel();
        await Capture(nextPump);
        Assert.Equal(Encoding.UTF8.GetBytes("b"), b.Bytes);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task ActualKeyboard_WriteFailureIsRecordedAndPumpStops()
    {
        await using var host = new HostFixture();
        using var stream = new ControlledStream { FailFlush = true };
        var binding = host.Attach(stream);
        var reads = 0;
        async ValueTask<ConsoleKeyInfo?> Read(CancellationToken token)
        {
            if (Interlocked.Increment(ref reads) == 1) return new ConsoleKeyInfo('a', ConsoleKey.A, false, false, false);
            await Task.Delay(Timeout.Infinite, token);
            return null;
        }
        var pump = host.Keyboard(binding, Read);
        await stream.Flushed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var outcome = await Capture(pump);
        Assert.Null(outcome);
        Assert.Equal(1, reads);
        Assert.NotNull(host.InputError);
        Assert.Equal(1, stream.Writes);
    }

    [Fact]
    public async Task ActualAssemblyAndPortablePdbMatchProductionSource()
    {
        await using var host = new HostFixture();
        Assert.Equal(Path.GetFullPath(HostFixture.BridgePath), Path.GetFullPath(host.Type.Assembly.Location));
        Assert.NotEqual(typeof(GmBridgeInputLifetimeTests).Assembly, host.Type.Assembly);
        using var stream = File.OpenRead(Path.ChangeExtension(HostFixture.BridgePath, ".pdb"));
        using var provider = MetadataReaderProvider.FromPortablePdbStream(stream);
        var metadata = provider.GetMetadataReader();
        foreach (var name in new[] { "Program.cs", "BridgeHost.PromptDispatch.cs" })
        {
            var doc = Assert.Single(metadata.Documents.Select(metadata.GetDocument), d => metadata.GetString(d.Name).Replace('\\', '/').EndsWith("BookOfEternityGMBridge/" + name, StringComparison.Ordinal));
            Assert.Equal(SHA256.HashData(File.ReadAllBytes(Path.Combine(HostFixture.RepoRoot, "BookOfEternityGMBridge/" + name))), metadata.GetBlobBytes(doc.Hash));
        }
        Assert.Null(host.Get("_pty"));
    }

    [Theory]
    [InlineData(false, "Failed")]
    [InlineData(true, "Completed")]
    public async Task ActualDispatchCompletion_ReflectsActualOperationSuccess(bool success, string expected)
    {
        await using var host = new HostFixture();
        using var stream = new ControlledStream();
        var binding = host.Attach(stream);
        host.Complete(binding, success);
        Assert.Equal(expected, host.Status("LastPromptDispatchState"));
    }

    [Fact]
    public async Task ActualLifetime_LateRevokeAndCompletionCannotChangeReplacement()
    {
        await using var host = new HostFixture();
        using var a = new ControlledStream();
        using var b = new ControlledStream();
        var first = host.Attach(a);
        await host.Stop();
        var next = host.Attach(b);
        host.Complete(next, true);
        host.Revoke(first);
        host.Complete(first, false);
        Assert.NotNull(await Capture(host.Write(first, "old", false)));
        Assert.Null(host.InputError);
        Assert.Equal("Completed", host.Status("LastPromptDispatchState"));
        await host.Write(next, "new", false);
        Assert.Equal(Encoding.UTF8.GetBytes("new"), b.Bytes);
    }

    [Fact]
    public async Task ActualStop_AwaitsCancellationCallbacksThatReenterTheHost()
    {
        await using var host = new HostFixture();
        using var stream = new ControlledStream();
        var binding = host.Attach(stream);
        var seen = Signal();
        using var registration = host.ShellTokenSource.Token.Register(() =>
        {
            // Re-enter the same state lock; callback must not run inline under it.
            host.Revoke(binding);
            seen.TrySetResult(true);
        });
        await host.Stop().WaitAsync(TimeSpan.FromSeconds(8));
        Assert.True(seen.Task.IsCompletedSuccessfully);
        Assert.Empty(stream.Bytes);
    }

    [Theory]
    [InlineData("shell", true)]
    [InlineData("host", false)]
    [InlineData("io", true)]
    public async Task ActualRequestBoundary_OnlyHostCancellationStopsServing(string source, bool shouldContinue)
    {
        await using var host = new HostFixture();
        using var response = new MemoryStream();
        using var shell = new CancellationTokenSource();
        shell.Cancel();
        if (source == "host") host.HostTokenSource.Cancel();
        Func<Task> request = () => Task.FromException(source == "io"
            ? new IOException("controlled failure")
            : new OperationCanceledException(source == "host" ? host.HostTokenSource.Token : shell.Token));
        Assert.Equal(shouldContinue, await host.ConnectedRequest(response, request));
        if (shouldContinue)
        {
            response.Position = 0;
            using var reader = new StreamReader(response, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            var reply = await reader.ReadToEndAsync();
            using var json = System.Text.Json.JsonDocument.Parse(reply);
            Assert.False(json.RootElement.GetProperty("ok").GetBoolean());
            Assert.NotEmpty(json.RootElement.GetProperty("error").GetString()!);
            var handled = false;
            using var laterResponse = new MemoryStream();
            Assert.True(await host.ConnectedRequest(laterResponse, () => { handled = true; return Task.CompletedTask; }));
            Assert.True(handled);
            Assert.False(host.HostTokenSource.IsCancellationRequested);
        }
        else Assert.Empty(response.ToArray());
    }

    [Fact]
    public void ProductionOriginsAndRetirementConsumeTheTestedLifetimeMethods()
    {
        var source = File.ReadAllText(Path.Combine(HostFixture.RepoRoot, "BookOfEternityGMBridge/Program.cs"));
        Assert.Contains("BeginInputLifetime(pty.InputWriter, shellLoopCts)", source, StringComparison.Ordinal);
        var server = source[source.IndexOf("private async Task RunServerLoopAsync", StringComparison.Ordinal)..source.IndexOf("private async Task<bool> ProcessConnectedRequestAsync", StringComparison.Ordinal)];
        Assert.Contains("await ProcessConnectedRequestAsync(server, async () =>", server, StringComparison.Ordinal);
        Assert.Contains("var response = await HandleRequestAsync(request);", server, StringComparison.Ordinal);
        Assert.Contains("}, deadline.Token);", server, StringComparison.Ordinal);
        Assert.Contains("finally { await Task.WhenAll(peers); }", server, StringComparison.Ordinal);
        Assert.Contains("PumpKeyboardAsync(input, ReadConsoleKeyAsync, shellToken)", source, StringComparison.Ordinal);
        Assert.Contains("return await DispatchPromptAsync(request);", source, StringComparison.Ordinal);
        Assert.Contains("Task.WhenAll(input.PromptTasks)", source, StringComparison.Ordinal);
        var prompts = File.ReadAllText(Path.Combine(HostFixture.RepoRoot, "BookOfEternityGMBridge/BridgeHost.PromptDispatch.cs"));
        Assert.Contains("WriteToPtyAsync(operation.Input", prompts, StringComparison.Ordinal);
        Assert.Contains("ReferenceEquals(_inputLifetime, operation.Input)", prompts, StringComparison.Ordinal);
        var exit = source[source.IndexOf("private async Task HandlePtyExitedAsync", StringComparison.Ordinal)..source.IndexOf("private void WriteStatusFile", StringComparison.Ordinal)];
        Assert.Contains("!ReferenceEquals(_inputLifetime, observedInput)", exit, StringComparison.Ordinal);
        Assert.Contains("await StopShellCoreAsync();", exit, StringComparison.Ordinal);
        var dispose = source[source.IndexOf("public void Dispose()", StringComparison.Ordinal)..];
        Assert.True(dispose.IndexOf("_inputClosed = true", StringComparison.Ordinal) < dispose.IndexOf("StopShellAsync()", StringComparison.Ordinal));
    }

    private static TaskCompletionSource<bool> Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static async Task<Exception?> Capture(Task task, int seconds = 8)
    {
        try { await task.WaitAsync(TimeSpan.FromSeconds(seconds)); return null; }
        catch (Exception ex) { return ex; }
    }

    private sealed class HostFixture : IAsyncDisposable
    {
        public static readonly string RepoRoot = FindRoot();
        public static readonly string BridgePath = Path.Combine(RepoRoot, "BookOfEternityGMBridge/bin", new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name, "net8.0/BookOfEternityGMBridge.dll");
        public Type Type { get; } = Assembly.LoadFrom(BridgePath).GetType("BookOfEternityGMBridge.BridgeHost", true)!;
        private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-input-lifetime-" + Guid.NewGuid().ToString("N"));
        private readonly object _host;
        private readonly List<CancellationTokenSource> _shells = [];
        private readonly List<Task> _tasks = [];
        public HostFixture() => _host = Activator.CreateInstance(Type, [_root, "not-opened-" + Guid.NewGuid().ToString("N")])!;
        public CancellationTokenSource ShellTokenSource { get; private set; } = null!;
        public CancellationTokenSource HostTokenSource => (CancellationTokenSource)Get("_cts")!;
        public SemaphoreSlim WriteGate => (SemaphoreSlim)Get("_ptyWriteLock")!;
        public string? InputError => (string?)Get("_status")!.GetType().GetProperty("LastInputWriteError")?.GetValue(Get("_status"));
        public object? Get(string name) => Type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(_host);
        private void Set(string name, object? value) => Type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(_host, value);
        private MethodInfo? Method(string name) => Type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
        private object? Invoke(string name, params object?[] args)
        {
            try { return Method(name)!.Invoke(_host, args); }
            catch (TargetInvocationException ex) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException!).Throw(); throw; }
        }
        public object Attach(Stream stream)
        {
            ShellTokenSource = new CancellationTokenSource();
            _shells.Add(ShellTokenSource);
            if (Method("BeginInputLifetime") != null) return Invoke("BeginInputLifetime", stream, ShellTokenSource)!;
            // Causal RED adapter: these are the actual old StartShell assignments, no input algorithm.
            Set("_ptyInput", stream); Set("_shellLoopCts", ShellTokenSource);
            return new object();
        }
        public void Revoke(object binding)
        {
            if (Method("RevokeInputLifetime") != null) Invoke("RevokeInputLifetime", binding);
            else Set("_ptyInput", null); // Old root-exit behavior clears stream without cancelling keyboard.
        }
        public Task Write(object binding, string text, bool append, CancellationToken token = default) => Track(
            (Task)(Method("WriteToPtyAsync")!.GetParameters().Length == 2
                ? Invoke("WriteToPtyAsync", text, append)!
                : Invoke("WriteToPtyAsync", binding, text, append, token)!));
        public Task Keyboard(object binding, Func<CancellationToken, ValueTask<ConsoleKeyInfo?>> keys)
        {
            var method = Method("PumpKeyboardAsync")!;
            var task = (Task)(method.GetParameters().Length == 2
                ? Invoke("PumpKeyboardAsync", ShellTokenSource.Token, keys)!
                : Invoke("PumpKeyboardAsync", binding, keys, ShellTokenSource.Token)!);
            Set("_keyboardPumpTask", task);
            return Track(task);
        }
        public async Task<bool> ConnectedRequest(Stream response, Func<Task> request)
        {
            var task = (Task<bool>)Invoke("ProcessConnectedRequestAsync", response, request, HostTokenSource.Token)!;
            Track(task);
            return await task;
        }
        public Task Stop() => Track((Task)Invoke("StopShellAsync")!);
        public void Complete(object binding, bool success) => Invoke("CompletePromptDispatch", binding, success, 1L);
        public object? Status(string name) => Get("_status")!.GetType().GetProperty(name)!.GetValue(Get("_status"));
        private Task Track(Task task) { _tasks.Add(task); return task; }
        public void CancelAllShells() { foreach (var cts in _shells) try { cts.Cancel(); } catch (ObjectDisposedException) { } }
        public void DisposeHost() => ((IDisposable)_host).Dispose();
        public async ValueTask DisposeAsync()
        {
            CancelAllShells();
            try { HostTokenSource.Cancel(); } catch (ObjectDisposedException) { }
            foreach (var task in _tasks)
            {
                try { await task.WaitAsync(TimeSpan.FromSeconds(8)); } catch { }
                Assert.True(task.IsCompleted, "Owned fixture task must settle.");
            }
            DisposeHost();
            foreach (var cts in _shells) cts.Dispose();
            Directory.Delete(_root, true);
            Assert.False(Directory.Exists(_root));
        }
        private static string FindRoot()
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "BookOfEternityGMBridge/BookOfEternityGMBridge.csproj"))) return dir.FullName;
            throw new DirectoryNotFoundException("Repository root not found.");
        }
    }

    private sealed class ControlledStream : Stream
    {
        private readonly MemoryStream _bytes = new();
        public bool BlockWrite { get; init; }
        public bool BlockFlush { get; init; }
        public bool IgnoreCancellation { get; init; }
        public bool FailWrite { get; init; }
        public bool FailFlush { get; init; }
        public Action? OnWrite { get; init; }
        public Action? OnFlush { get; init; }
        public TaskCompletionSource<bool> Entered { get; } = Signal();
        public TaskCompletionSource<bool> Release { get; } = Signal();
        public TaskCompletionSource<bool> Flushed { get; } = Signal();
        public int Writes { get; private set; }
        public int Flushes { get; private set; }
        public bool Disposed { get; private set; }
        public byte[] Bytes { get { lock (_bytes) return _bytes.ToArray(); } }
        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token)
        {
            Writes++;
            lock (_bytes) _bytes.Write(buffer, offset, FailWrite ? Math.Min(1, count) : count);
            OnWrite?.Invoke();
            if (BlockWrite) { Entered.TrySetResult(true); await Release.Task.WaitAsync(IgnoreCancellation ? CancellationToken.None : token); }
            if (FailWrite) throw new IOException("Synthetic partial write.");
        }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default) => new(WriteAsync(buffer.ToArray(), 0, buffer.Length, token));
        public override async Task FlushAsync(CancellationToken token)
        {
            Flushes++; OnFlush?.Invoke(); Flushed.TrySetResult(true);
            if (BlockFlush) { Entered.TrySetResult(true); await Release.Task.WaitAsync(IgnoreCancellation ? CancellationToken.None : token); }
            if (FailFlush) throw new IOException("Synthetic flush failure.");
        }
        protected override void Dispose(bool disposing) { Disposed = true; Release.TrySetResult(true); base.Dispose(disposing); }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => !Disposed;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
