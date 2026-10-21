using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks durable component diagnostics without constructing a game fixture or issuer.
/// </summary>
public sealed class JointBundleDiagnosticOutputTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Selects the actual case receiver for compact diagnostic references.
    /// </summary>
    /// <param name="output">
    /// The current xUnit output helper.
    /// </param>
    public JointBundleDiagnosticOutputTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Requires live-readable complete messages and timing rows beyond 1024 records, then exact closing accounting.
    /// </summary>
    [Fact]
    public void RecordsAreReadableBeforeCloseWithoutDiagnosticQuota()
    {
        using var output = new JointBundleDiagnosticOutput(
            nameof(RecordsAreReadableBeforeCloseWithoutDiagnosticQuota), _output);
        Assert.True(File.Exists(output.InitialPath));
        Assert.False(Directory.Exists(output.ExpectedOwnedRoot));
        const string message = "Unicode: рана 日本語 🩹; quote: \"; slash: \\; newline:\nsecond line\tend";
        for (var index = 0; index < 1100; index++)
        {
            output.WriteLine("{0}:{1}", index, message);
            output.Timings.Observe("control." + index, "end", TimeSpan.FromTicks(index));
        }
        var live = File.ReadAllLines(output.RecordsPath);
        Assert.Equal(2200, live.Length);
        for (var index = 0; index < 1100; index++)
        {
            using var row = JsonDocument.Parse(live[index * 2]);
            Assert.Equal(index + ":" + message, row.RootElement.GetProperty("Message").GetString());
            using var timing = JsonDocument.Parse(live[index * 2 + 1]);
            Assert.Equal("control." + index, timing.RootElement.GetProperty("Operation").GetString());
        }
        Assert.Equal(1100, output.Timings.Attempted);
        Assert.Equal(1100, output.Timings.Stored);
        Assert.True(output.Timings.Complete);
        output.Finish();
        using var report = JsonDocument.Parse(File.ReadAllText(output.ReportPath));
        var root = report.RootElement;
        Assert.True(root.GetProperty("DiagnosticComplete").GetBoolean());
        Assert.True(root.GetProperty("WriterClosed").GetBoolean());
        Assert.Equal(1100, root.GetProperty("OutputAttempted").GetInt64());
        Assert.Equal(1100, root.GetProperty("OutputStored").GetInt64());
        Assert.Equal(1100, root.GetProperty("TimingAttempted").GetInt64());
        Assert.Equal(1100, root.GetProperty("TimingStored").GetInt64());
        Assert.Empty(root.GetProperty("Failures").EnumerateArray());
        Assert.Equal(new FileInfo(output.RecordsPath).Length, root.GetProperty("StoredBytes").GetInt64());
        using var bytes = File.OpenRead(output.RecordsPath);
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)),
            root.GetProperty("Sha256").GetString());
        output.Finish();
    }

    /// <summary>
    /// Requires real disposed-writer errors to survive production observer isolation and refuse completion repeatedly.
    /// </summary>
    [Fact]
    public void ObserverWriteFailurePreventsCompleteReport()
    {
        var output = new JointBundleDiagnosticOutput(nameof(ObserverWriteFailurePreventsCompleteReport),
            _output, disposeWriterForControl: true);
        // The constructor creates an empty physical namespace; keep it with these retained diagnostics.
        var files = new FileSystemManager(Path.Combine(output.DiagnosticDirectory, "observer-owner"),
            NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks { OperationTimingObserver = output.Timings.Observe });
        var bodyExecuted = false;
        using (files.MeasureOperation("control.disposed-writer")) bodyExecuted = true;
        Assert.True(bodyExecuted);
        Assert.Equal(2, output.Timings.Attempted);
        Assert.Equal(0, output.Timings.Stored);
        Assert.False(output.Timings.Complete);
        Assert.Equal(2, output.Timings.GetFailures().Length);
        Assert.All(output.Timings.GetFailures(), row => Assert.IsType<ObjectDisposedException>(row.Error));
        Assert.Throws<AggregateException>(() => output.Finish());
        using var report = JsonDocument.Parse(File.ReadAllText(output.ReportPath));
        Assert.False(report.RootElement.GetProperty("DiagnosticComplete").GetBoolean());
        Assert.Equal(2, report.RootElement.GetProperty("TimingAttempted").GetInt64());
        Assert.Equal(0, report.RootElement.GetProperty("TimingStored").GetInt64());
        Assert.Contains(report.RootElement.GetProperty("Failures").EnumerateArray(),
            row => row.GetProperty("Error").GetString()!.Contains(nameof(ObjectDisposedException), StringComparison.Ordinal));
        Assert.Throws<AggregateException>(() => output.Finish());
        Assert.Throws<AggregateException>(() => output.Dispose());
    }
}
