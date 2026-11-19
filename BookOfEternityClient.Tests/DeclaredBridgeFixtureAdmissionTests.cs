using System.Text.Json;
using BookOfEternityClient.Services.GmRuntime;
using Xunit;
namespace BookOfEternityClient.Tests;

public sealed class DeclaredBridgeFixtureAdmissionTests
{
    private const string Root="/tmp/authored-declared-fixture", Run="11111111111111111111111111111111", Generation="22222222222222222222222222222222";
    private static readonly TerminalIdentity Owner=new(Run,"native-lineage","owned",123);
    private static string Gen(string id=Generation)=>JsonSerializer.Serialize(new{SchemaVersion=1,GenerationId=id});
    private static byte[] Record(string generation=Generation,string run=Run,string root=Root,long epoch=1)=>GmSessionRunRecordCodec.Encode(new(1,
        new(root,run,generation,epoch,GmSessionRunBackend.LinuxSupervisor,"33333333333333333333333333333333","boot"),GmSessionRunDisposition.Running,null));
    private static string Status(string kind="linux-fifo-v1",string session=Root+"/game_session",string run=Run,int pid=123,bool retained=false)=>JsonSerializer.Serialize(new{
        sessionPath=session,pipeName="authored",terminalRunId=run,shellPid=pid,terminalUncertain=false,terminalOwnerRetained=retained,ready=false,
        transportEndpoint=new{version=1,kind,address=kind=="linux-fifo-v1"?"/tmp/boe-bridge-44444444444444444444444444444444":"/tmp/CoreFxPipe_authored",transportRun=kind=="linux-fifo-v1"?"44444444444444444444444444444444":null}});
    private static DeclaredBridgeFixtureAdmission Fresh()=>new(Root,"authored",Owner,Gen(),Record());
    [Theory]
    [InlineData("linux-fifo-v1")]
    [InlineData("named-pipe-v1")]
    public void ExactDeclaredEndpoint_ReadyFalseAndPersistedUnretainedAreNotCircularGates(string kind)
    {
        var gate=Fresh();var status=Status(kind);Assert.Equal(status,gate.BeforeConnect(status,Gen(),Record(),false,false));
        using var reply=JsonDocument.Parse("{\"ok\":true,\"status\":"+Status(kind,retained:true)+"}");gate.CheckReply(reply.RootElement,false);
    }
    [Fact]
    public void ChangedOrMissingIdentityGenerationAndPhysicalLoss_RefuseBeforeConnector()
    {
        var calls=0;
        void Attempt(string status,string gen,byte[] record,bool exited=false,bool lost=false){Fresh().BeforeConnect(status,gen,record,exited,lost);calls++;}
        Assert.ThrowsAny<Exception>(()=>Attempt(Status(),"",Record()));
        Assert.ThrowsAny<Exception>(()=>Attempt(Status(),Gen(),Array.Empty<byte>()));
        Assert.ThrowsAny<Exception>(()=>Attempt(Status(),Gen("55555555555555555555555555555555"),Record()));
        Assert.ThrowsAny<Exception>(()=>Attempt(Status(),Gen(),Record(epoch:2)));
        Assert.ThrowsAny<Exception>(()=>Attempt(Status(),Gen(),Record(root:"/tmp/foreign")));
        Assert.ThrowsAny<Exception>(()=>Attempt(Status(),Gen(),Record(run:"55555555555555555555555555555555")));
        Assert.ThrowsAny<Exception>(()=>Attempt(Status(),Gen(),Record(),true));
        Assert.ThrowsAny<Exception>(()=>Attempt(Status(),Gen(),Record(),lost:true));
        Assert.Equal(0,calls);
    }
    [Fact]
    public void MalformedForeignAndDriftingEndpoint_RefuseBeforeConnector()
    {
        foreach(var status in new[]{"{}",Status().Replace("\"version\":1","\"version\":2"),Status("unknown"),Status(session:"/tmp/foreign/game_session"),Status(pid:456),Status(run:"55555555555555555555555555555555")})
            Assert.ThrowsAny<Exception>(()=>Fresh().BeforeConnect(status,Gen(),Record(),false,false));
        var gate=Fresh();gate.BeforeConnect(Status(),Gen(),Record(),false,false);
        Assert.ThrowsAny<Exception>(()=>gate.BeforeConnect(Status("named-pipe-v1"),Gen(),Record(),false,false));
    }
    [Fact]
    public void OrdinaryRetentionAndShutdownReceiptHaveDistinctContracts()
    {
        var gate=Fresh();gate.BeforeConnect(Status(),Gen(),Record(),false,false);
        using var retired=JsonDocument.Parse("{\"ok\":true,\"status\":"+Status()+"}");
        Assert.ThrowsAny<Exception>(()=>gate.CheckReply(retired.RootElement,false));
        gate.CheckReply(retired.RootElement,true); // Existing actual caller still verifies the shutdown receipt.
    }
}
