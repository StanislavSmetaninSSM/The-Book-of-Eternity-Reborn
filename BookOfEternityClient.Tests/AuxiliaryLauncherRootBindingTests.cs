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
        var before=Snapshot(other.GameSessionPath);
        var result=await f.Run("existing-directory-action",external);
        Assert.True(result.Exit==0,result.Output+result.Error);
        Assert.True(File.Exists(f.Files.ResolvePath(LiveTurnPreparationService.TurnRequestPath)),"Causal root failure: --action redirected preparation from original session root.");
        Assert.Equal(before,Snapshot(other.GameSessionPath));
    }
    internal static string Snapshot(string path)=>string.Join("\n",Directory.GetFiles(path,"*",SearchOption.AllDirectories).OrderBy(p=>p,StringComparer.Ordinal).Select(p=>Path.GetRelativePath(path,p)+":"+Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)))));
}
