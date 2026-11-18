using BookOfEternityClient.Services.GmWorkers;
using Xunit;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityGMBridge;

namespace BookOfEternityClient.Tests;

// Diagnostic DATA only: no owner, native process, game session or publication authority.
public sealed class StopDiagnosticFullTextTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OriginalFault_PreservesCompleteUnicodeText(bool longStack)
    {
        var text = string.Concat(Enumerable.Repeat("Жизнь 🌍\n", 3000)) + "END-OF-ORIGINAL";
        var message = longStack ? "original message" : text;
        var stack = longStack ? text : "original stack";
        var original = new TextFailure(message, stack);
        var captured = Assert.IsType<HostExitFaultCapture.Fault>(
            HostExitFaultCapture.Capture(Task.FromException(original)));
        Assert.Equal(message, captured.Message);
        Assert.Equal(stack, captured.StackText);
        Assert.True(captured.CaptureComplete);
        Assert.Null(captured.CaptureFailure);

        // Same source exception at the existing coordinator DATA collector boundary.
        // In initial RED, the earlier HostExit equality fails before these assertions execute.
        var frozen = GmSessionRunCoordinator.TryFreezeStopFailureText(original);
        Assert.Same(original, frozen.Exception);
        Assert.Equal(message, frozen.Message);
        Assert.Equal(stack, frozen.StackText);
        Assert.True(frozen.CaptureComplete);
        Assert.Null(frozen.CaptureFailure);
    }

    [Fact]
    public void ReportExport_PreservesFullUnicodePayloadBeyondFormerByteLimit()
    {
        var text = string.Concat(Enumerable.Repeat("Жизнь 🌍\n", 6000)) + "END-OF-ORIGINAL";
        var report = DataReport(text);
        using var sink = new StringWriter();
        BridgeHost.WriteStopFailureReport(report, sink);
        var output = sink.ToString();
        Assert.True(Encoding.UTF8.GetByteCount(output) > 32768);
        const string prefix = "BOE_MAIN_STOP_FAILURE ";
        Assert.StartsWith(prefix, output);
        using var document = JsonDocument.Parse(output[prefix.Length..]);
        Assert.Equal(text, document.RootElement.GetProperty("CurrentFailure").GetProperty("Message").GetString());
        Assert.Equal(text, document.RootElement.GetProperty("CurrentFailure").GetProperty("StackText").GetString());
        Assert.Equal(report.Nonce, document.RootElement.GetProperty("Nonce").GetString());
        Assert.Equal(report.OriginalOwnerIdentity.RunId,
            document.RootElement.GetProperty("OriginalOwnerIdentity").GetProperty("RunId").GetString());
        Assert.True(document.RootElement.GetProperty("CaptureComplete").GetBoolean());
    }

    [Fact]
    public void ReportExport_SinkFailureDoesNotReplaceOriginalFailure()
    {
        var report = DataReport("original diagnostic");
        using var sink = new RejectingWriter();
        var original = report.CurrentException;
        Assert.Null(Record.Exception(() => BridgeHost.WriteStopFailureReport(report, sink)));
        Assert.Equal(1, sink.Attempts);
        Assert.Same(original, report.CurrentException);
        Assert.Equal("original diagnostic", original.Message);
    }

    // Constructed diagnostic DTO only: never passed to a coordinator or used as an admission grant.
    private static GmSessionRunCoordinator.StopFailureReport DataReport(string text)
    {
        var original = new TextFailure(text, text);
        var failure = new GmSessionRunCoordinator.StopFailure("data-test", "original-predicate", null,
            original.GetType().FullName!, text, text, null, true, null);
        var identity = new GmSessionRunIdentity("/diagnostic-data-only", new string('1', 32), new string('2', 32),
            1, GmSessionRunBackend.LinuxSupervisor, new string('3', 32), "diagnostic-boot");
        return new(1, new string('4', 32), identity, failure, failure, 1, true, null!, original, original);
    }

    private sealed class RejectingWriter : StringWriter
    {
        internal int Attempts;
        public override void WriteLine(string? value) { Attempts++; throw new IOException("controlled sink failure"); }
    }

    private sealed class TextFailure(string message, string stack) : Exception(message)
    {
        public override string StackTrace => stack;
    }
}
