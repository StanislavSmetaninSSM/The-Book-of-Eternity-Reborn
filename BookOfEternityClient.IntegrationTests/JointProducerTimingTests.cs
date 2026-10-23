using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks producer observation through the actual fixture manager without preparing a game world.
/// </summary>
public sealed class JointProducerTimingTests
{
    /// <summary>
    /// Preserves ordinary file data while enabling all nested timings only for an observed fixture.
    /// </summary>
    /// <param name="asynchronous">
    /// Whether the control uses the original asynchronous rather than synchronous text reader.
    /// </param>
    /// <param name="observed">
    /// Whether the fixture receives the existing uncapped timing collector.
    /// </param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task AdoptedProducerManager_PreservesDataAndNestedObservation(bool asynchronous, bool observed)
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-producer-timing-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        const string content = "Исходные данные 🕰";
        using var output = new StringWriter();
        var timings = new GameEngineTurnLifecycleTests.JointOperationTimings(output);
        using (var fixture = observed
            ? new GameEngineTurnLifecycleTests(null, root, timings.Observe)
            : new GameEngineTurnLifecycleTests(null, root))
        {
            var files = fixture.DiagnosticFiles;
            var subject = files.ResolvePath("subject.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(subject)!);
            await File.WriteAllTextAsync(subject, content);
            string? actual;
            using (files.MeasureOperation("Producer.Control.Read"))
                actual = asynchronous ? await files.ReadFileAsync("subject.txt") : files.ReadFileSync("subject.txt");
            Assert.Equal(content, actual);
            Assert.Equal(content, await File.ReadAllTextAsync(subject));
            var rows = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => JsonDocument.Parse(line)).ToArray();
            try
            {
                if (!observed)
                {
                    Assert.Empty(rows);
                    return;
                }
                Assert.NotEmpty(rows);
                Assert.Contains(rows, row => row.RootElement.GetProperty("Operation").GetString() ==
                    (asynchronous ? "OrdinaryRead.Async.Total" : "OrdinaryRead.Sync.Total"));
                Assert.Contains(rows, row => row.RootElement.GetProperty("Operation").GetString() ==
                    "TrustedScope.Probe.GetAttributes");
                var stack = new Stack<string>();
                long sequence = 0;
                foreach (var row in rows)
                {
                    var value = row.RootElement;
                    Assert.Equal(++sequence, value.GetProperty("Sequence").GetInt64());
                    Assert.Equal(Environment.ProcessId, value.GetProperty("PID").GetInt32());
                    var name = value.GetProperty("Operation").GetString()!;
                    if (value.GetProperty("State").GetString() == "begin")
                    {
                        if (name == (asynchronous ? "OrdinaryRead.Async.Total" : "OrdinaryRead.Sync.Total"))
                            Assert.Contains("Producer.Control.Read", stack);
                        stack.Push(name);
                    }
                    else
                    {
                        Assert.NotEmpty(stack);
                        Assert.Equal(stack.Pop(), name);
                        Assert.True(value.GetProperty("Milliseconds").GetDouble() >= 0);
                    }
                }
                Assert.Empty(stack);
                Assert.Contains(rows, row => row.RootElement.GetProperty("Operation").GetString() ==
                    "Producer.Control.Read" && row.RootElement.GetProperty("State").GetString() == "begin");
                Assert.Equal("Producer.Control.Read", rows[^1].RootElement.GetProperty("Operation").GetString());
            }
            finally { foreach (var row in rows) row.Dispose(); }
        }
        Assert.False(Directory.Exists(root));
    }
}
