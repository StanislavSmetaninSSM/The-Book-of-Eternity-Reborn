using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class LifecyclePassiveTimingDiagnosticsTests
{
    /// <summary>Refuses a normal zero-event/no-message gzip capture when the actual writer emitted no member.</summary>
    [Fact]
    public async Task ZeroEventGzipWithoutMemberCannotClaimClosedFooterOrCompleteCapture()
    {
        var root = CreateRoot();
        var capture = new LifecyclePassiveTimingDiagnostics(nameof(ZeroEventGzipWithoutMemberCannotClaimClosedFooterOrCompleteCapture),
            _transportTestOutput, gzipRecords: true);
        capture.BindFixtureRoot(root);
        var recorder = new OperationTimingAccumulator();
        var window = recorder.StartWindow(captureEvents: true);
        capture.Arm(window); // no dummy scope, row, message or writer-fault control
        Directory.Delete(root);
        var failure = await Record.ExceptionAsync(() => capture.CompleteAsync());
        using var report = JsonDocument.Parse(File.ReadAllText(capture.ReportPath));
        var receipt = report.RootElement;
        Assert.Equal(0, new FileInfo(capture.RecordsPath).Length);
        Assert.Equal(0, receipt.GetProperty("StoredBytes").GetInt64());
        Assert.Equal(0, window.CaptureEventStatus().Attempted);
        Assert.Equal(0, window.CaptureEventStatus().Drained);
        Assert.True(window.Capture().Complete);
        Assert.True(receipt.GetProperty("PassiveTimingCompletion").GetProperty("Complete").GetBoolean());
        Assert.True(receipt.GetProperty("WriterClosed").GetBoolean());
        Assert.True(receipt.GetProperty("FixtureClosing").GetProperty("Closing").GetBoolean());
        Assert.Equal(root, receipt.GetProperty("FixtureClosing").GetProperty("Owned").GetString());
        Assert.False(receipt.GetProperty("GzipFooterClosed").GetBoolean());
        Assert.False(receipt.GetProperty("DiagnosticComplete").GetBoolean());
        Assert.IsType<AggregateException>(failure);
        Assert.Equal(JsonValueKind.Null, receipt.GetProperty("DecodedBytes").ValueKind);
        Assert.Contains(receipt.GetProperty("Failures").EnumerateArray(), row =>
            row.GetProperty("Stage").GetString() == "decoded-records-validation" &&
            row.GetProperty("Error").GetString()!.Contains("No complete gzip member", StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => JointBundleDiagnosticOutput.ValidateClosedGzipIdentity(capture.RecordsPath,
            0, receipt.GetProperty("Sha256").GetString()!, 0, receipt.GetProperty("Sha256").GetString()!));
    }

    /// <summary>Validates closed stored/decoded identity, then refuses every removed footer byte and corruption.</summary>
    [Fact]
    public async Task GzipClosedIdentityRefusesEachFooterTruncationAndCorruption()
    {
        var root = CreateRoot();
        var capture = new LifecyclePassiveTimingDiagnostics(nameof(GzipClosedIdentityRefusesEachFooterTruncationAndCorruption),
            _transportTestOutput, gzipRecords: true);
        capture.BindFixtureRoot(root);
        var recorder = new OperationTimingAccumulator();
        capture.Arm(recorder.StartWindow(captureEvents: true));
        using (recorder.Measure("gzip.integrity.actual")) { }
        Directory.Delete(root);
        await capture.CompleteAsync();
        using var document = JsonDocument.Parse(File.ReadAllText(capture.ReportPath));
        var report = document.RootElement;
        var storedBytes = report.GetProperty("StoredBytes").GetInt64();
        var storedHash = report.GetProperty("Sha256").GetString()!;
        var decodedBytes = report.GetProperty("DecodedBytes").GetInt64();
        var decodedHash = report.GetProperty("DecodedSha256").GetString()!;
        Assert.True(report.GetProperty("GzipFooterClosed").GetBoolean());
        Assert.Equal("gzip-jsonl-utf8-v1", report.GetProperty("RecordsEncoding").GetString());
        JointBundleDiagnosticOutput.ValidateClosedGzipIdentity(capture.RecordsPath, storedBytes, storedHash, decodedBytes, decodedHash);
        var original = File.ReadAllBytes(capture.RecordsPath);
        Assert.True(original.Length > 18);
        var scratch = CreateRoot();
        try
        {
            for (var removed = 1; removed <= 8; removed++)
            {
                var path = Path.Combine(scratch, "footer-minus-" + removed + ".gz");
                File.WriteAllBytes(path, original[..^removed]);
                var error = Assert.Throws<InvalidDataException>(() =>
                    JointBundleDiagnosticOutput.ValidateClosedGzipIdentity(path, storedBytes, storedHash, decodedBytes, decodedHash));
                Assert.Contains("stored identity", error.Message);
            }
            var corrupted = original.ToArray(); corrupted[corrupted.Length / 2] ^= 1;
            var corruptPath = Path.Combine(scratch, "corrupt.gz"); File.WriteAllBytes(corruptPath, corrupted);
            Assert.Throws<InvalidDataException>(() =>
                JointBundleDiagnosticOutput.ValidateClosedGzipIdentity(corruptPath, storedBytes, storedHash, decodedBytes, decodedHash));
            Assert.Throws<InvalidDataException>(() =>
                JointBundleDiagnosticOutput.ValidateClosedGzipIdentity(capture.RecordsPath, storedBytes, storedHash,
                    decodedBytes + 1, decodedHash));
        }
        finally { Directory.Delete(scratch, recursive: true); }
        AssertComplete(capture);
    }

    /// <summary>Exposes flushed gzip complete rows while retaining, then rejecting, only a malformed completed tail.</summary>
    [Fact]
    public void GzipLiveReaderKeepsCompletePrefixAndRejectsCompletedMalformedRow()
    {
        var root = CreateRoot(); var path = Path.Combine(root, "partial.jsonl.gz");
        try
        {
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite);
            using var gzip = new GZipStream(stream, CompressionLevel.Fastest);
            using var writer = new StreamWriter(gzip, new UTF8Encoding(false));
            writer.Write("{\"Kind\":\"first 日本語\"}\n{\"Kind\":"); writer.Flush();
            Assert.Single(ReadRows(path)); // decoder EOF is not a complete-container verdict
            writer.Write("\"second рана\"}\n"); writer.Flush();
            Assert.Equal(2, ReadRows(path).Length);
            writer.Write("malformed-complete-row\n"); writer.Flush();
            Assert.ThrowsAny<JsonException>(() => ReadRows(path));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    /// <summary>Preserves the component's default plain namespace and refuses a foreign closing receipt.</summary>
    [Fact]
    public void DefaultComponentIdentityAndForeignRootReceiptStayStrict()
    {
        var root = CreateRoot();
        var output = new JointBundleDiagnosticOutput(nameof(DefaultComponentIdentityAndForeignRootReceiptStayStrict),
            _transportTestOutput, repositoryRootForControl: root);
        Assert.Equal(Path.Combine(root, "TestResults", "joint-bundle-component", output.FixtureNonce), output.ExpectedOwnedRoot);
        Assert.EndsWith("records.jsonl", output.RecordsPath);
        output.WriteLine("plain default remains complete");
        output.Finish();
        Assert.Throws<InvalidDataException>(() => output.RecordFixtureClosing(root, retained: false, removed: true, []));
        using var report = JsonDocument.Parse(File.ReadAllText(output.ReportPath));
        Assert.Equal("jsonl-utf8-v1", report.RootElement.GetProperty("RecordsEncoding").GetString());
        Assert.Equal(JsonValueKind.Null, report.RootElement.GetProperty("DecodedBytes").ValueKind);
        Assert.True(report.RootElement.GetProperty("DiagnosticComplete").GetBoolean());
        if (!output.DiagnosticDirectory.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)) Directory.Delete(root);
    }

    /// <summary>Measures the actual serializer/compressor/flush path with 32768 fresh real scopes and one complete large message.</summary>
    [Fact]
    public async Task FreshManagedGzipCostControlKeepsEveryScopeAndLargeUnicodeMessage()
    {
        const int scopes = 32768;
        var root = CreateRoot();
        var capture = new LifecyclePassiveTimingDiagnostics(nameof(FreshManagedGzipCostControlKeepsEveryScopeAndLargeUnicodeMessage),
            _transportTestOutput, gzipRecords: true);
        capture.BindFixtureRoot(root);
        var recorder = new OperationTimingAccumulator();
        var window = recorder.StartWindow(captureEvents: true);
        capture.Arm(window);
        var message = "actual Unicode рана 日本語 🩹\n" + new string('傷', 65536);
        var expectedMessage = JsonSerializer.Serialize(new { Kind = "ActualLargeMessage", Payload = message });
        var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        var memoryBefore = GC.GetTotalMemory(forceFullCollection: false);
        var started = Stopwatch.GetTimestamp();
        long work = 0;
        for (var index = 0; index < scopes; index++)
            using (recorder.Measure("managed.actual.scope")) work += index;
        capture.WriteLine(expectedMessage);
        Directory.Delete(root);
        var beforeClose = Stopwatch.GetTimestamp();
        await capture.CompleteAsync();
        var closedAt = Stopwatch.GetTimestamp();
        var allocatedThroughClose = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
        var memoryAtClose = GC.GetTotalMemory(forceFullCollection: false);
        var elapsed = Stopwatch.GetElapsedTime(started, closedAt);
        using var report = JsonDocument.Parse(File.ReadAllText(capture.ReportPath));
        var receipt = report.RootElement;
        Assert.Equal((long)scopes * (scopes - 1) / 2, work);
        Assert.Equal(scopes * 2, window.CaptureEventStatus().Attempted);
        Assert.Equal(scopes * 2, window.CaptureEventStatus().Drained);
        Assert.Equal(scopes * 2 + 1, receipt.GetProperty("DecodedRows").GetInt64());
        Assert.True(receipt.GetProperty("FlushCosts").GetProperty("Attempted").GetInt64() > 0);
        Assert.Equal(receipt.GetProperty("FlushCosts").GetProperty("Attempted").GetInt64(),
            receipt.GetProperty("FlushCosts").GetProperty("Stored").GetInt64());
        long events = 0, messageRows = 0;
        using (var stream = File.OpenRead(capture.RecordsPath))
        using (var gzip = new GZipStream(stream, CompressionMode.Decompress))
        using (var reader = new StreamReader(gzip))
        {
            while (reader.ReadLine() is { } line)
            {
                using var row = JsonDocument.Parse(line);
                if (row.RootElement.TryGetProperty("Operation", out _))
                {
                    events++;
                    Assert.Equal(events, row.RootElement.GetProperty("PassiveSequence").GetInt64());
                    Assert.Equal("managed.actual.scope", row.RootElement.GetProperty("Operation").GetString());
                }
                else
                {
                    messageRows++;
                    Assert.Equal("ActualLargeMessage", row.RootElement.GetProperty("Kind").GetString());
                    Assert.Equal(message, row.RootElement.GetProperty("Payload").GetString());
                }
            }
        }
        Assert.Equal(scopes * 2, events); Assert.Equal(1, messageRows);
        JointBundleDiagnosticOutput.ValidateClosedGzipIdentity(capture.RecordsPath,
            receipt.GetProperty("StoredBytes").GetInt64(), receipt.GetProperty("Sha256").GetString()!,
            receipt.GetProperty("DecodedBytes").GetInt64(), receipt.GetProperty("DecodedSha256").GetString()!);
        AssertComplete(capture);
        _transportTestOutput.WriteLine(JsonSerializer.Serialize(new
        {
            Kind = "ActualManagedGzipCostControl", ActualScopes = scopes, ActualEvents = events, ActualWork = work,
            TotalElapsed = elapsed, AllocatedThroughClose = allocatedThroughClose,
            ManagedMemoryBefore = memoryBefore, ManagedMemoryAtClose = memoryAtClose,
            capture.ConsumerBatches, capture.LargestDetachedBatch,
            ProducerAndMessageElapsed = Stopwatch.GetElapsedTime(started, beforeClose),
            ClosingElapsed = Stopwatch.GetElapsedTime(beforeClose, closedAt),
            CaseThroughValidationElapsed = Stopwatch.GetElapsedTime(started), Receipt = receipt.Clone(),
            Limits = "Fresh managed transport only; no application speedup, full-run size or headroom guarantee."
        }));
    }
}
