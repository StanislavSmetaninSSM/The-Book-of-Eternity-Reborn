using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class JointChildOutputCaptureTests
{
    /// <summary>
    /// Preserves decoded Unicode, CRLF and split surrogate pairs in independent durable files.
    /// </summary>
    /// <param name="chunkSize">
    /// Maximum decoded characters returned by each controlled read.
    /// </param>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4096)]
    public async Task ArtifactsPreserveUnicodeAcrossChunksAndFixtureCleanup(int chunkSize)
    {
        using var fixture = new OutputTestFixture();
        var text = "\uFEFFstart\r\nA😀Б\0終\nend\r";
        var capture = new JointChildOutputCapture(fixture.Path, new { Role = "unicode-control", chunkSize });
        var other = new JointChildOutputCapture(fixture.Path, new { Role = "independent-control" });
        Assert.NotEqual(capture.DirectoryPath, other.DirectoryPath);
        capture.BindIdentity(new { ParentKnownPid = Environment.ProcessId, Kind = "control-only" });
        using var reader = new ChunkReader(text, chunkSize);
        var drains = await Task.WhenAll(FinitePipeOutput.DrainAsync(reader, capture.StandardOutput),
            FinitePipeOutput.DrainAsync(new StringReader("stderr\r\n"), capture.StandardError),
            FinitePipeOutput.DrainAsync(new StringReader("other"), other.StandardOutput),
            FinitePipeOutput.DrainAsync(new StringReader(""), other.StandardError));
        await capture.FinishAsync(drains[0], drains[1]); await other.FinishAsync(drains[2], drains[3]);
        Assert.Null(capture.PreserveFailure(null)); Assert.Null(other.PreserveFailure(null));
        var expected = Encoding.UTF8.GetBytes(text);
        Assert.Equal(expected, File.ReadAllBytes(capture.OutputReference.Path));
        Assert.Equal(expected.LongLength, capture.OutputReference.StoredBytes);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(expected)), capture.OutputReference.Sha256);
        Assert.Equal("stderr\r\n", File.ReadAllText(capture.ErrorReference.Path));
        new CleanupOwnedFixture(fixture.Path, _ => { }).Dispose();
        Assert.False(Directory.Exists(fixture.Path));
        Assert.True(File.Exists(capture.OutputReference.Path));
        Assert.True(capture.OutputReference.PersistenceComplete);
        Assert.False(capture.EvidenceRetentionRequired);
        var manifest = File.ReadAllText(Path.Combine(capture.DirectoryPath, "capture.json"));
        Assert.DoesNotContain("start", manifest, StringComparison.Ordinal);
        Assert.Contains(capture.OutputReference.Sha256!, manifest, StringComparison.Ordinal);
    }

    /// <summary>
    /// Uses the native pipe decoder with BOM and UTF8 code points split across byte reads.
    /// </summary>
    [Fact]
    public async Task NativeDecoderPreservesTextAndLeavesOriginalStreamOpen()
    {
        using var fixture = new OutputTestFixture();
        var capture = new JointChildOutputCapture(fixture.Path, new { Role = "native-decoder-control" });
        var text = "😀Ж\r\ntext\n";
        using var stream = new SingleByteStream(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(text)).ToArray());
        var output = await NativeLineageOwner.ReadStreamedPipeOutputAsync(stream, capture.StandardOutput);
        var error = await FinitePipeOutput.DrainAsync(new StringReader(""), capture.StandardError);
        await capture.FinishAsync(output, error);
        Assert.Null(capture.PreserveFailure(null)); Assert.True(stream.CanRead);
        Assert.Equal(Encoding.UTF8.GetBytes(text), File.ReadAllBytes(capture.OutputReference.Path));
    }

    /// <summary>
    /// Stores and verifies a line beyond the observed JSON scalar failure using constant-size buffers.
    /// </summary>
    [Fact]
    public async Task LongSingleLineBeyondObservedScalarFailureIsComplete()
    {
        const long length = 169321203;
        const string expectedHash = "90BF3EAC79AB1DD2856F5F47CF9DC5B844538C6016EA738C7FFC56CD7BC5E7D1";
        using var fixture = new OutputTestFixture();
        var capture = new JointChildOutputCapture(fixture.Path, new { Role = "long-line-control", ExpectedCharacters = length });
        using var reader = new GeneratedReader(length);
        var output = await FinitePipeOutput.DrainAsync(reader, capture.StandardOutput);
        var error = await FinitePipeOutput.DrainAsync(new StringReader(""), capture.StandardError);
        await capture.FinishAsync(output, error);
        Assert.Null(capture.PreserveFailure(null)); Assert.True(capture.OutputReference.PersistenceComplete);
        Assert.Equal(length, capture.OutputReference.StoredBytes);
        Assert.Equal(expectedHash, capture.OutputReference.Sha256);
        using var file = File.OpenRead(capture.OutputReference.Path);
        var buffer = new byte[8192]; long verified = 0; int count;
        while ((count = file.Read(buffer)) != 0)
        {
            Assert.True(buffer.AsSpan(0, count).IndexOfAnyExcept((byte)'x') < 0);
            verified += count;
        }
        Assert.Equal(length, verified);
        var frame = JsonSerializer.Serialize(new { Stdout = capture.OutputReference, Stderr = capture.ErrorReference });
        Assert.Contains(expectedHash, frame, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('x', 100), frame, StringComparison.Ordinal);
    }

    /// <summary>
    /// Records write, flush or close failure without claiming complete storage or replacing the original error.
    /// </summary>
    /// <param name="operation">
    /// The exact storage operation that throws in each independent capture.
    /// </param>
    [Theory]
    [InlineData("write")]
    [InlineData("flush")]
    [InlineData("close")]
    public async Task StorageFailuresRemainSecondaryAndRetainEvidence(string operation)
    {
        using var fixture = new OutputTestFixture();
        var fault = new IOException(operation);
        var capture = new JointChildOutputCapture(fixture.Path, new { Role = "storage-fault-control", operation },
            openWriter: path => new ControlledWriter(path, operation, fault));
        var output = await FinitePipeOutput.DrainAsync(new StringReader(new string('y', 20000)), capture.StandardOutput);
        var error = await FinitePipeOutput.DrainAsync(new StringReader("error"), capture.StandardError);
        await capture.FinishAsync(output, error);
        var primary = new TimeoutException("original child wait");
        Assert.Same(primary, capture.PreserveFailure(primary));
        Assert.True(primary.Data.Count >= 2);
        Assert.True(output.Eof); Assert.True(error.Eof);
        Assert.False(capture.OutputReference.PersistenceComplete);
        Assert.True(capture.EvidenceRetentionRequired);
        Assert.NotNull(capture.OutputReference.Sha256);
        Assert.Contains(capture.OutputReference.Failures, x => x.StartsWith(operation + ":", StringComparison.Ordinal));
    }

    /// <summary>
    /// Keeps partial text and read failure separately from EOF and retains the original error instance.
    /// </summary>
    [Fact]
    public async Task ArtifactReadFailureKeepsPrefixAndOriginalException()
    {
        using var fixture = new OutputTestFixture();
        var capture = new JointChildOutputCapture(fixture.Path, new { Role = "read-fault-control" });
        var fault = new IOException("original pipe");
        using var reader = new PrefixFaultReader(fault);
        var output = await FinitePipeOutput.DrainAsync(reader, capture.StandardOutput);
        var error = await FinitePipeOutput.DrainAsync(new StringReader(""), capture.StandardError);
        await capture.FinishAsync(output, error);
        Assert.Same(fault, capture.PreserveFailure(null));
        Assert.Equal("prefix\r\n", File.ReadAllText(capture.OutputReference.Path));
        Assert.False(capture.OutputReference.Eof); Assert.False(capture.OutputReference.PersistenceComplete);
        Assert.True(capture.ErrorReference.PersistenceComplete);
    }

    /// <summary>
    /// Does not invent byte hashes after inspection fails, while retaining actual completed EOF.
    /// </summary>
    [Fact]
    public async Task HashFailureIsExplicitAndDoesNotBecomeNativeReadFailure()
    {
        using var fixture = new OutputTestFixture();
        var fault = new IOException("hash");
        var capture = new JointChildOutputCapture(fixture.Path, new { Role = "hash-fault-control" }, inspect: _ => throw fault);
        var output = await FinitePipeOutput.DrainAsync(new StringReader("full text"), capture.StandardOutput);
        var error = await FinitePipeOutput.DrainAsync(new StringReader(""), capture.StandardError);
        await capture.FinishAsync(output, error);
        Assert.True(output.Eof); Assert.Null(output.ReadFailure);
        Assert.True(capture.OutputReference.Eof); Assert.Null(capture.OutputReference.Sha256);
        Assert.False(capture.OutputReference.PersistenceComplete);
        Assert.Equal("full text", File.ReadAllText(capture.OutputReference.Path));
        Assert.Same(fault, capture.PreserveFailure(null));
    }

    /// <summary>
    /// Preserves initial references if later manifest writes fail and records every failed attempt.
    /// </summary>
    [Fact]
    public async Task ManifestFailureRetainsInitialMetadataAndAllSecondaryErrors()
    {
        using var fixture = new OutputTestFixture();
        var fault = new IOException("manifest"); int writes = 0;
        var capture = new JointChildOutputCapture(fixture.Path, new { Role = "manifest-fault-control" },
            writeManifest: (path, text) => { if (++writes == 1) File.WriteAllText(path, text); else throw fault; });
        capture.BindIdentity(new { Pid = Environment.ProcessId });
        var output = await FinitePipeOutput.DrainAsync(new StringReader("output"), capture.StandardOutput);
        var error = await FinitePipeOutput.DrainAsync(new StringReader(""), capture.StandardError);
        await capture.FinishAsync(output, error);
        var primary = new TimeoutException("original");
        Assert.Same(primary, capture.PreserveFailure(primary)); Assert.Equal(2, primary.Data.Count);
        Assert.True(File.Exists(Path.Combine(capture.DirectoryPath, "initial.json")));
        Assert.True(capture.OutputReference.PersistenceComplete); Assert.True(capture.EvidenceRetentionRequired);
        Assert.Equal(3, writes);
    }

    /// <summary>
    /// A failing observation cannot stop complete raw storage or manufacture a pipe failure.
    /// </summary>
    [Fact]
    public async Task ObserverFailureDoesNotStopRawCapture()
    {
        using var fixture = new OutputTestFixture();
        var capture = new JointChildOutputCapture(fixture.Path, new { Role = "observer-fault-control" });
        var fault = new IOException("observer"); var text = new string('z', 20000);
        var output = await FinitePipeOutput.DrainAsync(new StringReader(text), capture.StandardOutput, _ => throw fault);
        var error = await FinitePipeOutput.DrainAsync(new StringReader(""), capture.StandardError);
        await capture.FinishAsync(output, error);
        Assert.True(output.Eof); Assert.Null(output.ReadFailure); Assert.Null(output.WriteFailure);
        Assert.Same(fault, output.ObserverFailure); Assert.True(capture.OutputReference.PersistenceComplete);
        Assert.Equal(text, File.ReadAllText(capture.OutputReference.Path));
    }

    /// <summary>
    /// Cleanup and report exceptions remain secondary to the exact original exception and stack.
    /// </summary>
    [Fact]
    public void CleanupLedgerKeepsOriginalErrorAndFailsWithoutPrimary()
    {
        var ledger = new JointChildFailureLedger();
        var primary = new TimeoutException("primary");
        try
        {
            try { throw primary; }
            catch (Exception error) { ledger.Record(error, "wait"); throw; }
            finally { ledger.Cleanup(() => throw new IOException("report")); }
        }
        catch (TimeoutException error)
        {
            Assert.Same(primary, error); Assert.Contains(nameof(CleanupLedgerKeepsOriginalErrorAndFailsWithoutPrimary), error.StackTrace);
            Assert.Single(error.Data); Assert.IsType<IOException>(error.Data.Values.Cast<object>().Single());
        }
        var fresh = new JointChildFailureLedger(); var cleanup = new IOException("cleanup only");
        Assert.Same(cleanup, Assert.Throws<IOException>(() => fresh.Cleanup(() => throw cleanup)));
    }

    /// <summary>
    /// Refuses nested artifact placement before any process or native resources are acquired.
    /// </summary>
    [Fact]
    public void ArtifactsCannotBePlacedUnderDisposableRoot()
    {
        using var fixture = new OutputTestFixture();
        Assert.Throws<InvalidDataException>(() => new JointChildOutputCapture(fixture.Path, new { }, fixture.Path));
        Assert.Throws<InvalidDataException>(() => new JointChildOutputCapture(fixture.Path, new { }, Path.Combine(fixture.Path, "nested")));
    }

    /// <summary>
    /// Persists each prefix before the next read and never finalizes a pending original drain as EOF.
    /// </summary>
    [Fact]
    public async Task PrefixIsVisibleWhileOriginalDrainRemainsPending()
    {
        using var fixture = new OutputTestFixture();
        var capture = new JointChildOutputCapture(fixture.Path, new { Role = "pending-drain-control" });
        using var reader = new GatedReader();
        var output = FinitePipeOutput.DrainAsync(reader, capture.StandardOutput);
        var error = await FinitePipeOutput.DrainAsync(new StringReader(""), capture.StandardError);
        try
        {
            await reader.Waiting.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(output.IsCompleted);
            using (var file = new FileStream(capture.OutputReference.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var decoded = new StreamReader(file)) Assert.Equal("prefix", await decoded.ReadToEndAsync());
            await capture.FinishAsync(null, error);
            Assert.False(capture.OutputReference.Eof); Assert.False(capture.OutputReference.PersistenceComplete);
            Assert.True(capture.EvidenceRetentionRequired);
        }
        finally { reader.Release.TrySetResult(); }
        await capture.FinishAsync(await output, error);
        Assert.True(capture.OutputReference.Eof); Assert.True(capture.OutputReference.PersistenceComplete);
        Assert.False(capture.EvidenceRetentionRequired);
    }

    /// <summary>
    /// Observer oversize limits cannot remove raw text, and a later valid marker is still observed.
    /// </summary>
    [Fact]
    public async Task IncrementalObserverKeepsFullOversizeRawAndOriginalMarkerSemantics()
    {
        using var fixture = new OutputTestFixture();
        var nonce = Guid.NewGuid().ToString("N");
        var delivery = new JointInterfaceDiagnosticDelivery(new(123, 456, fixture.Path, nonce, 0));
        var line = JsonSerializer.Serialize(new { Boundary = JointInterfaceDiagnosticDelivery.Boundary,
            Pid = 123, BirthTick = 456UL, Root = fixture.Path, Nonce = nonce, Cut = 0,
            ElapsedMilliseconds = 1L, Utc = DateTimeOffset.UtcNow, Stage = "late-first-return", Detail = (string?)null });
        var text = new string('q', JointInterfaceDiagnosticDelivery.LineLimit + 17) + "\n" + line + "\r\n";
        var capture = new JointChildOutputCapture(fixture.Path, new { Role = "incremental-observer-control" });
        using var reader = new ChunkReader(text, 3);
        var output = await delivery.DrainToAsync(reader, capture.StandardOutput);
        var error = await FinitePipeOutput.DrainAsync(new StringReader(""), capture.StandardError);
        await capture.FinishAsync(output, error);
        Assert.Equal(text, File.ReadAllText(capture.OutputReference.Path));
        Assert.True(capture.OutputReference.PersistenceComplete);
        var health = delivery.ReadHealth(); Assert.True(health.Eof); Assert.False(health.RawReadFault);
        Assert.Equal(1, health.Oversize); Assert.True(health.LateReturnSeen);
        Assert.True(delivery.Reader.TryRead(out var observed)); Assert.Equal("LATE_RETURN", observed.Control);
        Assert.False(delivery.Reader.TryRead(out _));
    }

    /// <summary>
    /// Delays EOF until the test releases the original reader after inspecting its persisted prefix.
    /// </summary>
    private sealed class GatedReader : TextReader
    {
        private bool _read;
        internal readonly TaskCompletionSource Waiting = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        /// <inheritdoc />
        public override async ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
        {
            if (!_read) { _read = true; "prefix".AsMemory().CopyTo(buffer); return 6; }
            Waiting.TrySetResult(); await Release.Task; return 0;
        }
    }

    /// <summary>
    /// Owns one independently created transport-only fixture directory.
    /// </summary>
    private sealed class OutputTestFixture : IDisposable
    {
        /// <summary>
        /// Gets the exclusive fixture path under the repository test-results directory.
        /// </summary>
        internal string Path { get; } = System.IO.Path.Combine(TestRepoPaths.RepoRoot, "TestResults", "joint-child-fixtures", Guid.NewGuid().ToString("N"));
        /// <summary>
        /// Creates only this fixture's exclusive directory.
        /// </summary>
        internal OutputTestFixture() => Directory.CreateDirectory(Path);
        /// <inheritdoc />
        public void Dispose() => new CleanupOwnedFixture(Path, _ => { }).Dispose();
    }

    /// <summary>
    /// Returns decoded text in controlled character chunks without additional buffers.
    /// </summary>
    /// <param name="text">
    /// The small expected Unicode text.
    /// </param>
    /// <param name="chunkSize">
    /// Maximum characters returned per read.
    /// </param>
    private sealed class ChunkReader(string text, int chunkSize) : TextReader
    {
        private int _offset;
        /// <inheritdoc />
        public override ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
        {
            var count = Math.Min(Math.Min(buffer.Length, chunkSize), text.Length - _offset);
            text.AsMemory(_offset, count).CopyTo(buffer); _offset += count;
            return ValueTask.FromResult(count);
        }
    }

    /// <summary>
    /// Generates an arbitrarily long ASCII line without allocating the complete text.
    /// </summary>
    /// <param name="length">
    /// Total characters to emit; no diagnostic quota is applied.
    /// </param>
    private sealed class GeneratedReader(long length) : TextReader
    {
        private long _remaining = length;
        /// <inheritdoc />
        public override ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
        {
            var count = (int)Math.Min(buffer.Length, _remaining);
            buffer.Span[..count].Fill('x'); _remaining -= count;
            return ValueTask.FromResult(count);
        }
    }

    /// <summary>
    /// Forces the real StreamReader decoder to handle one input byte at a time.
    /// </summary>
    /// <param name="bytes">
    /// BOM-prefixed UTF8 input bytes.
    /// </param>
    private sealed class SingleByteStream(byte[] bytes) : MemoryStream(bytes)
    {
        /// <inheritdoc />
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(buffer.Length, 1)], cancellationToken);
    }

    /// <summary>
    /// Injects a selected storage failure while still closing its own original file handle.
    /// </summary>
    /// <param name="path">
    /// Exclusive output artifact path.
    /// </param>
    /// <param name="operation">
    /// The write, flush or close operation that fails.
    /// </param>
    /// <param name="failure">
    /// The exact injected storage exception.
    /// </param>
    private sealed class ControlledWriter(string path, string operation, Exception failure) : TextWriter
    {
        private readonly StreamWriter _inner = new(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read),
            new UTF8Encoding(false)) { AutoFlush = true };
        /// <inheritdoc />
        public override Encoding Encoding => Encoding.UTF8;
        /// <inheritdoc />
        public override Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default) =>
            operation == "write" ? Task.FromException(failure) : _inner.WriteAsync(buffer, cancellationToken);
        /// <inheritdoc />
        public override Task FlushAsync() => operation == "flush" ? Task.FromException(failure) : _inner.FlushAsync();
        /// <inheritdoc />
        public override async ValueTask DisposeAsync()
        {
            await _inner.DisposeAsync();
            if (operation == "close") throw failure;
            GC.SuppressFinalize(this);
        }
    }
}
