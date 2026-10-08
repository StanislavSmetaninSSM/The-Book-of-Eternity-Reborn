using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.Configuration;
using Microsoft.Win32.SafeHandles;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmMainSystemdContractTests
{
    [Fact]
    public void TransientScope_UsesFdTransferAndExactBoundedProperties()
    {
        using var synthetic=new SafeFileHandle((IntPtr)42,false); // codec bytes only; never owner authority
        var request=new SystemdScopeRequest("boe-main-"+Guid.NewGuid().ToString("N")+".scope",synthetic);
        Assert.Equal("fail",request.Mode);
        Assert.Same(synthetic,request.Properties["PIDFDs"]);
        Assert.Equal(true,request.Properties["AddRef"]);
        Assert.Equal("control-group",request.Properties["KillMode"]);
        Assert.Equal(true,request.Properties["SendSIGKILL"]);
        Assert.Equal((ulong)2_000_000,request.Properties["TimeoutStopUSec"]);
        Assert.Equal(5,request.Properties.Count);
    }
    [Fact]
    public void HeldRoot_CannotBindEmptyCgroup()
    {
        var source=new Samples(SystemdCgroupState.Empty);
        using var observation=new SystemdCgroupObservation(source);
        Assert.Throws<IOException>(()=>observation.Bind(Unit,123));
    }
    [Theory]
    [InlineData((int)SystemdCgroupState.Invalid)]
    [InlineData((int)SystemdCgroupState.Pruned)]
    public void OriginalCgroup_RejectsUnqualifiedEvidence(int state)
    {
        var source=new Samples((SystemdCgroupState)state);
        using var observation=new SystemdCgroupObservation(source);
        Assert.Throws<IOException>(()=>observation.Bind(Unit,123));
    }
    [Fact]
    public void OriginalCgroup_RequiresFreshSameIdentityEmptyAfterPopulation()
    {
        var source=new Samples(SystemdCgroupState.Populated);
        using var observation=new SystemdCgroupObservation(source);observation.Bind(Unit,123);
        Assert.False(observation.ReadFreshEmpty());source.State=SystemdCgroupState.Empty;
        Assert.True(observation.ReadFreshEmpty());source.Stale=true;
        Assert.Throws<IOException>(()=>observation.ReadFreshEmpty());
    }
    [Fact]
    public void JobReceipt_SignalBeforeReplyAndForeignUnit()
    {
        var receipts=new SystemdJobReceipts(":1.42","own.scope");
        receipts.Observe(":1.99","foreign.scope","unrelated",7,"failed");
        Assert.False(receipts.Complete("/org/freedesktop/systemd1/job/7"));
        receipts.Observe(":1.42","own.scope","/org/freedesktop/systemd1/job/7",7,"done");
        Assert.True(receipts.Complete("/org/freedesktop/systemd1/job/7"));
    }
    [Theory]
    [InlineData(":1.99",7,"done")]
    [InlineData(":1.42",8,"done")]
    public void JobReceipt_RejectsChangedSenderOrPath(string sender,uint id,string result)
    {
        var receipts=new SystemdJobReceipts(":1.42","own.scope");
        Assert.Throws<IOException>(()=>receipts.Observe(sender,"own.scope","/org/freedesktop/systemd1/job/7",id,result));
    }
    [Fact]
    public void JobReceipt_FailedCompletionIsNotSuccess()
    {
        var receipts=new SystemdJobReceipts(":1.42","own.scope");
        receipts.Observe(":1.42","own.scope","/org/freedesktop/systemd1/job/7",7,"timeout");
        Assert.Throws<IOException>(()=>receipts.Complete("/org/freedesktop/systemd1/job/7"));
    }
    [Fact]
    public void Codec_EncodesUnixFdArrayAndNoAuxiliaryUnits()
    {
        using var fd=new SafeFileHandle((IntPtr)42,false);var writer=new Writer();
        SdBusScopeCodec.Write(new("boe-main-"+Guid.NewGuid().ToString("N")+".scope",fd),writer);
        Assert.Contains("open:v:ah",writer.Events);Assert.Contains("open:a:h",writer.Events);
        Assert.Same(fd,writer.Fd);Assert.Equal("open:a:(sa(sv))",writer.Events[^2]);Assert.Equal("close",writer.Events[^1]);
    }
    [Theory]
    [InlineData("Auto")]
    [InlineData("SystemdUser")]
    public void PublicProductionSelection_RemainsClosedBeforePackageOrDirectory(string backend)
    {
        var settings=new GameSettings {GmBridgeEnabled=true,GmBridgeBackend="OwnedTerminal",GmMainOwnerBackend=backend,GmCliLaunchCommand="must-not-execute"};
        Assert.Throws<PlatformNotSupportedException>(()=>ProductionMainConfiguration.Resolve(settings,"/nonexistent-own-s1",new(80,25),"/nonexistent-own-package"));
    }
    [Fact]
    public async Task UnavailableFixture_RefusesBeforeAnyNativePackageOrChild()
    {
        var folder=Path.Combine(Path.GetTempPath(),"s1-capability-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        try {
            var bus=new CapabilityBus(false);var fixture=new SystemdControlledFixture(bus,new Samples(SystemdCgroupState.Populated));
            var created=false;var launch=NeutralTerminalLaunch.Create(Path.Combine(folder,"missing-package"),folder);
            await Assert.ThrowsAsync<PlatformNotSupportedException>(()=>OwnedTerminalSessionFactory.PrepareSystemdControlledAsync(launch,fixture,Guid.NewGuid().ToString("N"),CancellationToken.None,_=>created=true));
            Assert.False(created);Assert.Equal(0,bus.Calls);
        }finally{Directory.Delete(folder,true);}
    }
    [Fact]
    public void ControlledCapability_CannotBeConsumedTwice()
    {
        var fixture=new SystemdControlledFixture(new CapabilityBus(true),new Samples(SystemdCgroupState.Populated));
        fixture.Consume();Assert.Throws<PlatformNotSupportedException>(()=>fixture.RequireAvailable());
        Assert.Throws<PlatformNotSupportedException>(()=>fixture.Consume());
    }
    private sealed class CapabilityBus(bool available):ISystemdBusTransport
    {
        public SystemdManagerBinding Manager=>throw new InvalidOperationException();
        public Task<string> AuthorityLost {get;}=new TaskCompletionSource<string>().Task;
        public bool SupportsPidfdScopes=>available;internal int Calls;
        public Task SubscribeAsync(CancellationToken t){Calls++;throw new InvalidOperationException();}
        public Task StartScopeAsync(SystemdScopeRequest r,CancellationToken t){Calls++;throw new InvalidOperationException();}
        public Task<SystemdUnitSnapshot> ObserveAsync(string n,SafeFileHandle? f,CancellationToken t){Calls++;throw new InvalidOperationException();}
        public Task StopScopeAsync(string n,CancellationToken t){Calls++;throw new InvalidOperationException();}
        public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    }
    private static readonly SystemdUnitSnapshot Unit=new("/org/freedesktop/systemd1/unit/own",new string('a',32),"/own.scope","/org/freedesktop/systemd1/unit/own");
    private sealed class Samples(SystemdCgroupState state):ISystemdCgroupSource
    {
        private SystemdCgroupIdentity? _id;internal SystemdCgroupState State=state;internal bool Stale;
        public SystemdCgroupIdentity BindOriginal(SystemdUnitSnapshot unit,int pid)=>_id=new(unit.ControlGroup,1,1,1);
        public SystemdCgroupSample ReadOriginal(long sequence)=>new(_id!,Stale?sequence-1:sequence,State);
        public void Dispose() { }
    }
    private sealed class Writer:ISdBusMessageWriter
    {
        internal List<string> Events=[];internal SafeFileHandle? Fd;
        public void Open(char type,string signature)=>Events.Add($"open:{type}:{signature}");
        public void Close()=>Events.Add("close");public void String(string value)=>Events.Add("s:"+value);
        public void Boolean(bool value)=>Events.Add("b:"+value);public void UInt64(ulong value)=>Events.Add("t:"+value);
        public void UnixFd(SafeFileHandle value){Fd=value;Events.Add("h");}
    }
}
