using System.Globalization;
using System.IO;
using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    /// <summary>
    /// Verifies every named begin and end remains observable beyond the former diagnostic record limit.
    /// </summary>
    [Fact]
    public void JointOperationTimings_PreservesAllBoundariesBeyondFormerLimit()
    {
        const int operationCount = 1100;
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        var timings = new JointOperationTimings(output);
        for (var operation = 0; operation < operationCount; operation++)
        {
            var name = "operation-" + operation.ToString(CultureInfo.InvariantCulture);
            timings.Observe(name, "begin", TimeSpan.Zero);
            timings.Observe(name, "end", TimeSpan.FromMilliseconds(operation + 1));
        }

        var lines = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(operationCount * 2, lines.Length);
        double previousTotalMilliseconds = 0;
        for (var index = 0; index < lines.Length; index++)
        {
            using var record = JsonDocument.Parse(lines[index]);
            var root = record.RootElement;
            Assert.Equal("joint_operation_timing", root.GetProperty("Boundary").GetString());
            Assert.Equal("operation-" + (index / 2).ToString(CultureInfo.InvariantCulture), root.GetProperty("Operation").GetString());
            Assert.Equal(index % 2 == 0 ? "begin" : "end", root.GetProperty("State").GetString());
            Assert.Equal(index + 1, root.GetProperty("Sequence").GetInt64());
            Assert.Equal(Environment.ProcessId, root.GetProperty("PID").GetInt32());
            Assert.Equal(index % 2 == 0 ? 0d : index / 2 + 1d, root.GetProperty("Milliseconds").GetDouble());
            var totalMilliseconds = root.GetProperty("TotalElapsedMilliseconds").GetDouble();
            Assert.True(totalMilliseconds >= previousTotalMilliseconds);
            previousTotalMilliseconds = totalMilliseconds;
            Assert.False(root.TryGetProperty("Limit", out _));
        }

        using var finalRecord = JsonDocument.Parse(lines[^1]);
        Assert.Equal("operation-1099", finalRecord.RootElement.GetProperty("Operation").GetString());
        Assert.Equal("end", finalRecord.RootElement.GetProperty("State").GetString());
    }
}
