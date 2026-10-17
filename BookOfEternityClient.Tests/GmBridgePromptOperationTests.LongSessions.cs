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
        host.Input.Written = bytes => host.Observe(bytes == "<submit>" ? "WORKING" : "CONTROLLED CLI\n› " + text);
        for (var index = 1; index <= 272; index++)
        {
            host.Observe("CONTROLLED CLI\n› ");
            // Same real fixture/pipe/view sequence as the accepted two-prompt control.
            var response = await host.Rpc(host.Request("long-session-" + index, text));
            Assert.True(Disposition(response) == "submission-observed", $"Completed original operations before boundary: {index - 1}; current reply: {response}");
        }
        var beforeDuplicate = host.Input.Bytes.ToArray();
        var duplicate = await host.Rpc(host.Request("long-session-1", text));
        Assert.Contains(Disposition(duplicate), new[] { "submission-observed", "unknown-outcome", "not-written" });
        Assert.Equal(beforeDuplicate, host.Input.Bytes);
        var tasks = Assert.IsAssignableFrom<IEnumerable>(host.Binding.GetType().GetField("PromptTasks", BindingFlags.Instance | BindingFlags.Public)!.GetValue(host.Binding));
        Assert.True(tasks.Cast<Task>().Count() <= 2, "Settled task references must retire; active/uncertain ownership still remains joined.");
        var operations = Assert.IsAssignableFrom<IDictionary>(host.Get("_promptOperations"));
        Assert.True(operations.Count <= 256, "A bounded completed receipt window must not become a lifetime full-payload set.");
    }
}
