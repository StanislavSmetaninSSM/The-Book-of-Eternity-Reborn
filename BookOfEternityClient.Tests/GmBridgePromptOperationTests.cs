using System.IO.Pipes;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Real bridge assembly and pipe accept loop, controlled input/view only; no native startup.</summary>
public sealed class GmBridgePromptOperationTests
{
    [Fact]
    public async Task AutomaticDispatch_DoesNotEraseManualDraft()
    {
        await using var host = new PromptHostFixture();
        host.Screen = "CONTROLLED CLI\n› ручной черновик";
        var oldClear = host.Method("ClearPendingInputBeforePromptDispatchAsync");
        if (oldClear != null)
        {
            // Causal baseline: invoke the actual unconditional clear used before old dispatch readiness.
            await (Task)oldClear.Invoke(host.Host, [host.Binding, CancellationToken.None])!;
        }
        else
        {
            var response = await host.Rpc(host.Request("draft"));
            Assert.Equal("not-written", response.GetProperty("promptDelivery").GetProperty("disposition").GetString());
        }
        Assert.Empty(host.Input.Bytes);
    }

    [Fact]
    public async Task ActualPipe_SlowPeerDoesNotBlockStatus()
    {
        await using var host = new PromptHostFixture();
        using var slow = await host.Connect();
        await slow.WriteAsync(Encoding.UTF8.GetBytes("{\"command\":\"dispatchPrompt\""));
        await slow.FlushAsync();
        var response = host.Rpc(new { command = "status" });
        var error = await Record.ExceptionAsync(async () => await response.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Null(error);
        Assert.Empty(host.Input.Bytes);
    }

    private static string? Disposition(JsonElement response) => response.GetProperty("promptDelivery").GetProperty("disposition").GetString();
    private static object Control(PromptHostFixture host, string command, string id) => new
    { command, operationId = id, operationKind = "turn", operationRevision = "revision-1", inputBindingId = host.BindingId };

    [Fact]
    public async Task TwoPromptsOneBinding_CustomSequences_OneSubmitEach()
    {
        await using var host = new PromptHostFixture();
        var text = "one\ntwo";
        host.Input.Written = bytes => host.Observe(bytes == "<submit>" ? "WORKING" : "CONTROLLED CLI\n› " + text);
        Assert.Equal("submission-observed", Disposition(await host.Rpc(host.Request("first", text))));
        host.Observe("CONTROLLED CLI\n› ");
        Assert.Equal("submission-observed", Disposition(await host.Rpc(host.Request("second", text))));
        Assert.Equal("<paste>one\ntwo</paste><submit><paste>one\ntwo</paste><submit>", Encoding.UTF8.GetString(host.Input.Bytes));
        Assert.Equal(host.BindingId, (await host.Rpc(new { command = "status" })).GetProperty("status").GetProperty("inputBindingId").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("CONTROLLED CLI\nAUTH\n› ")]
    [InlineData("CONTROLLED CLI\nTRUST\n› ")]
    [InlineData("CONTROLLED CLI\nUPDATE\n› ")]
    public async Task UnsupportedOrBlockedView_WritesNothing(string screen)
    {
        await using var host = new PromptHostFixture();
        host.Screen = screen;
        Assert.Equal("not-written", Disposition(await host.Rpc(host.Request("blocked"))));
        Assert.Empty(host.Input.Bytes);
    }

    [Fact]
    public async Task PasteNeedsFreshObservation_TimeoutRetainsDraftAndPause()
    {
        await using var host = new PromptHostFixture();
        // Matching text without a newer renderer observation is not proof of this paste.
        host.Input.Written = _ => host.Screen = "CONTROLLED CLI\n› text";
        Assert.Equal("draft-uncertain", Disposition(await host.Rpc(host.Request("first", "text"))));
        host.Observe("CONTROLLED CLI\n› ");
        Assert.False((await host.Rpc(new { command = "setReady", ready = true })).GetProperty("ok").GetBoolean());
        Assert.Equal("not-written", Disposition(await host.Rpc(host.Request("other"))));
        Assert.Equal("<paste>text</paste>", Encoding.UTF8.GetString(host.Input.Bytes));
    }

    [Fact]
    public async Task DuplicateAndChangedBody_RetainOriginalWithoutRepaste()
    {
        await using var host = new PromptHostFixture();
        host.Input.Written = bytes => host.Observe(bytes == "<submit>" ? "WORKING" : "CONTROLLED CLI\n› text");
        Assert.Equal("submission-observed", Disposition(await host.Rpc(host.Request("same", "text"))));
        Assert.Equal("submission-observed", Disposition(await host.Rpc(host.Request("same", "text"))));
        var conflict = await host.Rpc(host.Request("same", "changed"));
        Assert.Equal("identity-conflict", conflict.GetProperty("promptDelivery").GetProperty("reason").GetString());
        Assert.Equal("<paste>text</paste><submit>", Encoding.UTF8.GetString(host.Input.Bytes));
    }

    [Theory]
    [InlineData(false, "draft-uncertain")]
    [InlineData(true, "unknown-outcome")]
    public async Task PartialWrite_RecordsPhaseAndNeverReplays(bool submit, string expected)
    {
        await using var host = new PromptHostFixture();
        host.Input.Fail = !submit;
        host.Input.Written = bytes => { host.Observe("CONTROLLED CLI\n› text"); if (submit) host.Input.Fail = true; };
        Assert.Equal(expected, Disposition(await host.Rpc(host.Request("partial", "text"))));
        var count = host.Input.Bytes.Length;
        Assert.Equal(expected, Disposition(await host.Rpc(host.Request("partial", "text"))));
        Assert.Equal(count, host.Input.Bytes.Length);
    }

    [Fact]
    public async Task ActualPipe_HeldPasteStatusQueueCancelOverflowAndRevoke()
    {
        await using var host = new PromptHostFixture();
        host.Input.Hold = true;
        var active = host.Rpc(host.Request("active", "text"));
        await host.Input.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var queued = host.Rpc(host.Request("queued"));
        for (var i = 0; i < 100 && (int)host.Get("_admittedPrompts")! < 2; i++) await Task.Delay(10);
        Assert.Equal(2, (int)host.Get("_admittedPrompts")!);
        Assert.Equal("busy", (await host.Rpc(host.Request("overflow"))).GetProperty("promptDelivery").GetProperty("reason").GetString());
        Assert.True((await host.Rpc(new { command = "status" }).WaitAsync(TimeSpan.FromSeconds(2))).GetProperty("ok").GetBoolean());
        await host.Rpc(Control(host, "cancelPrompt", "queued")).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("queued-cancelled", Disposition(await queued.WaitAsync(TimeSpan.FromSeconds(2))));
        host.Invoke("RevokeInputLifetime", host.Binding);
        Assert.True((await host.Rpc(new { command = "status" }).WaitAsync(TimeSpan.FromSeconds(2))).GetProperty("ok").GetBoolean());
        Assert.False(active.IsCompleted); // Revocation must retain the ignored-cancellation write.
        host.Input.Release.TrySetResult(true);
        Assert.Equal("draft-uncertain", Disposition(await active));
        Assert.Equal("<paste>text</paste>", Encoding.UTF8.GetString(host.Input.Bytes));
    }

    [Theory]
    [InlineData(false, "draft-uncertain")]
    [InlineData(true, "unknown-outcome")]
    public async Task ActualPipe_ManualTakeoverBeforeOrAfterSubmit(bool afterSubmit, string expected)
    {
        await using var host = new PromptHostFixture();
        host.Input.Written = bytes => { if (afterSubmit && bytes.StartsWith("<paste>")) host.Observe("CONTROLLED CLI\n› text"); };
        var active = host.Rpc(host.Request("takeover", "text"));
        for (var i = 0; i < 150 && !(afterSubmit ? Encoding.UTF8.GetString(host.Input.Bytes).Contains("<submit>") : host.Input.Entered.Task.IsCompleted); i++) await Task.Delay(5);
        Assert.Contains(afterSubmit ? "<submit>" : "<paste>", Encoding.UTF8.GetString(host.Input.Bytes));
        var manual = host.Rpc(new { command = "addText", text = "manual" });
        Assert.Equal(expected, Disposition(await active));
        await manual;
        Assert.Equal("<paste>text</paste>" + (afterSubmit ? "<submit>" : "") + "manual", Encoding.UTF8.GetString(host.Input.Bytes));
    }

    [Fact]
    public async Task StaleBinding_IsZeroWriteAndCannotQueryAnotherBinding()
    {
        await using var host = new PromptHostFixture();
        var stale = new { command = "dispatchPrompt", text = "text", operationId = "stale", operationKind = "turn",
            operationRevision = "revision-1", inputBindingId = "other-binding" };
        Assert.Equal("stale-binding", (await host.Rpc(stale)).GetProperty("promptDelivery").GetProperty("reason").GetString());
        Assert.Empty(host.Input.Bytes);
    }

    [Fact]
    public async Task ChangedBodyQuery_CannotBorrowOriginalSuccess()
    {
        await using var host = new PromptHostFixture();
        host.Input.Written = bytes => host.Observe(bytes == "<submit>" ? "WORKING" : "CONTROLLED CLI\n› original");
        Assert.Equal("submission-observed", Disposition(await host.Rpc(host.Request("content", "original"))));
        var changed = new { command = "promptStatus", operationId = "content", operationKind = "turn", operationRevision = "revision-1",
            inputBindingId = host.BindingId, text = "changed", appendEnter = true };
        Assert.NotEqual("submission-observed", Disposition(await host.Rpc(changed)));
        Assert.Equal("<paste>original</paste><submit>", Encoding.UTF8.GetString(host.Input.Bytes));
    }

    [Fact]
    public async Task DuplicateFlood_HeldInputCannotOccupyControlService()
    {
        await using var host = new PromptHostFixture();
        host.Input.Hold = true;
        var active = host.Rpc(host.Request("flood", "text"));
        await host.Input.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var duplicates = Enumerable.Range(0, 15).Select(_ => host.Rpc(host.Request("flood", "text"))).ToArray();
        await Task.Delay(200);
        var error = await Record.ExceptionAsync(async () => await host.Rpc(new { command = "status" }).WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Null(error);
        await host.Rpc(new { command = "cancelPrompt", operationId="flood", operationKind="turn", operationRevision="revision-1",
            inputBindingId=host.BindingId, text="text", appendEnter=true }).WaitAsync(TimeSpan.FromSeconds(2));
        host.Input.Release.TrySetResult(true);
        await active;
        await Task.WhenAll(duplicates);
        Assert.Equal("<paste>text</paste>", Encoding.UTF8.GetString(host.Input.Bytes));
    }

    [Fact]
    public async Task ManualTakeover_BlocksAutomaticShellBootstrap()
    {
        await using var host = new PromptHostFixture();
        host.Invoke("TakeManualInput", host.Binding);
        var method = host.Method("WriteShellBootstrapAsync");
        var write = method != null ? (Task)method.Invoke(host.Host, [host.Binding, "shell-command", CancellationToken.None])! :
            (Task)host.Invoke("WriteExclusiveInputAsync", host.Binding, "shell-command", true, CancellationToken.None)!;
        await write;
        Assert.Empty(host.Input.Bytes);
    }

    internal sealed class PromptHostFixture : IAsyncDisposable
    {
        internal static readonly string Repo = FindRepo();
        private static readonly string BridgePath = Path.Combine(Repo, "BookOfEternityGMBridge/bin",
            new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name, "net8.0-windows/BookOfEternityGMBridge.dll");
        internal readonly Type HostType = Assembly.LoadFrom(BridgePath).GetType("BookOfEternityGMBridge.BridgeHost", true)!;
        internal readonly object Host;
        internal readonly string Root = Path.Combine(Path.GetTempPath(), "boe-prompt-" + Guid.NewGuid().ToString("N"));
        internal readonly string Pipe = "boe-prompt-" + Guid.NewGuid().ToString("N");
        internal readonly RecordingInput Input = new();
        internal readonly CancellationTokenSource Shell = new();
        internal readonly object Binding;
        private readonly List<Task> _clients = [];
        private readonly List<NamedPipeClientStream> _pipes = [];
        private readonly Task _server;
        internal string Screen = "CONTROLLED CLI\n› ";
        internal string BindingId => (string?)Binding.GetType().GetProperty("Id")?.GetValue(Binding) ?? "baseline-binding";
        internal CancellationTokenSource HostCancellation => (CancellationTokenSource)Get("_cts")!;
        internal PromptHostFixture()
        {
            Directory.CreateDirectory(Root);
            File.WriteAllText(Path.Combine(Root, "config.json"), JsonSerializer.Serialize(new
            {
                GmCliLaunchCommand = "controlled-cli", GmBridgePromptVisibilityTimeoutSeconds = 1,
                GmCliInputProfile = new
                {
                    IdleMarker = "CONTROLLED CLI", PromptPrefix = "› ", WorkingMarker = "WORKING",
                    PasteStart = "<paste>", PasteEnd = "</paste>", NewlineSequence = "\n", SubmitSequence = "<submit>",
                    ObservationTimeoutMilliseconds = 1000, BlockedMarkers = new[] { "AUTH", "TRUST", "UPDATE" }
                }
            }));
            Host = Activator.CreateInstance(HostType, [Root, Pipe])!;
            Binding = Invoke("BeginInputLifetime", Input, Shell)!;
            HostType.GetField("_promptScreenReader", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(Host, (Func<string>)(() => Screen));
            var status = Get("_status")!;
            status.GetType().GetProperty("Ready")!.SetValue(status, true);
            _server = (Task)Invoke("RunServerLoopAsync", HostCancellation.Token)!;
        }
        internal object Request(string id, string text = "автоматический запрос") => new
        {
            command = "dispatchPrompt", text, appendEnter = true, operationId = id,
            operationKind = "turn", operationRevision = "revision-1", inputBindingId = BindingId
        };
        internal MethodInfo? Method(string name) => HostType.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        internal object? Get(string name) => HostType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Host);
        internal object? Invoke(string name, params object?[] arguments)
        {
            try { return Method(name)!.Invoke(Host, arguments); }
            catch (TargetInvocationException e) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException!).Throw(); throw; }
        }
        internal async Task<NamedPipeClientStream> Connect()
        {
            var pipe = new NamedPipeClientStream(".", Pipe, PipeDirection.InOut, PipeOptions.Asynchronous);
            lock (_pipes) _pipes.Add(pipe);
            await pipe.ConnectAsync(2000);
            return pipe;
        }
        internal Task<JsonElement> Rpc(object request)
        {
            var task = RpcCore(request);
            lock (_clients) _clients.Add(task);
            return task;
        }
        private async Task<JsonElement> RpcCore(object request)
        {
            using var pipe = await Connect();
            using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
            await writer.WriteLineAsync(JsonSerializer.Serialize(request));
            using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
            var response = await reader.ReadLineAsync();
            using var json = JsonDocument.Parse(response ?? throw new IOException("Missing bridge response."));
            return json.RootElement.Clone();
        }
        internal void Observe(string screen)
        {
            Screen = screen;
            var sync = Get("_sync")!;
            lock (sync)
            {
                var field = HostType.GetField("_outputVersion", BindingFlags.Instance | BindingFlags.NonPublic)!;
                field.SetValue(Host, (long)field.GetValue(Host)! + 1);
                ((TaskCompletionSource<bool>)Get("_outputChanged")!).TrySetResult(true);
            }
        }
        public async ValueTask DisposeAsync()
        {
            Input.Release.TrySetResult(true);
            await Shell.CancelAsync();
            await HostCancellation.CancelAsync();
            lock (_pipes) foreach (var pipe in _pipes) pipe.Dispose();
            Task[] tasks;
            lock (_clients) tasks = _clients.Append(_server).ToArray();
            foreach (var task in tasks)
            {
                try { await task.WaitAsync(TimeSpan.FromSeconds(6)); } catch { }
                Assert.True(task.IsCompleted, "Every owned RPC and accept task must settle.");
            }
            ((IDisposable)Host).Dispose();
            Shell.Dispose(); Input.Dispose();
            Directory.Delete(Root, true);
            Assert.False(Directory.Exists(Root));
        }
        internal static string FindRepo()
        {
            for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
                if (File.Exists(Path.Combine(d.FullName, "BookOfEternityGMBridge/Program.cs"))) return d.FullName;
            throw new DirectoryNotFoundException();
        }
    }

    internal sealed class RecordingInput : Stream
    {
        private readonly MemoryStream _bytes = new();
        internal Action<string>? Written;
        internal bool Hold;
        internal bool Fail;
        internal readonly TaskCompletionSource<bool> Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<bool> Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal byte[] Bytes { get { lock (_bytes) return _bytes.ToArray(); } }
        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token)
        {
            var failThisWrite = Fail;
            lock (_bytes) _bytes.Write(buffer, offset, failThisWrite ? Math.Min(1, count) : count);
            Written?.Invoke(Encoding.UTF8.GetString(buffer, offset, count));
            Entered.TrySetResult(true);
            if (Hold) await Release.Task; // Deliberately retains actual in-flight I/O despite cancellation.
            if (failThisWrite) throw new IOException("Controlled partial input failure.");
        }
        public override Task FlushAsync(CancellationToken token) => Task.CompletedTask;
        public override void Flush() { }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _bytes.Length;
        public override long Position { get => _bytes.Position; set => throw new NotSupportedException(); }
        public override int Read(byte[] b, int o, int c) => throw new NotSupportedException();
        public override void Write(byte[] b, int o, int c) => throw new NotSupportedException();
        public override long Seek(long o, SeekOrigin s) => throw new NotSupportedException();
        public override void SetLength(long v) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) _bytes.Dispose(); base.Dispose(disposing); }
    }
}
