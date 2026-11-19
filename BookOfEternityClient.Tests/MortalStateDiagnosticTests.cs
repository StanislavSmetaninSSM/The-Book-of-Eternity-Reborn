using System.Text.Json;
using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class MortalStateDiagnosticTests
{
    [Theory]
    [InlineData("existing")]
    [InlineData("traversal")]
    [InlineData("hash")]
    [InlineData("link")]
    [InlineData("probe-traversal")]
    public void UnsafeManifestRefusesBeforeCopy(string mode)
    {
        var root=Path.Combine(TestDiagnosticPaths.Root,"mortal-state-diagnostic",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source=Path.Combine(root,"source");Directory.CreateDirectory(source);
            var data=Path.Combine(source,"data");File.WriteAllText(data,"{}");var hash=MortalStateDiagnostic.Hash(data);
            var pins=Enumerable.Range(0,106).ToDictionary(i=>"game_state/data"+i+".json",_=>hash);
            var probe=Path.Combine(source,"probe.json");File.WriteAllText(probe,JsonSerializer.Serialize(new {Success=true,AcceptedGameTurns=1,FinalWorldPins=pins,FinalConfigSHA256=hash}));
            var entries=pins.Keys.Append("config.json").Select(n=>new MortalStateDiagnostic.Entry(n,data,hash)).ToArray();
            var dest=Path.Combine(root,"destination");
            if(mode=="existing")Directory.CreateDirectory(dest);
            if(mode=="traversal")entries[0]=entries[0] with {Path="../escape"};
            if(mode=="hash")entries[0]=entries[0] with {SHA256=new string('0',64)};
            if(mode=="link") {var link=Path.Combine(source,"link");File.CreateSymbolicLink(link,data);entries[0]=entries[0] with {Source=link};}
            var probePath=mode=="probe-traversal" ? Path.Combine(source,"..","source","probe.json") : probe;
            var manifest=Path.Combine(root,"manifest.json");File.WriteAllText(manifest,JsonSerializer.Serialize(new MortalStateDiagnostic.Manifest(source,probePath,MortalStateDiagnostic.Hash(probe),entries)));
            Assert.Throws<InvalidDataException>(()=>MortalStateDiagnostic.Prepare(manifest,dest));
            Assert.False(Directory.Exists(Path.Combine(dest,"game_session")));
            Assert.False(File.Exists(dest+".reservation"));
            Assert.Equal(hash,MortalStateDiagnostic.Hash(data));
        }
        finally {Directory.Delete(root,true);}
    }
    [Fact]
    public void ExactManifestCopiesOnlyDeclaredDataWithoutAuthority()
    {
        var root=Path.Combine(TestDiagnosticPaths.Root,"mortal-state-diagnostic",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var source=Path.Combine(root,"source");Directory.CreateDirectory(source);var data=Path.Combine(source,"data");File.WriteAllText(data,"{}");var hash=MortalStateDiagnostic.Hash(data);
            var pins=Enumerable.Range(0,106).ToDictionary(i=>"game_state/data"+i+".json",_=>hash);var probe=Path.Combine(source,"probe.json");File.WriteAllText(probe,JsonSerializer.Serialize(new {Success=true,AcceptedGameTurns=1,FinalWorldPins=pins,FinalConfigSHA256=hash}));
            var entries=pins.Keys.Append("config.json").Select(n=>new MortalStateDiagnostic.Entry(n,data,hash)).ToArray();var manifest=Path.Combine(root,"manifest.json");File.WriteAllText(manifest,JsonSerializer.Serialize(new MortalStateDiagnostic.Manifest(source,probe,MortalStateDiagnostic.Hash(probe),entries)));var dest=Path.Combine(root,"destination");
            MortalStateDiagnostic.Prepare(manifest,dest);
            Assert.Equal(107,MortalStateDiagnostic.Inventory(dest).Count);Assert.False(Directory.Exists(Path.Combine(dest,".boe_runtime")));
            Assert.All(MortalStateDiagnostic.Inventory(dest).Values,x=>Assert.Equal(hash,x));
        }
        finally {Directory.Delete(root,true);}
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunRefusalDoesNotInventoryForeignDestination(bool linked)
    {
        var root=Path.Combine(TestDiagnosticPaths.Root,"mortal-state-diagnostic",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var source=Path.Combine(root,"source");Directory.CreateDirectory(source);
            var dest=Path.Combine(root,"foreign");Directory.CreateDirectory(dest);
            var secret=Path.Combine(dest,"unowned.txt");File.WriteAllText(secret,"not diagnostic DATA");
            if(linked)File.CreateSymbolicLink(Path.Combine(dest,"broken"),Path.Combine(root,"nonexistent"));
            var manifest=Path.Combine(root,"manifest.json");File.WriteAllText(manifest,JsonSerializer.Serialize(new MortalStateDiagnostic.Manifest(source,Path.Combine(source,"missing-probe"),new string('0',64),[])));
            var report=Path.Combine(root,"result.json");
            Assert.Equal(1,await MortalStateDiagnostic.RunAsync(manifest,dest,report));
            using var result=JsonDocument.Parse(File.ReadAllBytes(report));
            Assert.Equal(JsonValueKind.Null,result.RootElement.GetProperty("AfterValidation").ValueKind);
            Assert.Contains("destination must not exist",result.RootElement.GetProperty("Exception").GetString());
            Assert.Equal("not diagnostic DATA",File.ReadAllText(secret));
        }
        finally {Directory.Delete(root,true);}
    }
    [Fact]
    public async Task ResultCannotEqualDestination()
    {
        var root=Path.Combine(TestDiagnosticPaths.Root,"mortal-state-diagnostic",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var dest=Path.Combine(root,"new");
            await Assert.ThrowsAsync<InvalidDataException>(()=>MortalStateDiagnostic.RunAsync(Path.Combine(root,"missing-manifest"),dest,dest));
            Assert.False(Directory.Exists(dest));Assert.False(File.Exists(dest));
        }
        finally {Directory.Delete(root,true);}
    }
    [Theory]
    [InlineData("valid")]
    [InlineData("config-hash")]
    [InlineData("probe-hash")]
    [InlineData("probe-join")]
    [InlineData("probe-path")]
    public void FailedPhaseConfigHasExplicitSeparateProvenance(string mode)
    {
        var root=Path.Combine(TestDiagnosticPaths.Root,"mortal-state-diagnostic",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var source=Path.Combine(root,"source");Directory.CreateDirectory(source);
            var data=Path.Combine(source,"data");File.WriteAllText(data,"{}");var hash=MortalStateDiagnostic.Hash(data);
            var config=Path.Combine(source,"failed-config.json");File.WriteAllText(config,"{\"phase\":2}");var configHash=MortalStateDiagnostic.Hash(config);
            var pins=Enumerable.Range(0,106).ToDictionary(i=>"game_state/data"+i+".json",_=>hash);
            var probe=Path.Combine(source,"accepted.json");File.WriteAllText(probe,JsonSerializer.Serialize(new {Success=true,AcceptedGameTurns=1,FinalWorldPins=pins,FinalConfigSHA256=hash}));
            var failed=Path.Combine(source,"failed.json");File.WriteAllText(failed,JsonSerializer.Serialize(new {Success=false,AcceptedGameTurns=0,FinalConfigSHA256=mode=="probe-join"?hash:configHash}));
            var entries=pins.Keys.Select(n=>new MortalStateDiagnostic.Entry(n,data,hash)).Append(new("config.json",config,configHash)).ToArray();
            var witness=new MortalStateDiagnostic.ConfigWitness(config,mode=="config-hash"?hash:configHash,mode=="probe-path"?Path.Combine(source,"..","source","failed.json"):failed,mode=="probe-hash"?hash:MortalStateDiagnostic.Hash(failed));
            var manifest=Path.Combine(root,"manifest.json");File.WriteAllText(manifest,JsonSerializer.Serialize(new MortalStateDiagnostic.Manifest(source,probe,MortalStateDiagnostic.Hash(probe),entries,witness)));
            var dest=Path.Combine(root,"destination");
            if(mode=="valid")
            {
                var admitted=MortalStateDiagnostic.Prepare(manifest,dest);
                Assert.NotNull(admitted.FailedPhaseConfig);Assert.Equal(configHash,MortalStateDiagnostic.Hash(Path.Combine(dest,"game_session/config.json")));
                Assert.Equal(107,MortalStateDiagnostic.Inventory(dest).Count);
            }
            else
            {
                Assert.Throws<InvalidDataException>(()=>MortalStateDiagnostic.Prepare(manifest,dest));
                Assert.False(Directory.Exists(dest));Assert.False(File.Exists(dest+".reservation"));
            }
            Assert.Equal(configHash,MortalStateDiagnostic.Hash(config));
        }
        finally {Directory.Delete(root,true);}
    }
}
