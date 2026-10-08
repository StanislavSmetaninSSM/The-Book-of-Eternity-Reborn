using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
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
    [Theory]
    [InlineData("bound-operation")]
    [InlineData("main-admission")]
    public async Task LoadIpcRefusesOriginalFilesystemScopeBeforeSideEffects(string scope)
    {
        var root=Path.Combine(Path.GetTempPath(),"boe-load-ipc-"+Guid.NewGuid().ToString("N"));
        try {
            var files=new FileSystemManager(root,NullLogger<FileSystemManager>.Instance);PortableSaveFixture.Seed(files);
            var bytes=File.ReadAllBytes(files.SessionGenerationPath);
            await using var operation=GmLoadSessionOperation.Reserve(files,Guid.NewGuid().ToString("N"),"inert-selection",null);
            async Task Refuse()=>await Assert.ThrowsAsync<InvalidOperationException>(()=>operation.StopAsync());
            if(scope=="bound-operation") {
                var generation=JsonNode.Parse(bytes)!["generationId"]!.GetValue<string>();
                await SessionOperationContext.RunBoundAsync(files,generation,Refuse);
            } else {using var admission=files.BeginMainAdmission();await admission.AcquireAsync(quiescentOnly:true);await Refuse();}
            Assert.Equal(bytes,File.ReadAllBytes(files.SessionGenerationPath));Assert.Null(GmSessionRunPersistence.Read(root));
        } finally {if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
