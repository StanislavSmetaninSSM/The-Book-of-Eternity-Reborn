using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class JointBundleComponentTests
{
    /// <summary>Requires original events on disk while an actual synchronous measured body remains blocked.</summary>
    /// <returns>Completion after guaranteed body release/join, full source/storage/root closing and the saved-prefix oracle.</returns>
    [Fact]
    public async Task ComponentCandidateEvents_ArePersistentWhileSynchronousBodyIsBlocked()
    {
        var fixture = new Fixture(_output, nameof(ComponentCandidateEvents_ArePersistentWhileSynchronousBodyIsBlocked),
            passiveTimingEvents: true, gzipRecords: true, continuingPassiveTimings: true);
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var expectedBytes = new byte[] { 3, 17, 41, 99 };
        var sourcePath = Path.Combine(fixture.Output.ExpectedOwnedRoot, "visibility-source.bin");
        byte[]? actualBytes = null;
        var sourceReads = 0;
        Task? body = null;
        var visible = false;
        byte[] prefix = [];
        JsonElement[] liveRows = [];
        var trailing = 0;
        int start = -1, end = -1;
        await using (fixture)
        {
            try
            {
                File.WriteAllBytes(sourcePath, expectedBytes);
                start = fixture.BeginCandidateVisibilityControl();
                body = Task.Run(() =>
                {
                    using (fixture.Files.EnterOperationTimingContext()) // actual synchronous producer; no await while registered
                    using (var scope = fixture.Files.MeasureOperation("T608.CandidateVisibility.Subject"))
                    {
                        Assert.NotNull(scope);
                        actualBytes = File.ReadAllBytes(sourcePath);
                        Interlocked.Increment(ref sourceReads);
                        entered.SetResult();
                        release.Wait();
                    }
                });
                var first = await Task.WhenAny(entered.Task, body);
                await first;
                Assert.True(entered.Task.IsCompletedSuccessfully);
                Assert.False(body.IsCompleted);
                Assert.Equal(expectedBytes, actualBytes);
                Assert.Equal(1, sourceReads);
                fixture.RequireCandidateVisibilityHasNoAuthority();
                var began = Stopwatch.GetTimestamp();
                do
                {
                    (prefix, liveRows, trailing) = ReadCandidateVisibilityPrefix(fixture.Output.RecordsPath);
                    visible = liveRows.Any(row => row.TryGetProperty("Operation", out var operation) &&
                        operation.GetString() == "T608.CandidateVisibility.Subject" && row.GetProperty("State").GetString() == "begin");
                    if (!visible) await Task.Delay(20);
                } while (!visible && Stopwatch.GetElapsedTime(began) < TimeSpan.FromSeconds(2));
                // Missing visibility is DATA until every source and closing prerequisite has passed.
                Assert.False(body.IsCompleted);
                Assert.False(File.Exists(fixture.Output.ReportPath));
            }
            finally
            {
                release.Set();
                try { if (body is not null) await body; }
                finally
                {
                    try { if (start >= 0) end = fixture.EndCandidateVisibilityControl(start); }
                    finally { fixture.CaptureCandidateVisibilityClosingTail(); }
                }
            }
        }
        fixture.RequireCandidateVisibilityHasNoAuthority();
        var output = fixture.Output;
        using var document = JsonDocument.Parse(File.ReadAllText(output.ReportPath));
        var report = document.RootElement;
        foreach (var name in new[] { "DiagnosticComplete", "WriterClosed", "TimingComplete", "PassiveTimingRequired", "GzipFooterClosed" })
            Assert.True(report.GetProperty(name).GetBoolean());
        Assert.Empty(report.GetProperty("Failures").EnumerateArray());
        var closing = report.GetProperty("FixtureClosing");
        Assert.Equal(output.ExpectedOwnedRoot, closing.GetProperty("Owned").GetString());
        Assert.True(closing.GetProperty("Closing").GetBoolean()); Assert.False(closing.GetProperty("Retained").GetBoolean());
        Assert.Empty(closing.GetProperty("Failures").EnumerateArray());
        Assert.Throws<FileNotFoundException>(() => File.GetAttributes(output.ExpectedOwnedRoot));
        var stored = File.ReadAllBytes(output.RecordsPath);
        var decoded = ReadClosedCopyRangeRecords(output.RecordsPath, gzip: true);
        var (closedRows, unfinished) = ReadCopyTailRows(decoded);
        Assert.Equal(0, unfinished);
        var storedHash = Convert.ToHexString(SHA256.HashData(stored));
        var decodedHash = Convert.ToHexString(SHA256.HashData(decoded));
        Assert.Equal(stored.LongLength, report.GetProperty("StoredBytes").GetInt64());
        Assert.Equal(storedHash, report.GetProperty("Sha256").GetString());
        Assert.Equal(decoded.LongLength, report.GetProperty("DecodedBytes").GetInt64());
        Assert.Equal(decodedHash, report.GetProperty("DecodedSha256").GetString());
        Assert.Equal(closedRows.LongLength, report.GetProperty("DecodedRows").GetInt64());
        JointBundleDiagnosticOutput.ValidateClosedGzipIdentity(output.RecordsPath, stored.LongLength, storedHash, decoded.LongLength, decodedHash);
        var events = closedRows.Where(row => row.TryGetProperty("Boundary", out var boundary) && boundary.GetString() == "joint_operation_timing").ToArray();
        var expected = new[] { "T608.CandidateVisibility.Before|begin", "T608.CandidateVisibility.Before|end",
            "T608.CandidateVisibility.Subject|begin", "T608.CandidateVisibility.Subject|end",
            "T608.CandidateVisibility.ClosingTail|begin", "T608.CandidateVisibility.ClosingTail|end" };
        Assert.Equal(expected, events.Select(CopyTailEventName));
        var observed = fixture.CaptureOwnedOrdinaryPhaseRows();
        Assert.Equal(expected, observed.Select(row => row.Name + "|" + row.Boundary));
        Assert.Equal(observed.Select(row => row.SourceScopeId), events.Select(row => row.GetProperty("ScopeId").GetInt64()));
        Assert.Equal(observed.Select(row => row.Milliseconds), events.Select(row => row.GetProperty("Milliseconds").GetDouble()));
        Assert.Equal(2, start); Assert.Equal(4, end);
        Assert.Equal(expected.Skip(2).Take(2), observed.Skip(start).Take(end - start).Select(row => row.Name + "|" + row.Boundary));
        Assert.Equal(expectedBytes, actualBytes); Assert.Equal(1, sourceReads); Assert.True(body!.IsCompletedSuccessfully);
        var completion = report.GetProperty("PassiveTimingCompletion");
        Assert.True(completion.GetProperty("Complete").GetBoolean());
        Assert.Equal(Stopwatch.Frequency, completion.GetProperty("ClockFrequency").GetInt64());
        foreach (var name in new[] { "ExpectedRecords", "TransportAttempted", "TransportStored" }) Assert.Equal(6, completion.GetProperty(name).GetInt64());
        var status = completion.GetProperty("Events"); Assert.True(status.GetProperty("Enabled").GetBoolean());
        foreach (var name in new[] { "Attempted", "Enqueued", "Drained" }) Assert.Equal(6, status.GetProperty(name).GetInt64());
        Assert.Equal(0, status.GetProperty("Pending").GetInt64()); Assert.Equal(0, status.GetProperty("Failed").GetInt64());
        var snapshot = completion.GetProperty("Snapshot");
        Assert.True(snapshot.GetProperty("Closed").GetBoolean()); Assert.True(snapshot.GetProperty("Complete").GetBoolean());
        Assert.Equal(0, snapshot.GetProperty("OpenScopes").GetInt64()); Assert.Equal(0, snapshot.GetProperty("LateCompletions").GetInt64());
        var counters = snapshot.GetProperty("Rows").EnumerateArray().ToArray(); Assert.Equal(3, counters.Length);
        var fields = new[] { "Boundary", "Operation", "State", "Milliseconds", "Sequence", "PID", "TotalElapsedMilliseconds",
            "Transport", "PassiveSequence", "ScopeId", "TimestampTicks", "ElapsedTicks", "Late", "ObserverCompletedBefore",
            "ObserverGateMillisecondsBefore", "ObserverBuildSerializeMillisecondsBefore", "ObserverWriteMillisecondsBefore" };
        double[] previousCosts = [0, 0, 0];
        for (var i = 0; i < events.Length; i++)
        {
            var row = events[i]; Assert.Equal(fields, row.EnumerateObject().Select(field => field.Name));
            Assert.Equal(i + 1, row.GetProperty("Sequence").GetInt64()); Assert.Equal(i + 1, row.GetProperty("PassiveSequence").GetInt64());
            Assert.Equal(i, row.GetProperty("ObserverCompletedBefore").GetInt64()); Assert.Equal(Environment.ProcessId, row.GetProperty("PID").GetInt32());
            Assert.Equal("sealed-passive-events-v1", row.GetProperty("Transport").GetString()); Assert.False(row.GetProperty("Late").GetBoolean());
            Assert.Equal(i / 2 + 1, row.GetProperty("ScopeId").GetInt64());
            var ticks = row.GetProperty("ElapsedTicks").GetInt64(); Assert.True(ticks >= 0);
            Assert.Equal(ticks * 1000d / Stopwatch.Frequency, row.GetProperty("Milliseconds").GetDouble());
            Assert.True(row.GetProperty("TimestampTicks").GetInt64() > 0);
            if (i % 2 == 0) Assert.Equal(0, ticks);
            else
            {
                Assert.Equal(row.GetProperty("TimestampTicks").GetInt64() - events[i - 1].GetProperty("TimestampTicks").GetInt64(), ticks);
                var counter = Assert.Single(counters, value => value.GetProperty("Name").GetString() == row.GetProperty("Operation").GetString());
                Assert.Equal(1, counter.GetProperty("Started").GetInt64()); Assert.Equal(1, counter.GetProperty("Completed").GetInt64());
                Assert.Equal(row.GetProperty("Milliseconds").GetDouble(), counter.GetProperty("InclusiveMilliseconds").GetDouble());
                Assert.Equal(row.GetProperty("Milliseconds").GetDouble(), counter.GetProperty("MaxMilliseconds").GetDouble());
            }
            for (var j = 0; j < previousCosts.Length; j++)
            {
                var cost = row.GetProperty(fields[14 + j]).GetDouble(); Assert.True(double.IsFinite(cost) && cost >= previousCosts[j]); previousCosts[j] = cost;
            }
        }
        Assert.Equal(6, report.GetProperty("TimingAttempted").GetInt64()); Assert.Equal(6, report.GetProperty("TimingStored").GetInt64());
        Assert.Equal(closedRows.Length - events.Length, report.GetProperty("OutputStored").GetInt64());
        Assert.Equal(report.GetProperty("OutputAttempted").GetInt64(), report.GetProperty("OutputStored").GetInt64());
        var costs = report.GetProperty("TimingWriteCosts").EnumerateArray().ToArray(); Assert.Equal(6, costs.Length);
        foreach (var cost in costs)
        {
            Assert.Single(events, row => row.GetProperty("Operation").GetString() == cost.GetProperty("Operation").GetString() && row.GetProperty("State").GetString() == cost.GetProperty("State").GetString());
            foreach (var field in new[] { "Attempted", "Stored", "Measured" }) Assert.Equal(1, cost.GetProperty(field).GetInt64());
        }
        var liveEvents = liveRows.Where(row => row.TryGetProperty("Boundary", out var boundary) && boundary.GetString() == "joint_operation_timing").ToArray();
        foreach (var row in liveEvents)
        {
            var sequence = row.GetProperty("Sequence").GetInt64(); Assert.InRange(sequence, 1, events.LongLength);
            Assert.Equal(events[(int)sequence - 1].GetRawText(), row.GetRawText());
        }
        var prefixPath = Path.Combine(output.DiagnosticDirectory, "candidate-visibility-prefix.gz");
        File.WriteAllBytes(prefixPath, prefix);
        var oracle = Path.Combine(output.DiagnosticDirectory, "candidate-visibility-oracle.json");
        File.WriteAllText(oracle, JsonSerializer.Serialize(new
        {
            Kind = "T608CandidateVisibilityOracle", PrefixPath = prefixPath, StoredPrefixBytes = prefix.LongLength,
            StoredPrefixSha256 = Convert.ToHexString(SHA256.HashData(prefix)), TrailingDecodedBytes = trailing,
            LiveRows = liveRows, ClosedEvents = events, RetainedObservations = observed.Select(row => new { row.Name, row.Boundary, row.Milliseconds, row.SourceScopeId }),
            Start = start, End = end, VisibleBeforeRelease = visible, BodyReleasedAndJoined = true, SourceReads = sourceReads,
            SourceBytes = actualBytes, DiagnosticComplete = true, FixtureClosed = true, RuntimeLeaseIssuerAndSemanticOwnerAbsent = true
        }, new JsonSerializerOptions { WriteIndented = true }));
        _output.WriteLine(JsonSerializer.Serialize(new { Kind = "T608CandidateVisibilityReference", Path = oracle,
            output.ReportPath, output.RecordsPath, PrefixPath = prefixPath, FixtureClosed = true }));
        // Sole intended causal RED: old after-return transfer did not persist the real begin during synchronous work.
        Assert.True(visible, "Original candidate-control begin was not persistent while the synchronous body remained blocked.");
    }

    /// <summary>Reads a small live prefix without draining, flushing, repairing or qualifying its unfinished gzip container.</summary>
    /// <param name="path">The existing writer's actual records path.</param>
    /// <returns>Untouched stored prefix bytes, every complete decoded JSON row and final-fragment byte count.</returns>
    private static (byte[] Stored, JsonElement[] Rows, int Trailing) ReadCandidateVisibilityPrefix(string path)
    {
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var stored = new MemoryStream(); input.CopyTo(stored); var raw = stored.ToArray();
        using var source = new MemoryStream(raw); using var gzip = new GZipStream(source, CompressionMode.Decompress);
        using var decoded = new MemoryStream(); gzip.CopyTo(decoded);
        var (rows, trailing) = ReadCopyTailRows(decoded.ToArray());
        return (raw, rows, trailing);
    }

    private sealed partial class Fixture
    {
        /// <summary>Checks that the constructor-only contour has no runtime, owner, lease or issuer.</summary>
        internal void RequireCandidateVisibilityHasNoAuthority()
        {
            Assert.Null(_runtime); Assert.Null(_lease); Assert.Null(_jointBundle); Assert.Null(_jointContinuation); Assert.Null(_jointReservation);
            Assert.Null(Files.GetCurrentSemanticDataOperation());
        }

        /// <summary>Records an actual neighboring pair, then samples the real reset boundary.</summary>
        /// <returns>The exact original subject start ordinal.</returns>
        internal int BeginCandidateVisibilityControl()
        {
            RequireCandidateVisibilityHasNoAuthority();
            using (Files.EnterOperationTimingContext())
            using (Files.MeasureOperation("T608.CandidateVisibility.Before")) { }
            return ResetCandidateCapture("T608.CandidateVisibility");
        }

        /// <summary>Records a real synchronous finally tail after the blocked body joined.</summary>
        internal void CaptureCandidateVisibilityClosingTail()
        {
            using (Files.EnterOperationTimingContext())
            using (Files.MeasureOperation("T608.CandidateVisibility.ClosingTail")) { }
        }

        /// <summary>Completes the subject phase before the separate finally tail, retaining every original row.</summary>
        /// <param name="offset">The exact reset boundary saved before the synchronous subject.</param>
        /// <returns>The exact transferred subject end ordinal, excluding the independent finally tail.</returns>
        internal int EndCandidateVisibilityControl(int offset)
        {
            RequireCandidateVisibilityHasNoAuthority();
            var counts = CompleteCandidateCapture("T608.CandidateVisibility", offset);
            Assert.Single(counts); Assert.Equal(1, counts["T608.CandidateVisibility.Subject"]);
            return CaptureObservationOrdinal();
        }
    }
}
