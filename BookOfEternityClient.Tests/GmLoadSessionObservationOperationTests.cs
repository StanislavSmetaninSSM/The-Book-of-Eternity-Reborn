using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

#if DEBUG
[CollectionDefinition("Load observation console isolation", DisableParallelization = true)]
public sealed class LoadObservationConsoleIsolation { }

/// <summary>Executes the real Load client protocol over inert stream DATA, not a native owner or real save replacement.</summary>
[Collection("Load observation console isolation")]
public sealed class GmLoadSessionObservationOperationTests
{
    [Theory]
    [InlineData("success")]
    [InlineData("warning")]
    [InlineData("stop-malformed")]
    [InlineData("stop-foreign")]
    [InlineData("finish-foreign")]
    [InlineData("finish-stale")]
    [InlineData("finish-lost")]
    [InlineData("dispose-failure")]
    [InlineData("sink-failure")]
    public async Task ActualOperationRequiresValidatedPairAndSuccessfulOriginalDisposal(string mode)
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-load-observation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var oldNonce = Environment.GetEnvironmentVariable(GmLoadSessionObservationWriter.NonceVariable);
        var oldError = Console.Error;
        var nonce = Guid.NewGuid().ToString("N");
        var id = Guid.NewGuid().ToString("N");
        var original = new GmSessionRunIdentity(root, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"), 1,
            OperatingSystem.IsWindows() ? GmSessionRunBackend.WindowsJob : GmSessionRunBackend.LinuxSupervisor,
            Guid.NewGuid().ToString("N"), "inert-protocol-fixture");
        var replacement = original with { RunId = Guid.NewGuid().ToString("N"), GenerationId = Guid.NewGuid().ToString("N"), Epoch = 2 };
        var stop = new GmLoadSessionReply(true, mode == "stop-foreign" ? Guid.NewGuid().ToString("N") : id,
            GmLoadMainState.Stopped, original, new(original.RunId, "inert", "protocol-data", 101));
        var finish = new GmLoadSessionReply(true, mode == "finish-foreign" ? Guid.NewGuid().ToString("N") : id,
            mode == "warning" ? GmLoadMainState.StartedNotReady : GmLoadMainState.Running,
            mode == "finish-stale" ? original : replacement,
            new(replacement.RunId, "inert", "protocol-data", 102));
        var input = (mode == "stop-malformed" ? "{}" : JsonSerializer.Serialize(stop, MainOperationReader.Json)) + "\n" +
            (mode == "finish-lost" ? "" : JsonSerializer.Serialize(finish, MainOperationReader.Json) + "\n");
        using var transport = new InertLoadStream(Encoding.UTF8.GetBytes(input), mode == "dispose-failure");
        using var output = new DisposalCheckingWriter(transport, mode == "sink-failure");
        GmLoadSessionOperation? operation = null;
        var connects = 0;
        try
        {
            var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            var statusPath = files.ResolvePath("game_state/control/gm_bridge_status.json");
            Directory.CreateDirectory(Path.GetDirectoryName(statusPath)!);
            var endpoint = new BridgeTransportEndpoint(1, "named-pipe-v1",
                OperatingSystem.IsWindows() ? "inert-load" : Path.Combine(root, "inert-load"), null);
            File.WriteAllText(statusPath, JsonSerializer.Serialize(new { terminalRunId = original.RunId,
                transportEndpoint = JsonSerializer.Deserialize<JsonElement>(BridgeTransportConnector.EncodeEndpoint(endpoint)) }));
            WriteMetadata(root, original);
            Environment.SetEnvironmentVariable(GmLoadSessionObservationWriter.NonceVariable, nonce);
            Console.SetError(output);
            operation = GmLoadSessionOperation.Reserve(files, id, "selected-save", original.GenerationId,
                (actual, token) => { Assert.Equal(endpoint, actual); token.ThrowIfCancellationRequested(); connects++; return Task.FromResult<Stream>(transport); });
            if (mode is "stop-malformed" or "stop-foreign")
            {
                if (mode == "stop-malformed") await Assert.ThrowsAsync<JsonException>(() => operation.StopAsync());
                else await Assert.ThrowsAsync<IOException>(() => operation.StopAsync());
                Assert.Equal(GmLoadMainState.Uncertain, operation.State);
            }
            else
            {
                await operation.StopAsync();
                Assert.Equal(GmLoadMainState.Stopped, operation.State);
                Assert.Equal("", output.ToString());
                WriteMetadata(root, replacement);
                if (mode is "finish-foreign" or "finish-lost")
                    await Assert.ThrowsAsync<IOException>(() => operation.FinishAsync(true, true, replacement.GenerationId));
                else if (mode == "finish-stale")
                    await Assert.ThrowsAsync<InvalidDataException>(() => operation.FinishAsync(true, true, replacement.GenerationId));
                else
                    Assert.Equal(finish.State, await operation.FinishAsync(true, true, replacement.GenerationId));
                Assert.Equal("", output.ToString()); // No witness before original disposal.
            }
            if (mode == "dispose-failure")
            {
                await Assert.ThrowsAsync<IOException>(() => operation.DisposeAsync().AsTask());
                await operation.DisposeAsync(); // A later successful close must not replay a lost witness.
            }
            else await operation.DisposeAsync();
            Assert.True(transport.DisposedSuccessfully);
            Assert.Equal(1, connects);
            Assert.Equal(mode.StartsWith("stop-", StringComparison.Ordinal) ? 1 : 2,
                transport.WrittenFrames.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
            if (mode is "success" or "warning")
            {
                Assert.False(string.IsNullOrWhiteSpace(output.ToString()), "Validated and disposed operation did not emit its witness.");
                Assert.False(output.WroteBeforeDisposal);
                using var json = JsonDocument.Parse(output.ToString());
                Assert.Equal(nonce, json.RootElement.GetProperty("nonce").GetString());
                var observed = JsonSerializer.Deserialize<GmLoadSessionObservation>(json.RootElement.GetProperty("observation").GetRawText())!;
                Assert.Equal(stop, observed.OriginalStop); Assert.Equal(finish, observed.ReplacementFinish);
                Assert.Equal(id, observed.OperationId); Assert.Equal(root, observed.RootKey);
                Assert.Equal("selected-save", observed.SourceKey); Assert.Equal(original.GenerationId, observed.ExpectedGeneration);
                Assert.Equal(replacement.GenerationId, observed.EstablishedGeneration);
                Assert.True(observed.Committed && observed.RefreshConfirmed);
                var once = output.ToString(); await operation.DisposeAsync(); Assert.Equal(once, output.ToString());
            }
            else Assert.Equal("", output.ToString());
            if (mode == "sink-failure") { Assert.Equal(1, output.Attempts); Assert.Equal(GmLoadMainState.Running, operation.State); }
        }
        finally
        {
            Console.SetError(oldError);
            Environment.SetEnvironmentVariable(GmLoadSessionObservationWriter.NonceVariable, oldNonce);
            if (operation != null) await operation.DisposeAsync();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    /// <summary>Writes inert metadata consistency inputs; this fixture does not acquire or impersonate a live owner.</summary>
    private static void WriteMetadata(string root, GmSessionRunIdentity identity)
    {
        var path = Path.Combine(root, ".boe_runtime", "gm-runs", "main.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, GmSessionRunRecordCodec.Encode(new(1, identity, GmSessionRunDisposition.Running, null)));
    }

    private sealed class InertLoadStream(byte[] input, bool failDispose) : MemoryStream(input)
    {
        private readonly MemoryStream _writes = new();
        private bool _failDispose = failDispose;
        internal bool DisposedSuccessfully { get; private set; }
        internal string WrittenFrames => Encoding.UTF8.GetString(_writes.ToArray());
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default)
            => _writes.WriteAsync(buffer, token);
        public override Task FlushAsync(CancellationToken token) => Task.CompletedTask;
        protected override void Dispose(bool disposing)
        {
            if (_failDispose) { _failDispose = false; throw new IOException("Original transport disposal failed."); }
            base.Dispose(disposing); DisposedSuccessfully = true;
        }
    }

    private sealed class DisposalCheckingWriter(InertLoadStream stream, bool failWrite) : StringWriter
    {
        internal bool WroteBeforeDisposal { get; private set; }
        internal int Attempts { get; private set; }
        public override void WriteLine(string? value)
        {
            Attempts++; WroteBeforeDisposal |= !stream.DisposedSuccessfully;
            if (failWrite) throw new IOException("Observation sink failed.");
            base.WriteLine(value);
        }
    }
}
#endif
