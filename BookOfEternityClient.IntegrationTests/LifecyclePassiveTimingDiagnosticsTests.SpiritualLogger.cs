using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class LifecyclePassiveTimingDiagnosticsTests
{
    /// <summary>Proves full existing Engine logging and an original macro begin survive an unreleased body.</summary>
    [Fact]
    public async Task SpiritualLoggerAndMacroBeginArePersistentBeforeTheBodyReturns()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-lifecycle-timing-control-" + Guid.NewGuid().ToString("N"));
        var capture = new LifecyclePassiveTimingDiagnostics(nameof(SpiritualLoggerAndMacroBeginArePersistentBeforeTheBodyReturns),
            _transportTestOutput, gzipRecords: true);
        var timings = new OperationTimingAccumulator();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failure = new InvalidOperationException("original full phase exception");
        const string message = "Original Engine phase: целитель; full message\nsecond line";
        var expected = message + "\n" + failure;
        Task? body = null;
        Exception? primary = null;
        var visible = false;
        var formatterCalls = 0;
        try
        {
            Directory.CreateDirectory(root);
            capture.BindFixtureRoot(root);
            var window = timings.StartWindow(captureEvents: true);
            capture.Arm(window);
            var logger = new GameEngineTurnLifecycleTests.SpiritualLifecycleTestLogger(capture);
            body = BodyAsync();
            var first = await Task.WhenAny(entered.Task, body);
            await first;
            Assert.True(entered.Task.IsCompletedSuccessfully);
            await WaitUntilAsync(() =>
            {
                var rows = ReadRows(capture.RecordsPath);
                visible = rows.Any(r => r.TryGetProperty("Operation", out var name) && name.GetString() == "FactionService.WiringBlockedBody" &&
                    r.GetProperty("State").GetString() == "begin") &&
                    rows.Any(r => r.TryGetProperty("Message", out var text) && text.GetString() == expected);
                return visible;
            });
            Assert.False(body.IsCompleted); Assert.True(visible);
            Assert.Equal(1, formatterCalls);
            Assert.Equal("Engine diagnostics:\n" + expected, logger.Describe());
            Assert.False(File.Exists(capture.ReportPath));

            async Task BodyAsync()
            {
                using var operation = timings.Measure("FactionService.WiringBlockedBody");
                logger.Log(LogLevel.Information, new EventId(1), message, failure, (state, _) => { formatterCalls++; return state; });
                logger.Log(LogLevel.Debug, new EventId(2), "disabled", null, (state, _) => { formatterCalls++; return state; });
                entered.SetResult();
                await release.Task;
            }
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
            await capture.CompleteAsync(primary);
        }
        AssertComplete(capture);
        using var report = JsonDocument.Parse(File.ReadAllText(capture.ReportPath));
        Assert.True(report.RootElement.GetProperty("GzipFooterClosed").GetBoolean());
        Assert.Equal(2, ReadRows(capture.RecordsPath).Count(r => r.TryGetProperty("Operation", out _)));
    }
}
