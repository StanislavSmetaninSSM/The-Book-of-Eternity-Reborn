using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Real-file causal control for the existing records writer's encoded GZip input calls.</summary>
public sealed class JointRecordsWriterBufferTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>Retains the actual test receiver for the external comparison receipt.</summary>
    /// <param name="output">The current xUnit receiver.</param>
    public JointRecordsWriterBufferTests(ITestOutputHelper output) => _output = output;

    /// <summary>Requires fewer actual compressor-input writes after exact source, flush, decoded and closing proofs.</summary>
    [Fact]
    public void SameActualRowsReduceGzipInputWritesWithoutChangingDecodedData()
    {
        const int scopes = 2048;
        using var serialized = new StringWriter();
        var timings = new GameEngineTurnLifecycleTests.JointOperationTimings(serialized);
        var recorder = new OperationTimingAccumulator();
        using var window = recorder.StartWindow(captureEvents: true);
        for (var index = 0; index < scopes; index++)
            using (recorder.Measure("writer.actual.scope")) { }
        window.Dispose();
        var events = window.DrainEvents();
        Assert.True(window.Capture().Complete);
        var status = window.CaptureEventStatus();
        Assert.Equal(scopes * 2, status.Attempted);
        Assert.Equal(status.Attempted, status.Enqueued);
        Assert.Equal(status.Attempted, status.Drained);
        Assert.Equal(0, status.Pending); Assert.Equal(0, status.Failed);
        Assert.Equal(scopes * 2, events.Length);
        foreach (var original in events) timings.ObserveRecorded(original);
        // Freeze the real accepted serialization once. Both writers receive these
        // exact strings, including observer costs; neither replays source clocks.
        var rows = serialized.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        serialized.Dispose();
        Assert.Equal(events.Length, rows.Length);
        for (var index = 0; index < rows.Length; index++)
        {
            using var document = JsonDocument.Parse(rows[index]);
            var row = document.RootElement;
            var source = events[index];
            Assert.Equal(17, row.EnumerateObject().Count());
            Assert.Equal("joint_operation_timing", row.GetProperty("Boundary").GetString());
            Assert.Equal("sealed-passive-events-v1", row.GetProperty("Transport").GetString());
            Assert.Equal(index + 1, row.GetProperty("Sequence").GetInt64());
            Assert.Equal(Environment.ProcessId, row.GetProperty("PID").GetInt32());
            Assert.Equal(index, row.GetProperty("ObserverCompletedBefore").GetInt64());
            Assert.Equal(source.ElapsedTicks * 1000d / System.Diagnostics.Stopwatch.Frequency,
                row.GetProperty("Milliseconds").GetDouble());
            Assert.Equal(source.Name, row.GetProperty("Operation").GetString());
            Assert.Equal(source.State, row.GetProperty("State").GetString());
            Assert.Equal(source.Sequence, row.GetProperty("PassiveSequence").GetInt64());
            Assert.Equal(source.ScopeId, row.GetProperty("ScopeId").GetInt64());
            Assert.Equal(source.TimestampTicks, row.GetProperty("TimestampTicks").GetInt64());
            Assert.Equal(source.ElapsedTicks, row.GetProperty("ElapsedTicks").GetInt64());
            Assert.Equal(source.Late, row.GetProperty("Late").GetBoolean());
        }
        var message = "original Unicode рана 日本語 🩹 \"quote\" \\ \n\r\t\0" + new string('傷', 8192);
        var messageRow = JsonSerializer.Serialize(new { Kind = "ActualWriterBufferMessage", Message = message });
        var requestedRoot = Environment.GetEnvironmentVariable("BOE_TEST_DIAGNOSTIC_ROOT")
            ?? Path.Combine(Path.GetTempPath(), "boe-writer-buffer-diagnostics");
        var root = TestDiagnosticPaths.ResolveRoot(requestedRoot);
        var comparison = Path.GetFullPath(Path.Combine(root, "joint-records-writer-buffer", Guid.NewGuid().ToString("N")));
        var repository = Path.GetFullPath(TestRepoPaths.RepoRoot).TrimEnd(Path.DirectorySeparatorChar);
        Assert.False(comparison.Equals(repository, StringComparison.OrdinalIgnoreCase) ||
            comparison.StartsWith(repository + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        Directory.CreateDirectory(comparison);
        var baselinePath = Path.Combine(comparison, "baseline.jsonl.gz");
        var candidatePath = Path.Combine(comparison, "candidate.jsonl.gz");
        using var baselineInput = OpenInputCounter(baselinePath);
        using var candidateInput = OpenInputCounter(candidatePath);
        using var expected = new StringWriter();
        var visibilityChecks = 0;
        // Frozen independent baseline: the accepted two-argument/default-buffer constructor.
        using (var baseline = new StreamWriter(baselineInput, new UTF8Encoding(false)) { AutoFlush = false })
        using (var candidate = JointBundleDiagnosticOutput.CreateRecordsWriter(candidateInput, gzipRecords: true))
        {
            for (var index = 0; index < rows.Length; index++)
            {
                baseline.WriteLine(rows[index]); candidate.WriteLine(rows[index]); expected.WriteLine(rows[index]);
                if ((index + 1) % 1024 == 0) FlushAndVerify();
                if (index + 1 == rows.Length / 2)
                {
                    baseline.WriteLine(messageRow); candidate.WriteLine(messageRow); expected.WriteLine(messageRow);
                    FlushAndVerify(); // full original message before forwarding
                }
            }
            FlushAndVerify(); // unchanged nonempty drain's final flush
            Assert.Equal(6, visibilityChecks);
            Assert.Equal(6, baselineInput.Flushes); Assert.Equal(6, candidateInput.Flushes);

            void FlushAndVerify()
            {
                baseline.Flush(); candidate.Flush();
                var prefix = Encoding.UTF8.GetBytes(expected.ToString());
                Assert.Equal(prefix, Decode(baselinePath));
                Assert.Equal(prefix, Decode(candidatePath));
                visibilityChecks++;
            }
        }
        Assert.Equal(1, baselineInput.Disposals); Assert.Equal(1, candidateInput.Disposals);
        Assert.Equal(baselineInput.Flushes, candidateInput.Flushes);
        var expectedBytes = Encoding.UTF8.GetBytes(expected.ToString());
        expected.Dispose();
        var expectedHash = Convert.ToHexString(SHA256.HashData(expectedBytes));
        var baselineReceipt = VerifyClosed(baselinePath, expectedBytes, expectedHash);
        var candidateReceipt = VerifyClosed(candidatePath, expectedBytes, expectedHash);
        Assert.Equal(expectedBytes.LongLength, baselineInput.Bytes);
        Assert.Equal(expectedBytes.LongLength, candidateInput.Bytes);
        var decodedRows = Encoding.UTF8.GetString(Decode(candidatePath))
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(events.Length + 1, decodedRows.Length);
        using (var document = JsonDocument.Parse(decodedRows[events.Length / 2]))
            Assert.Equal(message, document.RootElement.GetProperty("Message").GetString());
        var receipt = JsonSerializer.Serialize(new
        {
            Kind = "ActualRecordsWriterBufferComparison", ActualScopes = scopes, ActualEvents = events.Length,
            SourceQueue = status, SourceComplete = window.Capture().Complete,
            DecodedRows = decodedRows.LongLength, DecodedBytes = expectedBytes.LongLength, DecodedSha256 = expectedHash,
            VisibilityChecks = visibilityChecks,
            Baseline = new { baselinePath, baselineInput.Writes, baselineInput.Bytes, baselineInput.Flushes,
                baselineInput.Disposals, Closed = baselineReceipt },
            Candidate = new { candidatePath, candidateInput.Writes, candidateInput.Bytes, candidateInput.Flushes,
                candidateInput.Disposals, Closed = candidateReceipt },
            Limits = "Input-call mechanism only; no speedup or expensive-positive admission. Full external gzip validation required."
        });
        var receiptPath = Path.Combine(comparison, "comparison.json");
        File.WriteAllText(receiptPath, receipt);
        _output.WriteLine(receiptPath);
        // The RED scaffold retains the same default constructor on both paths.
        // All integrity prerequisites above must pass before this causal failure.
        Assert.True(candidateInput.Writes < baselineInput.Writes,
            $"Expected fewer actual GZip input writes; baseline={baselineInput.Writes}, candidate={candidateInput.Writes}. Receipt: {receiptPath}");
    }

    private static CountingWriteStream OpenInputCounter(string path) => new(new GZipStream(
        new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read), CompressionLevel.Fastest));

    private static byte[] Decode(string path)
    {
        using var stored = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var gzip = new GZipStream(stored, CompressionMode.Decompress);
        using var decoded = new MemoryStream();
        gzip.CopyTo(decoded);
        return decoded.ToArray(); // a live prefix is explicitly not a finished-container verdict
    }

    private static object VerifyClosed(string path, byte[] expected, string decodedHash)
    {
        Assert.Equal(expected, Decode(path));
        var stored = File.ReadAllBytes(path);
        Assert.True(stored.Length >= 20);
        Assert.Equal(0x1f, stored[0]); Assert.Equal(0x8b, stored[1]);
        Assert.Equal((uint)expected.LongLength, BinaryPrimitives.ReadUInt32LittleEndian(stored.AsSpan(stored.Length - 4)));
        var storedHash = Convert.ToHexString(SHA256.HashData(stored));
        JointBundleDiagnosticOutput.ValidateClosedGzipIdentity(path, stored.LongLength, storedHash, expected.LongLength, decodedHash);
        return new { StoredBytes = stored.LongLength, StoredSha256 = storedHash };
    }

    // Counts StreamWriter encoder calls at GZip INPUT, forwarding each overload
    // directly once; it neither buffers, retries, encodes nor invents events.
    private sealed class CountingWriteStream(Stream inner) : Stream
    {
        private bool _disposed;
        internal long Writes { get; private set; }
        internal long Bytes { get; private set; }
        internal long Flushes { get; private set; }
        internal long Disposals { get; private set; }
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() { inner.Flush(); Flushes++; }
        public override void Write(byte[] buffer, int offset, int count)
        { inner.Write(buffer, offset, count); Writes++; Bytes += count; }
        public override void Write(ReadOnlySpan<byte> buffer)
        { inner.Write(buffer); Writes++; Bytes += buffer.Length; }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => inner.SetLength(value);
        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
                Disposals++;
                inner.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
