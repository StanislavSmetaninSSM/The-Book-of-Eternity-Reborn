using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Core;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class JointBundleComponentTests
{
    /// <summary>Requires the actual returned ordinary-read phase to satisfy the same recorded admission tree invariant.</summary>
    /// <returns>Completion after real producer/source/storage/root prerequisites and the final existing strict reader.</returns>
    [Fact]
    public async Task OrdinaryReadPathReturnedPhase_PreservesRecordedTreeContract()
    {
        var fixture = new Fixture(_output, nameof(OrdinaryReadPathReturnedPhase_PreservesRecordedTreeContract),
            passiveTimingEvents: true, gzipRecords: true);
        OrdinaryPhaseControlState producer;
        await using (fixture)
            producer = fixture.CaptureActualOrdinaryPhaseForTree();
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
        JointBundleDiagnosticOutput.ValidateClosedGzipIdentity(output.RecordsPath, stored.LongLength,
            report.GetProperty("Sha256").GetString()!, decoded.LongLength, report.GetProperty("DecodedSha256").GetString()!);
        var events = rows.Where(row => row.TryGetProperty("Boundary", out var boundary) &&
            boundary.GetString() == "joint_operation_timing").ToArray();
        var expected = new[] { "OrdinaryReadPath.OpenPhase|begin", "OrdinaryReadPath.Phase|begin",
            "OrdinaryReadPath.OpenPhase|end", "OrdinaryReadPath.Phase|end" };
        Assert.Equal(expected, events.Select(CopyTailEventName).ToArray());
        var observations = fixture.CaptureOwnedOrdinaryPhaseRows();
        Assert.Equal(4, observations.Length);
        var completion = report.GetProperty("PassiveTimingCompletion");
        Assert.True(completion.GetProperty("Complete").GetBoolean());
        Assert.Equal("sealed-passive-events-v1", completion.GetProperty("Mode").GetString());
        Assert.Equal(Stopwatch.Frequency, completion.GetProperty("ClockFrequency").GetInt64());
        foreach (var field in new[] { "ExpectedRecords", "TransportAttempted", "TransportStored" })
            Assert.Equal(4, completion.GetProperty(field).GetInt64());
        var status = completion.GetProperty("Events");
        Assert.True(status.GetProperty("Enabled").GetBoolean());
        foreach (var field in new[] { "Attempted", "Enqueued", "Drained" }) Assert.Equal(4, status.GetProperty(field).GetInt64());
        Assert.Equal(0, status.GetProperty("Pending").GetInt64());
        Assert.Equal(0, status.GetProperty("Failed").GetInt64());
        var snapshot = completion.GetProperty("Snapshot");
        Assert.True(snapshot.GetProperty("Complete").GetBoolean());
        Assert.True(snapshot.GetProperty("Closed").GetBoolean());
        Assert.Equal(0, snapshot.GetProperty("OpenScopes").GetInt64());
        Assert.Equal(0, snapshot.GetProperty("LateCompletions").GetInt64());
        var counters = snapshot.GetProperty("Rows").EnumerateArray().ToArray();
        Assert.Equal(2, counters.Length);
        for (var index = 0; index < events.Length; index++)
        {
            var row = events[index]; var retained = observations[index];
            Assert.Equal(17, row.EnumerateObject().Count());
            Assert.Equal(index + 1, row.GetProperty("Sequence").GetInt64());
            Assert.Equal(index + 1, row.GetProperty("PassiveSequence").GetInt64());
            Assert.Equal(Environment.ProcessId, row.GetProperty("PID").GetInt32());
            Assert.Equal("sealed-passive-events-v1", row.GetProperty("Transport").GetString());
            Assert.False(row.GetProperty("Late").GetBoolean());
            Assert.Equal(retained.Name, row.GetProperty("Operation").GetString());
            Assert.Equal(retained.Boundary, row.GetProperty("State").GetString());
            Assert.Equal(retained.Milliseconds, row.GetProperty("Milliseconds").GetDouble());
            Assert.Equal(retained.SourceScopeId, row.GetProperty("ScopeId").GetInt64());
            Assert.True(retained.SourceScopeId > 0);
            Assert.Equal(row.GetProperty("ElapsedTicks").GetInt64() * 1000d / Stopwatch.Frequency, retained.Milliseconds);
        }
        Assert.Equal(events[0].GetProperty("ScopeId").GetInt64(), events[2].GetProperty("ScopeId").GetInt64());
        Assert.Equal(events[1].GetProperty("ScopeId").GetInt64(), events[3].GetProperty("ScopeId").GetInt64());
        Assert.True(events[1].GetProperty("ScopeId").GetInt64() > events[0].GetProperty("ScopeId").GetInt64());
        for (var begin = 0; begin < 2; begin++)
        {
            var end = begin + 2; var name = events[begin].GetProperty("Operation").GetString();
            Assert.Equal(0, events[begin].GetProperty("ElapsedTicks").GetInt64());
            Assert.Equal(events[end].GetProperty("TimestampTicks").GetInt64() - events[begin].GetProperty("TimestampTicks").GetInt64(),
                events[end].GetProperty("ElapsedTicks").GetInt64());
            var counter = Assert.Single(counters, item => item.GetProperty("Name").GetString() == name);
            Assert.Equal(1, counter.GetProperty("Started").GetInt64());
            Assert.Equal(1, counter.GetProperty("Completed").GetInt64());
            Assert.Equal(events[end].GetProperty("Milliseconds").GetDouble(), counter.GetProperty("InclusiveMilliseconds").GetDouble());
            Assert.Equal(events[end].GetProperty("Milliseconds").GetDouble(), counter.GetProperty("MaxMilliseconds").GetDouble());
        }
        Assert.Equal(4, report.GetProperty("TimingAttempted").GetInt64());
        Assert.Equal(4, report.GetProperty("TimingStored").GetInt64());
        Assert.Equal(rows.Length - events.Length, report.GetProperty("OutputStored").GetInt64());
        Assert.Equal(report.GetProperty("OutputAttempted").GetInt64(), report.GetProperty("OutputStored").GetInt64());
        var costs = report.GetProperty("TimingWriteCosts").EnumerateArray().ToArray(); Assert.Equal(4, costs.Length);
        foreach (var cost in costs)
        {
            Assert.Single(events, row => row.GetProperty("Operation").GetString() == cost.GetProperty("Operation").GetString() &&
                row.GetProperty("State").GetString() == cost.GetProperty("State").GetString());
            foreach (var field in new[] { "Attempted", "Stored", "Measured" }) Assert.Equal(1, cost.GetProperty(field).GetInt64());
        }
        Assert.True(producer.ActiveBeforeClose && producer.ClosedAfterDispose && producer.NoRuntimeLeaseIssuerOrSemanticOwner);
        Assert.Equal(1, producer.Statistics.Invocations);
        Assert.Equal(0, producer.Statistics.Requests); Assert.Equal(0, producer.Statistics.SourceQueries); Assert.Equal(0, producer.Statistics.Hits);
        var oracle = Path.Combine(output.DiagnosticDirectory, "ordinary-phase-tree-oracle.json");
        File.WriteAllText(oracle, JsonSerializer.Serialize(new
        {
            Kind = "T608OrdinaryPhaseTreeOracle", Producer = producer, ClosedEvents = events,
            RetainedObservations = observations.Select(row => new { row.Name, row.Boundary, row.Milliseconds, row.SourceScopeId }).ToArray(),
            DiagnosticComplete = true, FixtureClosed = true, ExpectedEventNames = expected
        }, new JsonSerializerOptions { WriteIndented = true }));
        _output.WriteLine(JsonSerializer.Serialize(new { Kind = "T608OrdinaryPhaseTreeReference", Path = oracle,
            output.ReportPath, output.RecordsPath, FixtureClosed = true }));
        // Intended causal RED is ONLY the original strict reader's OpenPhase/Phase mismatch after all prerequisites.
        fixture.AssertOwnedOrdinaryPhaseTree();
    }

    /// <summary>Actual producer state detached after its returned object closed, without storage authority.</summary>
    private sealed record OrdinaryPhaseControlState(bool ActiveBeforeClose, bool ClosedAfterDispose,
        bool NoRuntimeLeaseIssuerOrSemanticOwner, AdmissionDirectoryStatistics Statistics);

    private sealed partial class Fixture
    {
        /// <summary>Calls the actual phase factory and returned Dispose under the existing real passive recorder.</summary>
        /// <returns>Checked actual active/closed and manager statistics; no initializer, issuer or lease is created.</returns>
        internal OrdinaryPhaseControlState CaptureActualOrdinaryPhaseForTree()
        {
            Assert.Null(_runtime); Assert.Null(_lease); Assert.Null(_jointBundle);
            Assert.Null(_jointContinuation); Assert.Null(_jointReservation); Assert.Null(Files.GetCurrentSemanticDataOperation());
            var before = Files.CaptureOrdinaryReadPathStatistics();
            bool active, closed;
            using (Files.EnterOperationTimingContext()) // synchronous only; no await crosses the actual phase lifetime
            {
                var phase = Files.BeginOrdinaryReadPathPhase();
                Assert.NotNull(phase);
                try { active = ReferenceEquals(phase, phase.Active); Assert.True(active); }
                finally { phase.Dispose(); }
                closed = phase.CaptureState().Closed;
                Assert.True(closed); Assert.Null(phase.Active);
            }
            var statistics = Files.CaptureOrdinaryReadPathStatistics() - before;
            Assert.Equal(1, statistics.Invocations);
            Assert.Null(_runtime); Assert.Null(_lease); Assert.Null(Files.GetCurrentSemanticDataOperation());
            Assert.Equal(4, CaptureCopyPhaseBoundary());
            var state = new OrdinaryPhaseControlState(active, closed, true, statistics);
            _output.WriteLine(JsonSerializer.Serialize(new { Kind = "T608OrdinaryPhaseProducer", State = state }));
            return state;
        }

        /// <summary>Exports the same owned observation rows with the actual original source identity scalar.</summary>
        /// <returns>Detached existing-queue DATA; legacy zero is not upgraded into an identity.</returns>
        internal (string Name, string Boundary, double Milliseconds, long SourceScopeId)[] CaptureOwnedOrdinaryPhaseRows() => _observations.ToArray();

        /// <summary>Requires strict function nesting and the exact actual returned-lifetime association for this source-owned interval.</summary>
        internal void AssertOwnedOrdinaryPhaseTree()
        {
            var rows = ReadAdmissionTimingRange((0, _observations.Count));
            var analysis = ReadAdmissionTimingTree(rows);
            Assert.True(analysis.SourceScopeIdPairingAvailable); Assert.Equal(4, analysis.RowsConsumed);
            Assert.Equal("OrdinaryReadPath.OpenPhase", Assert.Single(analysis.Functions).Name);
            var lifetime = Assert.Single(analysis.Lifetimes);
            Assert.Equal(rows[0].SourceScopeId, lifetime.FactoryScopeId);
            Assert.Equal(rows[1].SourceScopeId, lifetime.SourceScopeId);
            Assert.Equal(1, lifetime.BeginOrdinal); Assert.Equal(2, lifetime.FactoryEndOrdinal); Assert.Equal(3, lifetime.EndOrdinal);
            File.WriteAllText(Path.Combine(_output.DiagnosticDirectory, "ordinary-phase-tree-result.json"), JsonSerializer.Serialize(new
            {
                Kind = "T608OrdinaryPhaseTreeResult", FunctionStrictLifo = true,
                AllObservedRowsConsumed = analysis.RowsConsumed == rows.Length, analysis.SourceScopeIdPairingAvailable,
                FunctionNames = analysis.Functions.Select(node => node.Name).ToArray(), ReturnedLifetime = lifetime,
                ReturnedReadPathLifetimeProof = DescribeReturnedReadPathLifetimes(analysis), ActualGenuineAuthorityClaim = false
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
