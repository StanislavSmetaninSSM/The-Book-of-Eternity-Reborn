using Xunit;
using BookOfEternityClient.Core;
using BookOfEternityClient.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
namespace BookOfEternityClient.Tests;
public sealed class GmMainLinuxLauncherTests
{
    [Fact]
    public Task OrdinaryLauncher_ShippedLayoutWithoutSourceRunsConfiguredCli()=>ProductionMainLinuxFixture.RunAsync("launcher");
    [Fact]
    public Task ActualDaemonStartup_PortableOwnedBridgeWithoutWindowsDesktop()=>ProductionMainLinuxFixture.RunAsync("daemon");
    [Fact]
    public Task ShippedPublicationContainsRealLauncherAndParticipatingHelper()=>ProductionMainLinuxFixture.RunAsync("package-layout");
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ColdMalformedWorkerNamespace_RefusesBootstrapBeforeEffects(bool link)
    {
        var root=Path.Combine(Path.GetTempPath(),"m1-cold-inventory-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var path=Path.Combine(root,".boe_runtime/worker-runs-v1");Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if(link)File.CreateSymbolicLink(path,"missing-owned-inventory");else File.WriteAllBytes(path,[0xfe,0]);
        try {
            var files=new FileSystemManager(root,NullLogger<FileSystemManager>.Instance);
            var error=await Record.ExceptionAsync(()=>new StateManager(files,new GameSettings(),NullLogger<StateManager>.Instance).BootstrapLocalStorageAsync());
            Assert.NotNull(error);Assert.False(File.Exists(files.ResolvePath("config.json")));Assert.False(File.Exists(files.SessionGenerationPath));
            if(link)Assert.Equal("missing-owned-inventory",new FileInfo(path).LinkTarget);else Assert.Equal(new byte[]{0xfe,0},File.ReadAllBytes(path));
        }finally {Directory.Delete(root,true);}
    }
    [Fact]
    public async Task ActualBootstrap_ConfigAndGenerationUseOneAtomicPublication()
    {
        var root=Path.Combine(Path.GetTempPath(),"m1-bootstrap-atomic-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try {
            var intents=0;var members=new List<int>();
            var files=new FileSystemManager(root,NullLogger<FileSystemManager>.Instance,PhysicalLoadTransactionOperations.Instance,new FileSystemManagerHooks{LocalPublicationObserver=(phase,index)=>{if(phase==TrustedLocalPublicationPhase.IntentPublished)intents++;if(phase==TrustedLocalPublicationPhase.MemberPublished)members.Add(index);}});
            await new StateManager(files,new GameSettings(),NullLogger<StateManager>.Instance).BootstrapLocalStorageAsync();
            Assert.Equal(1,intents);Assert.Equal(new[]{0,1},members);
        }finally {Directory.Delete(root,true);}
    }
}
