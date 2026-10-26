using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Core;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Real-file interruption controls without a game, native process or authority.</summary>
public sealed partial class LifecyclePassiveTimingDiagnosticsTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _transportTestOutput = output;

    /// <summary>Requires an original begin on disk before its actual awaited body is released.</summary>
    [Fact]
    public Task OriginalBeginIsPersistentWhileActualBodyRemainsBlocked() => RunBlockedVisibilityAsync(gzip: false);

    /// <summary>Proves flushed compressed rows are visible during the same unreleased real await.</summary>
    [Fact]
    public Task GzipOriginalBeginIsPersistentWhileActualBodyRemainsBlocked() => RunBlockedVisibilityAsync(gzip: true);

    private async Task RunBlockedVisibilityAsync(bool gzip)
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-lifecycle-timing-control-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source.txt");
        var expected = new byte[] { 1, 3, 7, 19 };
        var diagnostics = new LifecyclePassiveTimingDiagnostics(nameof(OriginalBeginIsPersistentWhileActualBodyRemainsBlocked) + (gzip ? "-gzip" : "-plain"), output, gzipRecords: gzip);
        var recorder = new OperationTimingAccumulator();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? body = null;
        Exception? primary = null;
        OperationTimingAccumulator.Window? window = null;
        byte[]? actual = null;
        var reads = 0;
        var visible = false;
        long beforeBegin = 0, afterBegin = 0;
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllBytes(source, expected);
            diagnostics.BindFixtureRoot(root);
            Assert.Equal(Path.GetFullPath(root), diagnostics.RegisteredRoot);
            Assert.True(File.Exists(diagnostics.InitialPath));
            using (var initial = JsonDocument.Parse(File.ReadAllText(diagnostics.InitialPath)))
                Assert.Equal(root, initial.RootElement.GetProperty("ExpectedOwnedRoot").GetString());
            window = recorder.StartWindow(captureEvents: true);
            diagnostics.Arm(window);
            beforeBegin = Stopwatch.GetTimestamp();
            body = BodyAsync();
            // A source-read/setup failure must unwind cleanup, not leave the
            // entry handshake waiting for a body which already faulted.
            var first = await Task.WhenAny(entered.Task, body);
            await first;
            Assert.True(entered.Task.IsCompletedSuccessfully);
            afterBegin = Stopwatch.GetTimestamp();
            Assert.False(body.IsCompleted);
            Assert.Equal(expected, actual);
            Assert.Equal(1, reads);
            var pending = window.CaptureEventStatus();
            Assert.True(pending.Enabled);
            Assert.Equal(1, pending.Attempted);
            Assert.Equal(1, window.Capture().Rows.Single().Started);
            Assert.Equal(0, window.Capture().Rows.Single().Completed);
            var started = Stopwatch.GetTimestamp();
            do
            {
                visible = ReadRows(diagnostics.RecordsPath).Any(row =>
                    row.TryGetProperty("Operation", out var name) && name.GetString() == "LifecycleDiagnostics.BlockedBody" &&
                    row.GetProperty("State").GetString() == "begin");
                if (!visible) await Task.Delay(20);
            } while (!visible && Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(2));
            Assert.False(body.IsCompleted);
            Assert.False(File.Exists(diagnostics.ReportPath)); // live prefix is explicitly incomplete
        }
        catch (Exception error) { primary = error; }
        finally
        {
            release.TrySetResult();
            if (body is not null)
            {
                var bodyFailure = await Record.ExceptionAsync(() => body!);
                primary ??= bodyFailure;
            }
            try { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
            catch (Exception error) { primary ??= error; }
            // Preserve an original setup/body/cleanup error while still draining,
            // hashing and attempting the actual-root closing receipt.
            await diagnostics.CompleteAsync(primary);
        }

        Assert.False(Directory.Exists(root));
        Assert.Equal(1, reads);
        Assert.Equal(expected, actual);
        var rows = ReadRows(diagnostics.RecordsPath).Where(row => row.TryGetProperty("Operation", out _)).ToArray();
        Assert.Equal(2, rows.Length);
        Assert.Equal("begin", rows[0].GetProperty("State").GetString());
        Assert.Equal("end", rows[1].GetProperty("State").GetString());
        Assert.Equal(1, rows[0].GetProperty("PassiveSequence").GetInt64());
        Assert.Equal(2, rows[1].GetProperty("PassiveSequence").GetInt64());
        Assert.Equal(rows[0].GetProperty("ScopeId").GetInt64(), rows[1].GetProperty("ScopeId").GetInt64());
        Assert.InRange(rows[0].GetProperty("TimestampTicks").GetInt64(), beforeBegin, afterBegin);
        Assert.Equal(0, rows[0].GetProperty("ElapsedTicks").GetInt64());
        Assert.True(rows[1].GetProperty("TimestampTicks").GetInt64() >= afterBegin);
        Assert.All(rows, row => Assert.Equal("sealed-passive-events-v1", row.GetProperty("Transport").GetString()));
        Assert.Equal(2, window!.CaptureEventStatus().Drained);
        Assert.True(window.Capture().Complete);
        using var report = JsonDocument.Parse(File.ReadAllText(diagnostics.ReportPath));
        var receipt = report.RootElement;
        Assert.True(receipt.GetProperty("DiagnosticComplete").GetBoolean());
        Assert.True(receipt.GetProperty("PassiveTimingCompletion").GetProperty("Complete").GetBoolean());
        Assert.Equal(root, receipt.GetProperty("FixtureClosing").GetProperty("Owned").GetString());
        Assert.True(receipt.GetProperty("FixtureClosing").GetProperty("Closing").GetBoolean());
        Assert.False(receipt.GetProperty("FixtureClosing").GetProperty("Retained").GetBoolean());
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(diagnostics.RecordsPath))),
            receipt.GetProperty("Sha256").GetString());
        // Preserved causal oracle: T599's after-return baseline failed here;
        // the same original begin must now be persistent before release.
        Assert.True(visible, "Original begin was not persistent while the actual body remained blocked.");

        async Task BodyAsync()
        {
            using var operation = recorder.Measure("LifecycleDiagnostics.BlockedBody");
            actual = File.ReadAllBytes(source);
            reads++;
            entered.SetResult();
            await release.Task;
        }
    }

    // This is a live-prefix reader, not a gzip-completion validator. It
    // retries only an unfinished final fragment; every completed malformed row fails.
    private static JsonElement[] ReadRows(string path)
    {
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using Stream decoded = path.EndsWith(".gz", StringComparison.Ordinal)
            ? new GZipStream(input, CompressionMode.Decompress) : input;
        using var bytes = new MemoryStream();
        decoded.CopyTo(bytes);
        var raw = bytes.ToArray();
        var rows = new List<JsonElement>();
        var start = 0;
        for (var index = 0; index < raw.Length; index++)
        {
            if (raw[index] != '\n') continue;
            using var document = JsonDocument.Parse(raw.AsMemory(start, index - start));
            rows.Add(document.RootElement.Clone());
            start = index + 1;
        }
        return rows.ToArray();
    }
}
