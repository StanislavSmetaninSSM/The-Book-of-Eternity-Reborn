using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;

namespace BookOfEternityClient.Tests;

public sealed class GmMainRunFenceTests : IDisposable
{
    private readonly string _root=Path.Combine(Path.GetTempPath(),"main-fence-"+Guid.NewGuid().ToString("N"));
    private readonly FileSystemManager _files;
    public GmMainRunFenceTests(){_files=new(_root,NullLogger<FileSystemManager>.Instance);_files.EnsureDirectoryStructure();}
    private string RecordPath=>Path.Combine(_root,".boe_runtime/gm-runs/main.json");
    private GmSessionRunRecord Write(GmSessionRunDisposition state)
    {
        var id=new GmSessionRunIdentity(_files.BasePath,Guid.NewGuid().ToString("N"),Guid.Empty.ToString("N"),1,
            GmSessionRunBackend.LinuxSupervisor,Guid.NewGuid().ToString("N"),"fixture-boot");
        var r=new GmSessionRunRecord(1,id,state,state==GmSessionRunDisposition.Stopped?new(id,GmSessionRunStopKind.OwnedScopeEmpty,id.BootId):null);
        Directory.CreateDirectory(Path.GetDirectoryName(RecordPath)!);File.WriteAllBytes(RecordPath,GmSessionRunRecordCodec.Encode(r));return r;
    }
    [Theory]
    [InlineData((int)GmSessionRunDisposition.Prepared)]
    [InlineData((int)GmSessionRunDisposition.Running)]
    [InlineData((int)GmSessionRunDisposition.Stopping)]
    [InlineData((int)GmSessionRunDisposition.Uncertain)]
    public async Task ColdNonterminal_RefusesBeforeRecoveryAndMutation(int disposition)
    {
        Write((GmSessionRunDisposition)disposition);var before=File.ReadAllBytes(RecordPath);
        var error=await Record.ExceptionAsync(async()=>{await using var lease=await _files.AcquireCanonicalWriteLeaseAsync();});
        Assert.NotNull(error);Assert.Equal(before,File.ReadAllBytes(RecordPath));
        Assert.False(File.Exists(_files.SessionGenerationPath));
        Assert.False(Directory.Exists(Path.Combine(_root,".boe_runtime/trusted-local-publication-v1")));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task InitializedMissingOrInvalid_RefusesClearBeforeEffects(bool corrupt)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RecordPath)!);if(corrupt)File.WriteAllText(RecordPath,"{");
        var marker=_files.ResolvePath("game_state/marker.txt");File.WriteAllText(marker,"original");
        var error=await Record.ExceptionAsync(()=>_files.ClearGameStateAsync());
        Assert.NotNull(error);Assert.Equal("original",File.ReadAllText(marker));Assert.False(File.Exists(_files.SessionGenerationPath));
    }
    [Fact]
    public async Task HeldLease_RechecksMainRecordBeforeWrite()
    {
        await using var lease=await _files.AcquireCanonicalWriteLeaseAsync();Write(GmSessionRunDisposition.Running);
        var error=await Record.ExceptionAsync(()=>_files.WriteFileAtomicAsync(lease,"game_state/blocked.txt","blocked"));
        Assert.NotNull(error);Assert.False(File.Exists(_files.ResolvePath("game_state/blocked.txt")));
    }
    [Fact]
    public async Task StoppedTombstone_ClearRetainsEpochOutsideReplacement()
    {
        Write(GmSessionRunDisposition.Stopped);var before=File.ReadAllBytes(RecordPath);
        await _files.ClearGameStateAsync();Assert.Equal(before,File.ReadAllBytes(RecordPath));
    }
    [Theory]
    [InlineData((int)MainRunIoStage.NamespaceCreated)]
    [InlineData((int)MainRunIoStage.Staged)]
    [InlineData((int)MainRunIoStage.FileFlushed)]
    [InlineData((int)MainRunIoStage.Renamed)]
    [InlineData((int)MainRunIoStage.DirectoryFlushed)]
    [InlineData((int)MainRunIoStage.Readback)]
    public async Task MetadataFault_RetainsFrozenPlanAndOriginalGuard(int stage)
    {
        using var guard=await GmMainOwnerGuard.AcquireAsync(_files.BasePath);
        var fired=false;var disk=new GmSessionRunPersistence(guard,s=>{if(!fired && (int)s==stage){fired=true;throw new IOException("owned metadata fault");}});
        var id=new GmSessionRunIdentity(_files.BasePath,Guid.NewGuid().ToString("N"),Guid.Empty.ToString("N"),1,
            GmSessionRunBackend.LinuxSupervisor,Guid.NewGuid().ToString("N"),"fixture-boot");
        var prepared=new GmSessionRunRecord(1,id,GmSessionRunDisposition.Prepared,null);
        Assert.Throws<IOException>(()=>disk.Publish(null,prepared));Assert.True(disk.HasDebt);
        Assert.Throws<IOException>(()=>disk.Publish(null,prepared with{Disposition=GmSessionRunDisposition.Running}));
        Task<Exception?> contender;
        using(ExecutionContext.SuppressFlow())contender=Task.Run(()=>Record.ExceptionAsync(async()=>{await using var l=await _files.AcquireCanonicalWriteLeaseAsync();}));
        Assert.NotNull(await contender);
        disk.Retry();Assert.False(disk.HasDebt);Assert.Equal(GmSessionRunRecordCodec.Encode(prepared),File.ReadAllBytes(RecordPath));
    }
    [Fact]
    public async Task InitializationParentBarrier_IsReplayedBeforeRetryAck()
    {
        using var guard=await GmMainOwnerGuard.AcquireAsync(_files.BasePath);var calls=0;
        var disk=new GmSessionRunPersistence(guard,s=>{if(s==MainRunIoStage.BeforeNamespaceParentFlush && ++calls==1)throw new IOException("parent barrier unavailable");});
        var id=new GmSessionRunIdentity(_files.BasePath,Guid.NewGuid().ToString("N"),Guid.Empty.ToString("N"),1,GmSessionRunBackend.LinuxSupervisor,Guid.NewGuid().ToString("N"),"fixture-boot");
        Assert.Throws<IOException>(()=>disk.Publish(null,new(1,id,GmSessionRunDisposition.Prepared,null)));
        disk.Retry();Assert.Equal(2,calls);
    }
    [Fact]
    public async Task ActualBrowserLoad_GuardSpansCommittedMenuRefreshAndClosing()
    {
        var state=PortableSaveFixture.Seed(_files);
        var save=new SaveLoadService(_files,state,NullLogger<SaveLoadService>.Instance);
        Assert.True(await save.SaveGameAsync("main-fence","synthetic fixture"));
        var path=Assert.Single(Directory.GetFiles(_files.ResolvePath("saves/manual_saves"),"*.zip"));
        var writes=new BrowserLocalWriteCoordinator(_files,new LocalUiSessionLockService(_files));
        var session=new LocalWebUiSessionStatusService(_files,writes);
        var dashboard=new BrowserLifecycleDashboardService(_files,session,new ValidationService(_files,NullLogger<ValidationService>.Instance));
        var menu=new LocalWebUiMainMenuService(_files,dashboard,save,state,writes);var guarded=false;
        menu.BeforeCommittedMenuRefresh=async()=>{
            Task<bool> competitor;
            using(ExecutionContext.SuppressFlow())competitor=Task.Run(async()=>{try{using var g=await GmMainOwnerGuard.AcquireAsync(_files.BasePath);return false;}catch(IOException){return true;}});
            guarded=await competitor;
        };
        var result=await menu.LoadSaveAsync(new("manual:"+Path.GetFileName(path)));
        Assert.True(result.Success);Assert.False(result.ContinuationBlocked);Assert.True(guarded);
        using var after=await GmMainOwnerGuard.AcquireAsync(_files.BasePath);
    }
    public void Dispose(){if(Directory.Exists(_root))Directory.Delete(_root,true);}
}
