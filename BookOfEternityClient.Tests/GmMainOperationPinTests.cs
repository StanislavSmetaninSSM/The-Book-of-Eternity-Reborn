using System.Text;
using System.Text.Json;
using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmMainOperationPinTests
{
    [Fact]
    public async Task ActualPipe_LargeUnicodeT042IdentitySurvivesStatusAndCancel()
    {
        await using var host=new GmBridgePromptOperationTests.PromptHostFixture();
        host.Screen="CONTROLLED CLI\n› retained manual draft";
        var text=string.Concat(Enumerable.Repeat("Ж🌌\n\"escaped\"\r\n",6000));
        Assert.True(Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(host.Request("large-f2",text)))>65536);
        var first=await host.Rpc(host.Request("large-f2",text)).WaitAsync(TimeSpan.FromSeconds(4));
        Assert.Equal("not-written",first.GetProperty("promptDelivery").GetProperty("disposition").GetString());
        var hash=first.GetProperty("promptDelivery").GetProperty("contentHash").GetString();
        foreach(var command in new[]{"promptStatus","cancelPrompt"}) {
            var reply=await host.Rpc(new{command,operationId="large-f2",operationKind="turn",operationRevision="revision-1",inputBindingId=host.BindingId,text,appendEnter=true}).WaitAsync(TimeSpan.FromSeconds(4));
            Assert.Equal(hash,reply.GetProperty("promptDelivery").GetProperty("contentHash").GetString());
        }
        Assert.Empty(host.Input.Bytes);
    }
}
