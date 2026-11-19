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
            var manifest=Path.Combine(root,"manifest.json");File.WriteAllText(manifest,JsonSerializer.Serialize(new MortalStateDiagnostic.Manifest(source,probe,MortalStateDiagnostic.Hash(probe),entries)));
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
}
