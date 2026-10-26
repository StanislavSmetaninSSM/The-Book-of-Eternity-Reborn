using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class JointBundleComponentTests
{
    /// <summary>Requires exact transferred ranges at the same five manual copy boundaries after complete fixture closing.</summary>
    /// <param name="passiveTimingEvents">Whether real scopes use the existing sealed recorder instead of the original callback.</param>
    /// <param name="gzipRecords">Whether the existing records sink losslessly compresses the same full records.</param>
    /// <returns>Completion after body release, recorder/output hashing and actual root cleanup, before the strict range oracle.</returns>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task PassiveCopyRanges_RequireTransferredEventsAtAllFiveSamples(bool passiveTimingEvents, bool gzipRecords)
    {
        var fixture = new Fixture(_output, nameof(PassiveCopyRanges_RequireTransferredEventsAtAllFiveSamples),
            passiveTimingEvents: passiveTimingEvents, gzipRecords: gzipRecords);
        CopyRangeSample[] samples;
        await using (fixture)
            samples = fixture.CaptureCopyRangeControlBody();
        var observed = fixture.CaptureCopyRangeControlRows();
        var output = fixture.Output;
        using var report = JsonDocument.Parse(File.ReadAllText(output.ReportPath));
        var receipt = report.RootElement;
        Assert.True(receipt.GetProperty("DiagnosticComplete").GetBoolean());
        Assert.True(receipt.GetProperty("WriterClosed").GetBoolean());
        Assert.Empty(receipt.GetProperty("Failures").EnumerateArray());
        Assert.Equal(passiveTimingEvents, receipt.GetProperty("PassiveTimingRequired").GetBoolean());
        var closing = receipt.GetProperty("FixtureClosing");
        Assert.Equal(output.ExpectedOwnedRoot, closing.GetProperty("Owned").GetString());
        Assert.True(closing.GetProperty("Closing").GetBoolean());
        Assert.False(closing.GetProperty("Retained").GetBoolean());
        Assert.False(Directory.Exists(output.ExpectedOwnedRoot));
        Assert.False(File.Exists(output.ExpectedOwnedRoot));
        Assert.Equal(new FileInfo(output.RecordsPath).Length, receipt.GetProperty("StoredBytes").GetInt64());
        using (var stored = File.OpenRead(output.RecordsPath))
            Assert.Equal(Convert.ToHexString(SHA256.HashData(stored)), receipt.GetProperty("Sha256").GetString());
        Assert.Equal(gzipRecords ? "gzip-jsonl-utf8-v1" : "jsonl-utf8-v1", receipt.GetProperty("RecordsEncoding").GetString());
        Assert.Equal(gzipRecords, receipt.GetProperty("GzipFooterClosed").GetBoolean());
        var decoded = ReadClosedCopyRangeRecords(output.RecordsPath, gzipRecords);
        var lines = new UTF8Encoding(false, true).GetString(decoded).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.NotEmpty(lines);
        Assert.Equal((byte)'\n', decoded[^1]);
        var persisted = new List<CopyRangeObservation>();
        foreach (var line in lines)
        {
            using var row = JsonDocument.Parse(line.TrimEnd('\r'));
            var value = row.RootElement;
            if (value.TryGetProperty("Boundary", out var boundary) && boundary.GetString() == "joint_operation_timing")
                persisted.Add(new(value.GetProperty("Operation").GetString()!, value.GetProperty("State").GetString()!,
                    value.GetProperty("Milliseconds").GetDouble()));
        }
        Assert.Equal(observed, persisted.ToArray());
        Assert.Equal(observed.LongLength, receipt.GetProperty("TimingStored").GetInt64());
        Assert.Equal(receipt.GetProperty("TimingAttempted").GetInt64(), receipt.GetProperty("TimingStored").GetInt64());
        if (gzipRecords)
        {
            var decodedHash = Convert.ToHexString(SHA256.HashData(decoded));
            Assert.Equal(decoded.LongLength, receipt.GetProperty("DecodedBytes").GetInt64());
            Assert.Equal(lines.LongLength, receipt.GetProperty("DecodedRows").GetInt64());
            Assert.Equal(decodedHash, receipt.GetProperty("DecodedSha256").GetString());
            JointBundleDiagnosticOutput.ValidateClosedGzipIdentity(output.RecordsPath,
                receipt.GetProperty("StoredBytes").GetInt64(), receipt.GetProperty("Sha256").GetString()!, decoded.LongLength, decodedHash);
        }
        var names = new[] { "BeforeDirect", "DirectControl", "BetweenDirectAndPrepared", "PreparedControl",
            "BetweenPreparedAndEffect", "EffectControl", "AfterEffect" };
        var expectedAll = names.SelectMany(name => new[] { "T608.CopyRange." + name + "|begin", "T608.CopyRange." + name + "|end" }).ToArray();
        var actualAll = observed.Where(row => row.Name.StartsWith("T608.CopyRange.", StringComparison.Ordinal))
            .Select(row => row.Name + "|" + row.State).ToArray();
        Assert.Equal(expectedAll, actualAll); // all original events survive repeated drains and closing, even under RED
        var actualRanges = samples.Select(sample => observed.Skip(sample.Start).Take(sample.End - sample.Start).ToArray()).ToArray();
        var oraclePath = Path.Combine(output.DiagnosticDirectory, "range-oracle.json");
        File.WriteAllText(oraclePath, JsonSerializer.Serialize(new
        {
            Kind = "T608CopyRangeOracle", passiveTimingEvents, gzipRecords, Samples = samples,
            Observations = observed, ActualRanges = actualRanges, BodyReleased = true,
            DiagnosticComplete = true, FixtureClosed = true, SemanticOwnerWasNull = true
        }, new JsonSerializerOptions { WriteIndented = true }));
        _output.WriteLine(JsonSerializer.Serialize(new { Kind = "T608CopyRangeOracleReference", Path = oraclePath,
            output.ReportPath, output.RecordsPath, SampleCount = samples.Length, BodyReleased = true, FixtureClosed = true }));
        // Intended causal RED is only the final missing-range mismatch, after all storage/closing prerequisites.
        for (var i = 0; i < samples.Length; i++)
            Assert.Equal(new[] { samples[i].Operation + "|begin", samples[i].Operation + "|end" },
                actualRanges[i].Select(row => row.Name + "|" + row.State).ToArray());
    }

    /// <summary>Reads the actual closed sink without changing its original bytes or serialization.</summary>
    /// <param name="path">The existing stored records file.</param>
    /// <param name="gzip">Whether the actual fixture forwarded gzip mode.</param>
    /// <returns>All decoded bytes for the small constructor-only control.</returns>
    private static byte[] ReadClosedCopyRangeRecords(string path, bool gzip)
    {
        using var stored = File.OpenRead(path);
        using var bytes = new MemoryStream();
        if (gzip)
        {
            using var decoded = new GZipStream(stored, CompressionMode.Decompress, leaveOpen: true);
            decoded.CopyTo(bytes);
        }
        else stored.CopyTo(bytes);
        return bytes.ToArray();
    }

    /// <summary>Retains the actual sampled ordinal pair and its expected test-control operation.</summary>
    private sealed record CopyRangeSample(string Role, int Start, int End, string Operation);

    /// <summary>Detaches original observed values without granting an owner or authority.</summary>
    private sealed record CopyRangeObservation(string Name, string State, double Milliseconds);

    private sealed partial class Fixture
    {
        /// <summary>Exercises the exact five boundary roles with actual scopes and distinct neighboring events.</summary>
        /// <returns>Saved live samples; no range-correctness assertion is made before closing.</returns>
        internal CopyRangeSample[] CaptureCopyRangeControlBody()
        {
            Assert.Null(_runtime);
            Assert.Null(_lease);
            Assert.Null(Files.GetCurrentSemanticDataOperation());
            CopyRangeSample[] samples;
            using (Files.EnterOperationTimingContext()) // synchronous only: never spans body release/DisposeAsync
            {
                void Scope(string name) { using (Files.MeasureOperation("T608.CopyRange." + name)) { } }
                Scope("BeforeDirect");
                var directStart = ResetCandidateCapture("T608.CopyRange.Direct");
                Scope("DirectControl");
                var directEnd = CaptureObservationOrdinal();
                FlushPassiveTimings();
                FlushPassiveTimings(); // no duplicates on repeated drain
                Scope("BetweenDirectAndPrepared"); // pending neighbor makes the start-role drain necessary
                var preparedStart = CaptureObservationOrdinal();
                Scope("PreparedControl");
                var preparedEnd = CaptureObservationOrdinal();
                FlushPassiveTimings();
                FlushPassiveTimings();
                Scope("BetweenPreparedAndEffect"); // start must exclude this independently pending neighbor
                var effectStart = CaptureObservationOrdinal();
                Scope("EffectControl");
                var effectEnd = CaptureObservationOrdinal();
                Scope("AfterEffect"); // owning close must retain this final original pair too
                samples = [new("DirectEnd", directStart, directEnd, "T608.CopyRange.DirectControl"),
                    new("PreparedStartEnd", preparedStart, preparedEnd, "T608.CopyRange.PreparedControl"),
                    new("EffectStartEnd", effectStart, effectEnd, "T608.CopyRange.EffectControl")];
                _ = CompleteCandidateCapture("T608.CopyRange.Direct", directStart); // close phase, never resample RED ordinals
            }
            Assert.Null(Files.GetCurrentSemanticDataOperation());
            _output.WriteLine(JsonSerializer.Serialize(new { Kind = "T608CopyRangeSamples", Samples = samples,
                RuntimeAbsent = _runtime is null, LeaseAbsent = _lease is null, SemanticOwnerWasNull = true }));
            return samples;
        }

        /// <summary>Exports all original transferred values after fixture/window/output closing.</summary>
        /// <returns>Independent complete row DATA; neighboring events are retained.</returns>
        internal CopyRangeObservation[] CaptureCopyRangeControlRows() => _observations
            .Select(row => new CopyRangeObservation(row.Name, row.Boundary, row.Milliseconds)).ToArray();
    }
}
