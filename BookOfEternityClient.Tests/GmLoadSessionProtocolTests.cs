using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services.GmRuntime;
using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmLoadSessionProtocolTests
{
    [Theory]
    [InlineData("ok")]
    [InlineData("operationId")]
    [InlineData("state")]
    public void OriginalReplyRequiresExplicitReceiptFields(string missing)
    {
        var json=new JsonObject{["ok"]=true,["operationId"]=Guid.NewGuid().ToString("N"),["state"]="NoActiveSession"};json.Remove(missing);
        Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<GmLoadSessionReply>(json.ToJsonString(),MainOperationReader.Json));
    }
}
