using System.Reflection;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Exercises the built production bridge without invoking native startup.</summary>
public sealed class GmBridgeOutputPumpTests
{
    private const string UnicodeText = "AЖé€😀e\u0301Z";

    public static IEnumerable<object[]> UnicodeSplits()
    {
        for (var split = 1; split < Encoding.UTF8.GetByteCount(UnicodeText); split++)
            yield return [split];
    }

    [Theory]
    [MemberData(nameof(UnicodeSplits))]
    public async Task ActualPump_EveryUnicodeSplitPreservesTextAndRawBytes(int split)
    {
        var bytes = Encoding.UTF8.GetBytes(UnicodeText);
        using var host = new HostFixture();
        using var input = new ChunkStream([bytes[..split], bytes[split..]]);
        using var output = new RecordingStream();
        await host.Pump(input, output);
        Assert.Equal(UnicodeText, host.Recent);
        Assert.Equal(bytes, output.ToArray());
        Assert.Equal(2, host.Version);
        Assert.Equal(2, output.FlushCount);
        AssertStreamsStillOwnedByCaller(input, output);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(4096)]
    public async Task ActualPump_ChunkingAcrossBufferBoundaryPreservesControlAndUnicode(int chunkSize)
    {
        var text = new string('a', 4095) + "Ж😀e\u0301\r\n\0\u001b[31m紅\u001b[0m\u001b]0;title\a";
        var bytes = Encoding.UTF8.GetBytes(text);
        using var host = new HostFixture();
        using var input = new ChunkStream(Chunk(bytes, chunkSize));
        using var output = new RecordingStream();
        await host.Pump(input, output);
        Assert.Equal(text, host.Recent);
        Assert.Equal(bytes, output.ToArray());
        Assert.Equal(input.NonemptyReads, host.Version);
        Assert.Equal(input.NonemptyReads, output.FlushCount);
    }

    public static IEnumerable<object[]> MalformedStreams()
    {
        yield return [new byte[] { 0xE2, 0x82 }, "\uFFFD"];
        yield return [new byte[] { 0xE2, 0x82, 0x41 }, "\uFFFDA"];
        yield return [new byte[] { 0xC0, 0xAF }, "\uFFFD\uFFFD"];
        yield return [new byte[] { 0xED, 0xA0, 0x80 }, "\uFFFD\uFFFD\uFFFD"];
        yield return [new byte[] { 0xF4, 0x90, 0x80, 0x80 }, "\uFFFD\uFFFD\uFFFD\uFFFD"];
        yield return [new byte[] { 0xFF, 0x41, 0x80 }, "\uFFFDA\uFFFD"];
    }

    [Theory]
    [MemberData(nameof(MalformedStreams))]
    public async Task ActualPump_MalformedAndIncompleteInputKeepsWholeStreamReplacementPolicy(byte[] bytes, string expected)
    {
        Assert.Equal(expected, Encoding.UTF8.GetString(bytes));
        using var host = new HostFixture();
        using var input = new ChunkStream(Chunk(bytes, 1));
        using var output = new RecordingStream();
        await host.Pump(input, output);
        Assert.Equal(expected, host.Recent);
        Assert.Equal(bytes, output.ToArray());
        Assert.Equal(bytes.Length, host.Version);
        Assert.Equal(bytes.Length + 1, input.ReadCalls);
    }

    [Fact]
    public async Task ActualPump_PendingMalformedPrefixPlusFullReadFitsDecoderBuffer()
    {
        var ascii = Encoding.ASCII.GetBytes(new string('x', 4096));
        using var host = new HostFixture();
        using var input = new ChunkStream([new byte[] { 0xF0, 0x9F, 0x98 }, ascii]);
        using var output = new RecordingStream();
        await host.Pump(input, output);
        Assert.Equal("\uFFFD" + new string('x', 4096), host.Recent);
        Assert.Equal(new byte[] { 0xF0, 0x9F, 0x98 }.Concat(ascii), output.ToArray());
        Assert.Equal(2, host.Version);
    }

    [Fact]
    public async Task ActualPump_IncompleteScalarAdvancesByteActivityBeforeTextExists()
    {
        using var host = new HostFixture();
        var firstSignal = host.Signal;
        var observed = false;
        using var input = new ChunkStream([new byte[] { 0xF0 }, new byte[] { 0x9F, 0x98, 0x80 }]);
        using var output = new RecordingStream();
        input.BeforeRead = index =>
        {
            if (index != 1) return;
            observed = true;
            Assert.Equal(new byte[] { 0xF0 }, output.ToArray());
            Assert.Equal(1, output.FlushCount);
            Assert.Equal(1, host.Version);
            Assert.True(firstSignal.IsCompletedSuccessfully);
            Assert.NotSame(firstSignal, host.Signal);
            Assert.False(host.Signal.IsCompleted);
            Assert.Empty(host.Recent);
        };
        await host.Pump(input, output);
        Assert.True(observed);
        Assert.Equal("😀", host.Recent);
        Assert.Equal(2, host.Version);
    }

    [Fact]
    public async Task ActualPump_OnlyRealEofFinalizesPendingTextWithoutInventingByteActivity()
    {
        using var host = new HostFixture();
        Task? eofSignal = null;
        using var input = new ChunkStream([new byte[] { 0xE2, 0x82 }]);
        using var output = new RecordingStream();
        input.BeforeRead = index =>
        {
            if (index != 1) return;
            Assert.Empty(host.Recent);
            Assert.Equal(1, host.Version);
            eofSignal = host.Signal;
        };
        await host.Pump(input, output);
        Assert.Equal("\uFFFD", host.Recent);
        Assert.Equal(1, host.Version);
        Assert.True(eofSignal?.IsCompletedSuccessfully);
        Assert.Equal(2, input.ReadCalls);
        Assert.Equal(1, output.FlushCount);
    }

    [Fact]
    public async Task ActualPump_EmptyEofDoesNotInventActivityOrText()
    {
        using var host = new HostFixture();
        var signal = host.Signal;
        using var input = new ChunkStream([]);
        using var output = new RecordingStream();
        await host.Pump(input, output);
        Assert.Empty(host.Recent);
        Assert.Empty(output.ToArray());
        Assert.Equal(0, host.Version);
        Assert.Equal(0, output.FlushCount);
        Assert.False(signal.IsCompleted);
    }

    [Theory]
    [InlineData("read")]
    [InlineData("write")]
    [InlineData("flush")]
    public async Task ActualPump_FaultPropagatesWithoutFinalizingPendingBytes(string phase)
    {
        using var host = new HostFixture();
        using var input = new ChunkStream([new byte[] { 0xE2 }, new byte[] { 0x82, 0xAC }]);
        using var output = new RecordingStream();
        var error = new IOException("controlled " + phase + " fault");
        if (phase == "read") input.BeforeRead = i => { if (i == 1) throw error; };
        if (phase == "write") output.BeforeWrite = i => { if (i == 1) throw error; };
        if (phase == "flush") output.BeforeFlush = i => { if (i == 1) throw error; };
        var actual = await Assert.ThrowsAsync<IOException>(() => host.Pump(input, output));
        Assert.Same(error, actual);
        Assert.Empty(host.Recent);
        Assert.Equal(1, host.Version);
        Assert.Equal(phase == "flush" ? new byte[] { 0xE2, 0x82, 0xAC } : new byte[] { 0xE2 }, output.ToArray());
        AssertStreamsStillOwnedByCaller(input, output);
    }

    [Theory]
    [InlineData("before")]
    [InlineData("between")]
    [InlineData("read")]
    [InlineData("eof")]
    [InlineData("write")]
    [InlineData("flush")]
    public async Task ActualPump_CancellationIsNotSuccessfulEof(string phase)
    {
        using var host = new HostFixture();
        using var cts = new CancellationTokenSource();
        using var input = new ChunkStream(phase == "eof"
            ? [new byte[] { 0xE2 }]
            : [new byte[] { 0xE2 }, new byte[] { 0x82, 0xAC }]);
        using var output = new RecordingStream();
        if (phase == "before") cts.Cancel();
        if (phase is "read" or "eof") input.BeforeRead = i =>
        {
            if (i != 1) return;
            cts.Cancel();
            if (phase == "read") cts.Token.ThrowIfCancellationRequested();
            // Deliberately return a zero-byte read despite the canceled token.
        };
        if (phase is "between" or "flush") output.BeforeFlush = i =>
        {
            if (i != (phase == "between" ? 0 : 1)) return;
            cts.Cancel();
            if (phase == "flush") cts.Token.ThrowIfCancellationRequested();
        };
        if (phase == "write") output.BeforeWrite = i =>
        {
            if (i != 1) return;
            cts.Cancel();
            cts.Token.ThrowIfCancellationRequested();
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => host.Pump(input, output, cts.Token));
        Assert.Empty(host.Recent);
        Assert.Equal(phase == "before" ? 0 : 1, host.Version);
        AssertStreamsStillOwnedByCaller(input, output);
    }

    [Fact]
    public async Task ActualPump_CanceledBlockedReadCompletesWithoutEofFlush()
    {
        using var host = new HostFixture();
        using var cts = new CancellationTokenSource();
        using var input = new ChunkStream([new byte[] { 0xE2 }]) { BlockAfterChunks = true };
        using var output = new RecordingStream();
        var pump = host.Pump(input, output, cts.Token);
        try
        {
            await input.Blocked.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pump.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Empty(host.Recent);
            Assert.Equal(1, host.Version);
        }
        finally
        {
            cts.Cancel();
            try { await pump.WaitAsync(TimeSpan.FromSeconds(5)); } catch (OperationCanceledException) { }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualPump_SeparateInvocationsNeverSharePendingDecoderState(bool firstEndsInFault)
    {
        using var host = new HostFixture();
        using var first = new ChunkStream([new byte[] { 0xE2 }]);
        using var firstOutput = new RecordingStream();
        if (firstEndsInFault)
        {
            first.BeforeRead = i => { if (i == 1) throw new IOException("first pump failed"); };
            await Assert.ThrowsAsync<IOException>(() => host.Pump(first, firstOutput));
        }
        else await host.Pump(first, firstOutput);
        using var second = new ChunkStream([new byte[] { 0x82, 0xAC }]);
        using var secondOutput = new RecordingStream();
        await host.Pump(second, secondOutput);
        Assert.Equal(firstEndsInFault ? "\uFFFD\uFFFD" : "\uFFFD\uFFFD\uFFFD", host.Recent);
        Assert.Equal(2, host.Version);
        Assert.Equal(new byte[] { 0x82, 0xAC }, secondOutput.ToArray());
    }

    [Theory]
    [InlineData(65536)]
    [InlineData(12000)]
    public async Task ActualPump_BoundedTailNeverStartsInsideSurrogatePair(int limit)
    {
        var text = "😀" + new string('x', limit - 1);
        using var host = new HostFixture();
        using var input = new ChunkStream(Chunk(Encoding.UTF8.GetBytes(text), 4096));
        using var output = new RecordingStream();
        await host.Pump(input, output);
        var tail = limit == 65536 ? host.Recent : host.DiagnosticTail;
        Assert.Equal(new string('x', limit - 1), tail);
        Assert.True(tail.Length <= limit);
        Assert.Equal(Encoding.UTF8.GetBytes(text), output.ToArray());
    }

    [Theory]
    [InlineData(65536)]
    [InlineData(12000)]
    public async Task ActualPump_BoundedTailRetainsCompletePairAtBoundary(int limit)
    {
        var text = "prefix" + "😀" + new string('x', limit - 2);
        using var host = new HostFixture();
        using var input = new ChunkStream(Chunk(Encoding.UTF8.GetBytes(text), 4096));
        using var output = new RecordingStream();
        await host.Pump(input, output);
        var tail = limit == 65536 ? host.Recent : host.DiagnosticTail;
        Assert.Equal("😀" + new string('x', limit - 2), tail);
        Assert.Equal(limit, tail.Length);
    }

    [Fact]
    public async Task ActualPump_LongOutputStaysBoundedAfterEveryRead()
    {
        var text = string.Concat(Enumerable.Repeat("Ж😀abcdef", 30000));
        var bytes = Encoding.UTF8.GetBytes(text);
        using var host = new HostFixture();
        using var input = new ChunkStream(Chunk(bytes, 4096));
        using var output = new RecordingStream();
        input.BeforeRead = _ =>
        {
            Assert.InRange(host.Recent.Length, 0, 65536);
            Assert.InRange(host.DiagnosticTail.Length, 0, 12000);
            AssertWellFormedUtf16(host.Recent);
            AssertWellFormedUtf16(host.DiagnosticTail);
        };
        await host.Pump(input, output);
        Assert.Equal(bytes, output.ToArray());
        Assert.Equal(ScalarSafeSuffix(text, 65536), host.Recent);
        Assert.Equal(ScalarSafeSuffix(text, 12000), host.DiagnosticTail);
    }

    [Fact]
    public void ActualConsumerAssemblyAndPortablePdbMatchCurrentProductionSource()
    {
        using var host = new HostFixture();
        var assembly = host.HostType.Assembly;
        Assert.Equal(Path.GetFullPath(HostFixture.BridgePath), Path.GetFullPath(assembly.Location));
        Assert.Equal("BookOfEternityGMBridge", assembly.GetName().Name);
        Assert.NotEqual(typeof(GmBridgeOutputPumpTests).Assembly, assembly);
        using var pdb = File.OpenRead(Path.ChangeExtension(assembly.Location, ".pdb"));
        using var provider = MetadataReaderProvider.FromPortablePdbStream(pdb);
        var metadata = provider.GetMetadataReader();
        var document = Assert.Single(metadata.Documents.Select(metadata.GetDocument), d =>
            metadata.GetString(d.Name).Replace('\\', '/').EndsWith("BookOfEternityGMBridge/Program.cs", StringComparison.Ordinal));
        Assert.Equal(new Guid("8829d00f-11b8-4213-878b-770e8597ac16"), metadata.GetGuid(document.HashAlgorithm));
        Assert.Equal(SHA256.HashData(File.ReadAllBytes(Path.Combine(HostFixture.RepoRoot, "BookOfEternityGMBridge", "Program.cs"))), metadata.GetBlobBytes(document.Hash));
        Assert.Null(host.Field("_pty"));
        Assert.Null(host.Field("_ptyInput"));
    }

    [Fact]
    public void ProductionStartAndDiagnosticsConsumeTheTestedMethods()
    {
        var source = File.ReadAllText(Path.Combine(HostFixture.RepoRoot, "BookOfEternityGMBridge", "Program.cs"));
        Assert.Contains("AttachOwnedTerminal(pty, outputWriter)", source, StringComparison.Ordinal);
        Assert.Contains("PumpOutputAsync(session.OutputReader, output, CancellationToken.None)", source, StringComparison.Ordinal);
        var start = source.IndexOf("private BridgeDiagnostics SnapshotDiagnostics()", StringComparison.Ordinal);
        var end = source.IndexOf("private string GetRecentOutputTail()", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        Assert.Contains("recentOutput = GetRecentOutputTail();", source[start..end], StringComparison.Ordinal);
        Assert.Contains("RecentOutputTail = recentOutput", source[start..end], StringComparison.Ordinal);
    }

    private static string ScalarSafeSuffix(string text, int limit)
    {
        var runes = text.EnumerateRunes().Reverse().ToArray();
        var kept = new List<Rune>();
        var length = 0;
        foreach (var rune in runes)
        {
            if (length + rune.Utf16SequenceLength > limit) break;
            kept.Add(rune);
            length += rune.Utf16SequenceLength;
        }
        kept.Reverse();
        return string.Concat(kept);
    }

    private static void AssertWellFormedUtf16(string text) =>
        Assert.Equal(text, new UTF8Encoding(false, true).GetString(new UTF8Encoding(false, true).GetBytes(text)));

    private static void AssertStreamsStillOwnedByCaller(ChunkStream input, RecordingStream output)
    {
        Assert.False(input.Disposed);
        Assert.False(output.Disposed);
    }

    private static byte[][] Chunk(byte[] bytes, int size) => bytes.Chunk(size).ToArray();

    private sealed class HostFixture : IDisposable
    {
        public static readonly string RepoRoot = FindRepoRoot();
        private static readonly string Configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        public static readonly string BridgePath = Path.Combine(RepoRoot, "BookOfEternityGMBridge", "bin", Configuration, "net8.0", "BookOfEternityGMBridge.dll");
        private static readonly Type Type = Assembly.LoadFrom(BridgePath).GetType("BookOfEternityGMBridge.BridgeHost", throwOnError: true)!;
        private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-output-pump-" + Guid.NewGuid().ToString("N"));
        private readonly object _host;
        public Type HostType => Type;
        public HostFixture() => _host = Activator.CreateInstance(Type, [_root, "not-opened-" + Guid.NewGuid().ToString("N")])!;
        public string Recent => ((StringBuilder)Field("_recentOutput")!).ToString();
        public long Version => (long)Field("_outputVersion")!;
        public Task Signal => ((TaskCompletionSource<bool>)Field("_outputChanged")!).Task;
        public string DiagnosticTail => (string)Method("GetRecentOutputTail").Invoke(_host, null)!;
        public object? Field(string name) => Type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_host);
        private static MethodInfo Method(string name) => Type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
        public Task Pump(Stream input, Stream output, CancellationToken cancellationToken = default) =>
            (Task)Method("PumpOutputAsync").Invoke(_host, [input, output, cancellationToken])!;
        public void Dispose()
        {
            ((IDisposable)_host).Dispose();
            Directory.Delete(_root, recursive: true);
            Assert.False(Directory.Exists(_root));
        }
        private static string FindRepoRoot()
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "BookOfEternityGMBridge", "BookOfEternityGMBridge.csproj")) &&
                    File.Exists(Path.Combine(dir.FullName, "BookOfEternityClient.Tests", "BookOfEternityClient.Tests.csproj"))) return dir.FullName;
            throw new DirectoryNotFoundException("Repository root not found.");
        }
    }

    private sealed class ChunkStream(byte[][] chunks) : Stream
    {
        private int _index;
        public Action<int>? BeforeRead { get; set; }
        public bool BlockAfterChunks { get; init; }
        public TaskCompletionSource<bool> Blocked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int ReadCalls { get; private set; }
        public int NonemptyReads { get; private set; }
        public bool Disposed { get; private set; }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ReadCalls++;
            BeforeRead?.Invoke(_index);
            if (_index == chunks.Length)
            {
                if (BlockAfterChunks)
                {
                    Blocked.TrySetResult(true);
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                return 0;
            }
            var chunk = chunks[_index++];
            Assert.InRange(chunk.Length, 1, buffer.Length);
            chunk.AsMemory().CopyTo(buffer);
            NonemptyReads++;
            return chunk.Length;
        }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
        public override bool CanRead => !Disposed;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class RecordingStream : MemoryStream
    {
        private int _writes;
        public Action<int>? BeforeWrite { get; set; }
        public Action<int>? BeforeFlush { get; set; }
        public int FlushCount { get; private set; }
        public bool Disposed { get; private set; }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            BeforeWrite?.Invoke(_writes++);
            // Some fixtures deliberately ignore cancellation to test the pump's EOF checks.
            Write(buffer.Span);
            return ValueTask.CompletedTask;
        }
        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            BeforeFlush?.Invoke(FlushCount);
            FlushCount++;
            return Task.CompletedTask;
        }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
}
