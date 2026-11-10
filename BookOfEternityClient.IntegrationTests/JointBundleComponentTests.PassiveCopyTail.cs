using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class JointBundleComponentTests
{
    /// <summary>Requires completed success and sentinel-unwind events to be physically visible before owning close.</summary>
    /// <returns>Completion after exact sentinel, full source/storage/closing prerequisites and the final saved-prefix oracle.</returns>
    [Fact]
    public async Task PassiveCopyTail_PersistsCompletedPhasesBeforeFinalClose()
    {
        var fixture = new Fixture(_output, nameof(PassiveCopyTail_PersistsCompletedPhasesBeforeFinalClose),
            passiveTimingEvents: true, gzipRecords: true);
        CopyTailWitness witness;
        await using (fixture)
            witness = fixture.CaptureCopyTailControlBody();
        var output = fixture.Output;
        using var document = JsonDocument.Parse(File.ReadAllText(output.ReportPath));
        var report = document.RootElement;
        Assert.True(report.GetProperty("DiagnosticComplete").GetBoolean());
        Assert.True(report.GetProperty("WriterClosed").GetBoolean());
        Assert.True(report.GetProperty("TimingComplete").GetBoolean());
        Assert.True(report.GetProperty("PassiveTimingRequired").GetBoolean());
        Assert.True(report.GetProperty("GzipFooterClosed").GetBoolean());
        Assert.Equal("gzip-jsonl-utf8-v1", report.GetProperty("RecordsEncoding").GetString());
        Assert.Empty(report.GetProperty("Failures").EnumerateArray());
        var closing = report.GetProperty("FixtureClosing");
        Assert.Equal(output.ExpectedOwnedRoot, closing.GetProperty("Owned").GetString());
        Assert.True(closing.GetProperty("Closing").GetBoolean());
        Assert.False(closing.GetProperty("Retained").GetBoolean());
        Assert.Empty(closing.GetProperty("Failures").EnumerateArray());
        // A leftover directory, regular file or dangling link is not an absent owned root.
        Assert.Throws<FileNotFoundException>(() => File.GetAttributes(output.ExpectedOwnedRoot));
        var stored = File.ReadAllBytes(output.RecordsPath);
        Assert.Equal(stored.LongLength, report.GetProperty("StoredBytes").GetInt64());
        Assert.Equal(Convert.ToHexString(SHA256.HashData(stored)), report.GetProperty("Sha256").GetString());
        var decoded = ReadClosedCopyRangeRecords(output.RecordsPath, gzip: true);
        var (rows, trailing) = ReadCopyTailRows(decoded);
        Assert.Equal(0, trailing);
        Assert.Equal(decoded.LongLength, report.GetProperty("DecodedBytes").GetInt64());
        Assert.Equal(rows.LongLength, report.GetProperty("DecodedRows").GetInt64());
        Assert.Equal(Convert.ToHexString(SHA256.HashData(decoded)), report.GetProperty("DecodedSha256").GetString());
        Assert.Equal((uint)decoded.LongLength, BinaryPrimitives.ReadUInt32LittleEndian(stored.AsSpan(stored.Length - 4)));
        JointBundleDiagnosticOutput.ValidateClosedGzipIdentity(output.RecordsPath, stored.LongLength,
            report.GetProperty("Sha256").GetString()!, decoded.LongLength, report.GetProperty("DecodedSha256").GetString()!);
        var events = rows.Where(row => row.TryGetProperty("Boundary", out var boundary) &&
            boundary.GetString() == "joint_operation_timing").ToArray();
        var expected = new[] { "T608.CopyTail.Success|begin", "T608.CopyTail.Success|end",
            "T608.CopyTail.Sentinel|begin", "T608.CopyTail.Sentinel|end" };
        Assert.Equal(expected, events.Select(CopyTailEventName).ToArray());
        Assert.Equal(2, witness.SuccessOrdinal);
        Assert.Equal(4, witness.SentinelOrdinal);
        Assert.True(witness.ExactSentinelBeforeClose);
        var completion = report.GetProperty("PassiveTimingCompletion");
        var clock = completion.GetProperty("ClockFrequency").GetInt64();
        Assert.Equal(Stopwatch.Frequency, clock);
        Assert.True(completion.GetProperty("Complete").GetBoolean());
        Assert.Equal("sealed-passive-events-v1", completion.GetProperty("Mode").GetString());
        foreach (var field in new[] { "ExpectedRecords", "TransportAttempted", "TransportStored" })
            Assert.Equal(4, completion.GetProperty(field).GetInt64());
        var status = completion.GetProperty("Events");
        Assert.True(status.GetProperty("Enabled").GetBoolean());
        foreach (var field in new[] { "Attempted", "Enqueued", "Drained" })
            Assert.Equal(4, status.GetProperty(field).GetInt64());
        Assert.Equal(0, status.GetProperty("Pending").GetInt64());
        Assert.Equal(0, status.GetProperty("Failed").GetInt64());
        var snapshot = completion.GetProperty("Snapshot");
        Assert.True(snapshot.GetProperty("Closed").GetBoolean());
        Assert.True(snapshot.GetProperty("Complete").GetBoolean());
        Assert.Equal(0, snapshot.GetProperty("OpenScopes").GetInt64());
        Assert.Equal(0, snapshot.GetProperty("LateCompletions").GetInt64());
        var snapshots = snapshot.GetProperty("Rows").EnumerateArray().ToArray();
        Assert.Equal(2, snapshots.Length);
        var fields = new[] { "Boundary", "Operation", "State", "Milliseconds", "Sequence", "PID",
            "TotalElapsedMilliseconds", "Transport", "PassiveSequence", "ScopeId", "TimestampTicks", "ElapsedTicks",
            "Late", "ObserverCompletedBefore", "ObserverGateMillisecondsBefore", "ObserverBuildSerializeMillisecondsBefore",
            "ObserverWriteMillisecondsBefore" };
        for (var index = 0; index < events.Length; index++)
        {
            var row = events[index];
            Assert.Equal(fields, row.EnumerateObject().Select(field => field.Name).ToArray());
            Assert.Equal(index + 1, row.GetProperty("Sequence").GetInt64());
            Assert.Equal(index + 1, row.GetProperty("PassiveSequence").GetInt64());
            Assert.Equal(index, row.GetProperty("ObserverCompletedBefore").GetInt64());
            Assert.Equal(Environment.ProcessId, row.GetProperty("PID").GetInt32());
            Assert.Equal("sealed-passive-events-v1", row.GetProperty("Transport").GetString());
            Assert.False(row.GetProperty("Late").GetBoolean());
            Assert.True(row.GetProperty("TimestampTicks").GetInt64() > 0);
            Assert.Equal(index / 2 + 1, row.GetProperty("ScopeId").GetInt64());
            var ticks = row.GetProperty("ElapsedTicks").GetInt64();
            Assert.True(ticks >= 0);
            Assert.Equal(ticks * 1000d / clock, row.GetProperty("Milliseconds").GetDouble());
            if (index % 2 == 0) Assert.Equal(0, ticks);
            else
            {
                Assert.Equal(row.GetProperty("TimestampTicks").GetInt64() -
                    events[index - 1].GetProperty("TimestampTicks").GetInt64(), ticks);
                var name = row.GetProperty("Operation").GetString();
                var counter = Assert.Single(snapshots, item => item.GetProperty("Name").GetString() == name);
                Assert.Equal(1, counter.GetProperty("Started").GetInt64());
                Assert.Equal(1, counter.GetProperty("Completed").GetInt64());
                Assert.Equal(row.GetProperty("Milliseconds").GetDouble(), counter.GetProperty("InclusiveMilliseconds").GetDouble());
                Assert.Equal(row.GetProperty("Milliseconds").GetDouble(), counter.GetProperty("MaxMilliseconds").GetDouble());
            }
        }
        var observed = fixture.CaptureCopyRangeControlRows();
        Assert.Equal(expected, observed.Select(row => row.Name + "|" + row.State).ToArray());
        Assert.Equal(observed.Select(row => row.Milliseconds), events.Select(row => row.GetProperty("Milliseconds").GetDouble()));
        Assert.Equal(4, report.GetProperty("TimingAttempted").GetInt64());
        Assert.Equal(4, report.GetProperty("TimingStored").GetInt64());
        Assert.Equal(rows.Length - events.Length, report.GetProperty("OutputAttempted").GetInt64());
        Assert.Equal(report.GetProperty("OutputAttempted").GetInt64(), report.GetProperty("OutputStored").GetInt64());
        var costs = report.GetProperty("TimingWriteCosts").EnumerateArray().ToArray();
        Assert.Equal(4, costs.Length);
        foreach (var cost in costs)
        {
            Assert.Single(events, row => row.GetProperty("Operation").GetString() == cost.GetProperty("Operation").GetString() &&
                row.GetProperty("State").GetString() == cost.GetProperty("State").GetString());
            foreach (var field in new[] { "Attempted", "Stored", "Measured" }) Assert.Equal(1, cost.GetProperty(field).GetInt64());
        }
        // Saved live rows may omit buffered events, but may never invent or alter an original row.
        var liveEvents = witness.LiveRows.Where(row => row.TryGetProperty("Boundary", out var boundary) &&
            boundary.GetString() == "joint_operation_timing").ToArray();
        foreach (var row in liveEvents)
        {
            var sequence = row.GetProperty("Sequence").GetInt64();
            Assert.InRange(sequence, 1, events.LongLength);
            Assert.Equal(events[(int)sequence - 1].GetRawText(), row.GetRawText());
        }
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(witness.PrefixPath))), witness.StoredPrefixSha256);
        var oracle = Path.Combine(output.DiagnosticDirectory, "tail-visibility-oracle.json");
        File.WriteAllText(oracle, JsonSerializer.Serialize(new
        {
            Kind = "T608CopyTailVisibilityOracle", witness.PrefixPath, witness.StoredPrefixBytes, witness.StoredPrefixSha256,
            witness.SuccessOrdinal, witness.SentinelOrdinal, witness.ExactSentinelBeforeClose, witness.TrailingDecodedBytes,
            LiveRows = witness.LiveRows, LiveEventNames = liveEvents.Select(CopyTailEventName).ToArray(),
            ExpectedEventNames = expected, ClosedEvents = events, FunctionalBodyReleased = true,
            DiagnosticComplete = true, FixtureClosed = true, RuntimeLeaseIssuerAndSemanticOwnerAbsent = true
        }, new JsonSerializerOptions { WriteIndented = true }));
        _output.WriteLine(JsonSerializer.Serialize(new { Kind = "T608CopyTailVisibilityReference", Path = oracle,
            output.ReportPath, output.RecordsPath, witness.PrefixPath, ExactSentinelBeforeClose = true, FixtureClosed = true }));
        // The only intended RED: real completed events were not physically visible at the saved pre-close boundary.
        Assert.Equal(expected, liveEvents.Select(CopyTailEventName).ToArray());
    }

    /// <summary>Parses every complete row and retains only an actually unfinished decoded suffix as partial DATA.</summary>
    /// <param name="bytes">Actual decoded bytes from a saved live prefix or a closed capture.</param>
    /// <returns>Copied complete JSON rows and the exact count of unparsed final-fragment bytes.</returns>
    private static (JsonElement[] Rows, int Trailing) ReadCopyTailRows(byte[] bytes)
    {
        var rows = new List<JsonElement>();
        var start = 0;
        for (var index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] != '\n') continue;
            using var document = JsonDocument.Parse(bytes.AsMemory(start, index - start));
            rows.Add(document.RootElement.Clone());
            start = index + 1;
        }
        return (rows.ToArray(), bytes.Length - start);
    }

    /// <summary>Reads the literal original name and boundary of one real timing row.</summary>
    /// <param name="row">A complete parsed timing record.</param>
    /// <returns>The comparison-only name and boundary string.</returns>
    private static string CopyTailEventName(JsonElement row) =>
        row.GetProperty("Operation").GetString() + "|" + row.GetProperty("State").GetString();

    /// <summary>Detached saved-prefix DATA, without an owner, authority or open writer.</summary>
    private sealed record CopyTailWitness(JsonElement[] LiveRows, string PrefixPath, long StoredPrefixBytes,
        string StoredPrefixSha256, int TrailingDecodedBytes, int SuccessOrdinal, int SentinelOrdinal, bool ExactSentinelBeforeClose);

    private sealed partial class Fixture
    {
        /// <summary>Creates real success/unwind boundaries and saves their live physical prefix before any message flush.</summary>
        /// <returns>The actual detached prefix and ordinals after checking the same sentinel before closing.</returns>
        internal CopyTailWitness CaptureCopyTailControlBody()
        {
            Assert.Null(_runtime); Assert.Null(_lease); Assert.Null(_jointBundle);
            Assert.Null(_jointContinuation); Assert.Null(_jointReservation);
            Assert.Null(Files.GetCurrentSemanticDataOperation());
            var sentinel = new InvalidOperationException("T608 actual copy-tail sentinel.");
            Exception? caught = null;
            int successOrdinal, sentinelOrdinal = -1;
            using (Files.EnterOperationTimingContext()) // synchronous only; never spans DisposeAsync
            {
                try { using (Files.MeasureOperation("T608.CopyTail.Success")) { } }
                finally { successOrdinal = CaptureCopyPhaseBoundary(); }
                try
                {
                    try { using (Files.MeasureOperation("T608.CopyTail.Sentinel")) throw sentinel; }
                    finally { sentinelOrdinal = CaptureCopyPhaseBoundary(); }
                }
                catch (Exception failure) { caught = failure; }
            }
            Assert.Same(sentinel, caught); // exact functional exception BEFORE separate full closing prerequisites
            Assert.Equal(2, successOrdinal); Assert.Equal(4, sentinelOrdinal);
            Assert.Null(_runtime); Assert.Null(_lease); Assert.Null(Files.GetCurrentSemanticDataOperation());
            byte[] stored;
            using (var input = new FileStream(_output.RecordsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var bytes = new MemoryStream()) { input.CopyTo(bytes); stored = bytes.ToArray(); }
            var prefixPath = Path.Combine(_output.DiagnosticDirectory, "tail-live-prefix.jsonl.gz");
            File.WriteAllBytes(prefixPath, stored); // preserve the exact witness, never repair/append a footer
            using var memory = new MemoryStream(stored, writable: false);
            using var gzip = new GZipStream(memory, CompressionMode.Decompress);
            using var decoded = new MemoryStream();
            gzip.CopyTo(decoded); // IO/decompression errors propagate; never relabeled as empty rows
            var (rows, trailing) = ReadCopyTailRows(decoded.ToArray());
            Assert.Single(rows, row => row.TryGetProperty("Kind", out var kind) && kind.GetString() == "PassiveNamedTimingMode");
            Assert.All(rows, row =>
            {
                Assert.Equal(JsonValueKind.Object, row.ValueKind);
                if (row.TryGetProperty("Boundary", out var boundary) && boundary.GetString() == "joint_operation_timing")
                    Assert.Contains(row.GetProperty("Operation").GetString(), new[] { "T608.CopyTail.Success", "T608.CopyTail.Sentinel" });
                else if (row.TryGetProperty("Kind", out var kind)) Assert.Equal("PassiveNamedTimingMode", kind.GetString());
                else Assert.Equal("JointBundleFixture.AllocateNamespace", row.GetProperty("Name").GetString());
            });
            var witness = new CopyTailWitness(rows, prefixPath, stored.LongLength, Convert.ToHexString(SHA256.HashData(stored)),
                trailing, successOrdinal, sentinelOrdinal, ReferenceEquals(sentinel, caught));
            // Only now may a normal full original message flush the owning stream.
            _output.WriteLine(JsonSerializer.Serialize(new { Kind = "T608CopyTailSamples", witness.SuccessOrdinal,
                witness.SentinelOrdinal, witness.ExactSentinelBeforeClose, witness.StoredPrefixBytes,
                RuntimeLeaseIssuerAndSemanticOwnerAbsent = true }));
            return witness;
        }
    }
}
