using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmMainProductionAdmissionTests
{
    [Fact]
    public Task OrdinaryBridge_ConfiguredCliUsesDurableOriginalProductionRoute()=>ProductionMainLinuxFixture.RunAsync("bridge");
    [Theory]
    [InlineData("refuse-auto")]
    [InlineData("refuse-systemd")]
    [InlineData("refuse-command")]
    [InlineData("refuse-cwd")]
    [InlineData("refuse-package")]
    [InlineData("refuse-worker")]
    [InlineData("refuse-storage")]
    public Task AdmissionRefusal_RealOrdinaryEntrypointBeforeCreation(string mode)=>ProductionMainLinuxFixture.RunAsync(mode);
    [Fact]
    public Task OriginalProduction_PipeDraftTakeoverCancelScopedStop()=>ProductionMainLinuxFixture.RunAsync("production-main-controls");
    [Fact]
    public Task RealConsoleHealthAndInertDaemonConsumers_BorrowOriginalLiveProductionPins()=>ProductionMainLinuxFixture.RunAsync("production-main-consumers");
    [Fact]
    public Task OriginalProduction_UncertainRetainsOwnerAndWorkerInventory()=>ProductionMainLinuxFixture.RunAsync("production-main-uncertain");
    [Fact]
    public async Task RejectedMainAttempt_ReleasesOnlyItsBorrowedOriginalInventoryClient()
    {
        var root=Path.Combine(Path.GetTempPath(),"m1-refused-original-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var files=new BookOfEternityClient.Core.FileSystemManager(root,Microsoft.Extensions.Logging.Abstractions.NullLogger<BookOfEternityClient.Core.FileSystemManager>.Instance);
        files.EnsureDirectoryStructure();
        var context=BookOfEternityClient.Services.GmWorkers.GmWorkerRootContext.Attach(files,true,null);
        var ledger=(BookOfEternityClient.Services.GmWorkers.WorkerRunLedgerCoordinator)context.GetType().GetField("_coordinator",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(context)!;
        try {
            var prepared=await ledger.PrepareAsync(new(Guid.NewGuid().ToString("N"),"inert_worker","inert_task",new string('a',64),BookOfEternityClient.Services.GmWorkers.WorkerRunBackend.LinuxNativeLineage,BookOfEternityClient.Services.GmWorkers.WorkerRunScope.OrdinarySamePidNamespace,files.GameSessionPath),ledger.Sequence);
            Assert.Equal(BookOfEternityClient.Services.GmWorkers.WorkerLedgerMutationKind.Applied,prepared.Kind);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>BookOfEternityClient.Services.GmRuntime.GmSessionRunCoordinator.OpenProductionAsync(files));
            context.RequireOpen(); // rejected main must not close original worker authority
            Assert.False(File.Exists(files.SessionGenerationPath));Assert.False(File.Exists(files.ResolvePath("config.json")));
            Assert.False(File.Exists(Path.Combine(root,".boe_runtime/gm-runs/main.json")));
            Assert.Equal(BookOfEternityClient.Services.GmWorkers.WorkerLedgerMutationKind.Applied,await ledger.AbortBeforeLaunchAsync(prepared.Entry!,ledger.Sequence));
            context.ReleaseClient();
            await using var next=await BookOfEternityClient.Services.GmWorkers.GmWorkerRunLedger.OpenCoordinatorAsync(new(root));
            Assert.NotNull(next); // causal RED: refused main stranded an extra context reference
        } finally {
            await ledger.DisposeAsync();Directory.Delete(root,true);
        }
    }
}
