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
            lock (_bytes) _bytes.Write(buffer, offset, Fail ? Math.Min(1, count) : count);
            Written?.Invoke(Encoding.UTF8.GetString(buffer, offset, count));
            Entered.TrySetResult(true);
            if (Hold) await Release.Task; // Deliberately retains actual in-flight I/O despite cancellation.
            if (Fail) throw new IOException("Controlled partial input failure.");
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
