using System.Collections;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GmBridgePromptOperationTests
{
    [Fact]
    public async Task SettledPromptRetention_Crosses256WithoutRepasteOrGrowingLiveTasks()
    {
        await using var host = new PromptHostFixture();
        var text = "Стабильный текущий запрос Ж🙂";
        var completed = 0;
        JsonElement? last = null;
        var diagnostics = Path.Combine(TestRepoPaths.RepoRoot, "TestResults/relay1559", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(diagnostics);
        object Request(int sequence) => new
        {
            command = "dispatchPrompt", text, appendEnter = true,
            operationId = host.BindingId + ":" + sequence, operationSequence = sequence,
            operationKind = "turn", operationRevision = "revision-1", inputBindingId = host.BindingId
        };
        host.Input.Written = bytes => host.Observe(bytes == "<submit>" ? "WORKING" : "CONTROLLED CLI\n› " + text);
        try
        {
            for (var index = 1; index <= 272; index++)
            {
                host.Observe("CONTROLLED CLI\n› ");
                // Added current-binding sequence metadata is ignored by baseline BridgeRequest.
                // It therefore reaches the actual 256 cap, and is usable by the proposed current protocol.
                last = await host.Rpc(Request(index));
                Assert.True(Disposition(last.Value) == "submission-observed", $"Completed original operations before boundary: {completed}; current reply: {last}");
                completed++;
            }
            var beforeDuplicate = host.Input.Bytes.ToArray();
            var duplicate = await host.Rpc(Request(1));
            Assert.Contains(Disposition(duplicate), new[] { "submission-observed", "unknown-outcome", "not-written" });
            Assert.Equal(beforeDuplicate, host.Input.Bytes);
            var tasks = Assert.IsAssignableFrom<IEnumerable>(host.Binding.GetType().GetField("PromptTasks", BindingFlags.Instance | BindingFlags.Public)!.GetValue(host.Binding));
            Assert.True(tasks.Cast<Task>().Count() <= 2, "Settled task references must retire; active/uncertain ownership still remains joined.");
            var operations = Assert.IsAssignableFrom<IDictionary>(host.Get("_promptOperations"));
            Assert.True(operations.Count <= 256, "A bounded completed receipt window must not become a lifetime full-payload set.");
            long retainedBodyBytes = 0;
            foreach (var operation in operations.Values)
            {
                var snapshot = operation!.GetType().GetField("Snapshot")?.GetValue(operation);
                var body = snapshot?.GetType().GetProperty("Text")?.GetValue(snapshot) as string;
                retainedBodyBytes += body is null ? 0 : Encoding.UTF8.GetByteCount(body);
            }
            Assert.Equal(0, retainedBodyBytes); // All calls above have positively settled; compact receipts carry hashes, not payload.
        }
        finally
        {
            var operations = Assert.IsAssignableFrom<IDictionary>(host.Get("_promptOperations"));
            var tasks = Assert.IsAssignableFrom<IEnumerable>(host.Binding.GetType().GetField("PromptTasks", BindingFlags.Instance | BindingFlags.Public)!.GetValue(host.Binding));
            await File.WriteAllTextAsync(Path.Combine(diagnostics, "retention.json"), JsonSerializer.Serialize(new
            {
                CompletedOriginalOperations = completed, ActualLastReply = last, RetainedOperationCount = operations.Count,
                RetainedTaskCount = tasks.Cast<Task>().Count(), ActualInputBytes = host.Input.Bytes.Length,
                Scope = "REAL_BRIDGE_PIPE_CONTROLLED_VIEW_NOT_NATIVE_TERMINAL_OR_CLIENT_ACCEPTANCE"
            }));
        }
    }
}
