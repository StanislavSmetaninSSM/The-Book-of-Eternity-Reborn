using System.Security.Cryptography;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class AuxiliaryLauncherRootBindingTests
{
    [Fact]
    public async Task ExistingDirectoryAction_CannotRedirectExplicitSessionRoot()
    {
        await using var f=await AuxiliaryPackageFixture.Create();
        var external=Path.Combine(f.Root,"external root Ж");
        var other=new FileSystemManager(external,NullLogger<FileSystemManager>.Instance);other.EnsureDirectoryStructure();
        await new StateManager(other,new GameSettings {GmBridgeAutoStart=false},NullLogger<StateManager>.Instance).BootstrapLocalStorageAsync();
        var before=Snapshot(external);
        var result=await f.Run("existing-directory-action",external);
        Assert.True(result.Exit==0,result.Output+result.Error);
        Assert.True(File.Exists(f.Files.ResolvePath(LiveTurnPreparationService.TurnRequestPath)),"Causal root failure: --action redirected preparation from original session root.");
        Assert.Equal(before,Snapshot(external));
        var request=System.Text.Json.JsonDocument.Parse(File.ReadAllText(f.Files.ResolvePath(LiveTurnPreparationService.TurnRequestPath))).RootElement;
        Assert.Equal(external,request.GetProperty("playerAction").GetString());
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrNonexistentFirstRoot_ActualEarlyModeRefusesBeforeWrites(bool nonexistent)
    {
        await using var f=await AuxiliaryPackageFixture.Create();
        var before=Snapshot(f.Files.GameSessionPath);
        var arguments=nonexistent?new[]{Path.Combine(f.Root,"missing root"),"--prepare-live-turn","--action","test"}:new[]{"--prepare-live-turn","--action","test"};
        var result=await f.RunClient("invalid-first-root",arguments);
        Assert.Equal(2,result.Exit);Assert.Contains("existing explicit first root",result.Error);
        Assert.Equal(before,Snapshot(f.Files.GameSessionPath));Assert.False(Directory.Exists(Path.Combine(f.Runtime,"game_session")));
    }
    internal static string Snapshot(string path)=>string.Join("\n",Directory.GetFiles(path,"*",SearchOption.AllDirectories).OrderBy(p=>p,StringComparer.Ordinal).Select(p=>Path.GetRelativePath(path,p)+":"+Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)))));
}
