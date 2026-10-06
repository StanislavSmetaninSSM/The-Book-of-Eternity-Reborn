using System.Reflection;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

internal static partial class NativePoolScenarioDriver
{
    private static async Task<int> RunRestartControl(string mode, string package, string output)
    {
        var root = Path.Combine(output, "state-copy");
        await BootstrapRestartRoot(root);
        var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        var statePath = Path.Combine(root, ".boe_runtime", "worker-runs-v1", "state.json");
        var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile() with
        {
            LaunchCommand = string.Join(" ", new[] { Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!, "dotnet"),
                typeof(NativePoolScenarioDriver).Assembly.Location, "pool-worker-content-valid", package, output }.Select(x => "\"" + x + "\"")),
            TimeoutSeconds = 15
        };
        var task = GmWorkerBridgeTestFixtures.ValidationRepairTask() with
        {
            TimeoutSeconds = profile.TimeoutSeconds,
            ContextFiles = [new WorkerFileReference { Path = RestartContextPath, Sha256 = GmWorkerRunLedgerCodec.Hash(RestartContextBytes) }]
        };
        using var cancel = new CancellationTokenSource();
        GmWorkerProcessHostLaunch? host = null;
        ReleaseCountingStream? observed = null;
        var fault = true; var faults = 0; var repeatedCalls = 0;
        var pendingRepeats = new List<(Task Task, CancellationTokenSource Deadline)>();
        void Observe(WorkerLedgerIoStage stage)
        {
            if (mode != "released-ack" || !fault || stage != WorkerLedgerIoStage.StateDirectorySynced || !File.Exists(statePath)) return;
            var state = GmWorkerRunLedgerCodec.Decode(new(root), File.ReadAllBytes(statePath));
            if (!state.Entries.Any(x => x.Phase == WorkerRunPhase.Released)) return;
            faults++;
            var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            // Actual original host API, while its first sent Release is still live.
            // The pass-through stream counts bytes, not the pre-release pool hook.
            var repeat = host!.ReleaseAsync(deadline.Token);
            if (!repeat.IsCompleted) pendingRepeats.Add((repeat, deadline));
            else { try { repeat.GetAwaiter().GetResult(); repeatedCalls++; } finally { deadline.Dispose(); } }
            throw new IOException("Synthetic Released commit ACK withheld after original send.");
        }
        var hooks = new GmWorkerBridgePoolHooks
        {
            AfterHostPrepared = original =>
            {
                host = original;
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var stream = (Stream)typeof(GmWorkerProcessHostLaunch).GetField("_controlPipe", flags)!.GetValue(host)!;
                var nonce = (string)typeof(GmWorkerProcessHostLaunch).GetField("_launchNonce", flags)!.GetValue(host)!;
                observed = new(stream, nonce);
                typeof(GmWorkerProcessHostLaunch).GetField("_controlChannel", flags)!.SetValue(host, new GmWorkerProcessHostFrameChannel(observed));
            },
            BeforeWorkerReleaseAsync = () => { if (mode == "cancel") cancel.Cancel(); return Task.CompletedTask; },
            AfterWorkerReleaseAsync = async () =>
            {
                var clock = System.Diagnostics.Stopwatch.StartNew();
                while (!File.Exists(Path.Combine(output, "worker-starts")))
                {
                    if (clock.Elapsed > TimeSpan.FromSeconds(5)) throw new TimeoutException("Actual Released worker did not start.");
                    await Task.Delay(10);
                }
            }
        };
        var reaper = new GmWorkerQuarantineReaper(1, retrySchedule: [], runInBackground: false);
        using var admission = new GmWorkerNativePoolAdmission(package, root, durable: true, Observe);
        var pool = new GmWorkerBridgePool(fs, null, new GmWorkerAuditLog(fs), hooks, GmWorkerProcessTreeFactory.Instance, reaper, admission);
        var result = await pool.RunTaskAsync(profile, task, cancel.Token);
        foreach (var pending in pendingRepeats)
        { try { await pending.Task; } catch (OperationCanceledException) { } finally { pending.Deadline.Dispose(); } }
        var before = BoundarySnapshot(result, task, reaper, statePath, null);
        fault = false;
        await Task.WhenAll(reaper.RunPassAsync(), reaper.RunPassAsync());
        var after = BoundarySnapshot(result, task, reaper, statePath, null);
        await File.WriteAllTextAsync(Path.Combine(output, "restart-control.json"), JsonSerializer.Serialize(new
        {
            mode, faults, repeatedCalls, pendingRepeats = pendingRepeats.Count, before, after,
            writes = observed?.Writes, releaseFrames = observed?.ReleaseFrames,
            workerStarts = File.Exists(Path.Combine(output, "worker-starts")) ? File.ReadAllLines(Path.Combine(output, "worker-starts")).Length : 0,
            detachedRemaining = Directory.Exists(admission.RuntimeBase) && Directory.EnumerateDirectories(admission.RuntimeBase, "game_session", SearchOption.AllDirectories).Any()
        }));
        return 0;
    }

    // Writes and flushes go unchanged to the original admitted native control pipe.
    // The wrapper neither returns status frames nor manufactures any authority.
    private sealed class ReleaseCountingStream(Stream original, string nonce) : Stream
    {
        internal int Writes, ReleaseFrames;
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var text = Encoding.UTF8.GetString(buffer.Span);
            if (!text.EndsWith('\n')) throw new InvalidOperationException("Release observation requires the actual complete frame.");
            _ = GmWorkerProcessHostProtocol.ParseControl(text[..^1], nonce, GmWorkerProcessHostControlKind.Release);
            await original.WriteAsync(buffer, cancellationToken);
            Writes++; ReleaseFrames++;
        }
        public override Task FlushAsync(CancellationToken cancellationToken) => original.FlushAsync(cancellationToken);
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => original.Flush();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
