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
    [Fact]
    public void RetainedMainHandshake_RequiresExactOriginalGrantAndTransitions()
    {
        var identity=GmSessionRunRecordCodec.Decode(Record()).Identity;
        var operation="55555555555555555555555555555555";
        var grant=new MainOperationReply(true,MainOperationState.PreparedGrant,"66666666666666666666666666666666","77777777777777777777777777777777",operation,identity);
        DeclaredBridgeFixtureAdmission.RequireMainGrant(grant,identity,operation);
        foreach(var bad in new[]{grant with{Ok=false},grant with{State=MainOperationState.Active},grant with{PinId="bad"},grant with{CloseId="bad"},grant with{OperationId="other"},grant with{Identity=identity with{Epoch=2}}})
            Assert.Throws<InvalidDataException>(()=>DeclaredBridgeFixtureAdmission.RequireMainGrant(bad,identity,operation));
        Assert.Throws<InvalidDataException>(()=>DeclaredBridgeFixtureAdmission.RequireMainGrant(null,identity,operation));
        foreach(var state in new[]{MainOperationState.Active,MainOperationState.ClosedObserved}) {
            var actual=grant with{State=state};DeclaredBridgeFixtureAdmission.RequireMainTransition(actual,grant,state);
            foreach(var bad in new[]{actual with{PinId="88888888888888888888888888888888"},actual with{CloseId="88888888888888888888888888888888"},actual with{OperationId="other"},actual with{Identity=identity with{Epoch=2}},actual with{Ok=false},grant})
                Assert.Throws<InvalidDataException>(()=>DeclaredBridgeFixtureAdmission.RequireMainTransition(bad,grant,state));
        }
    }

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
        foreach(var status in new[]{"{}",Status().Replace("\"version\":1","\"version\":2"),Status("unknown"),Status(session:"/tmp/foreign/game_session"),Status(pid:456),Status().Replace("\"pipeName\":\"authored\"","\"pipeName\":\"foreign\""),Status().Replace("\"terminalUncertain\":false","\"terminalUncertain\":true"),Status(run:"55555555555555555555555555555555")})
            Assert.ThrowsAny<Exception>(()=>Fresh().BeforeConnect(status,Gen(),Record(),false,false));
        var gate=Fresh();gate.BeforeConnect(Status(),Gen(),Record(),false,false);
        Assert.ThrowsAny<Exception>(()=>gate.BeforeConnect(Status("named-pipe-v1"),Gen(),Record(),false,false));
    }
    [Fact]
    public void OrdinaryRetentionAndShutdownReceiptHaveDistinctContracts()
    {
        var gate=Fresh();gate.BeforeConnect(Status(),Gen(),Record(),false,false);
        using var live=JsonDocument.Parse("{\"ok\":true,\"status\":"+Status()+"}");
        Assert.ThrowsAny<Exception>(()=>gate.CheckReply(live.RootElement,false));
        Assert.ThrowsAny<Exception>(()=>gate.CheckReply(live.RootElement,true));
        using var initial=JsonDocument.Parse(Status());
        var wireOptions=new JsonSerializerOptions {PropertyNamingPolicy=JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            Converters={new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower)}};
        var status=JsonSerializer.SerializeToNode(new {SessionPath=Root+"/game_session",PipeName="authored",
            TerminalRunId=Owner.RunId,ShellPid=(int?)null,TerminalOwnerRetained=false,TerminalUncertain=false,
            TransportEndpoint=initial.RootElement.GetProperty("transportEndpoint").Clone(),
            TerminalStop=new TerminalStopEvidence(Owner,BookOfEternityClient.Services.GmWorkers.GmWorkerStopState.StoppedWithinScope,"owned",true,false)},wireOptions)!;
        Assert.Null(status["shellPid"]);
        Assert.False(status.AsObject().ContainsKey("shellPid"));
        var valid="{\"ok\":true,\"status\":"+status.ToJsonString()+"}";
        using var retired=JsonDocument.Parse(valid);gate.CheckReply(retired.RootElement,true);
        using var explicitNull=JsonDocument.Parse(valid.Replace("\"terminalRunId\":", "\"shellPid\":null,\"terminalRunId\":"));
        gate.CheckReply(explicitNull.RootElement,true);
        foreach(var invalid in new[]{valid.Replace("\"terminalRunId\":","\"shellPid\":123,\"terminalRunId\":"),valid.Replace("\"terminalOwnerRetained\":false","\"terminalOwnerRetained\":true"),
            valid.Replace("\"terminalUncertain\":false","\"terminalUncertain\":true"),
            valid.Replace("\"rootPid\":123","\"rootPid\":456"),
            valid.Replace("\"cleanupComplete\":true","\"cleanupComplete\":false"),
            valid.Replace("\"authorityRetained\":false","\"authorityRetained\":true"),
            valid.Replace("stopped-within-scope","uncertain")})
        {using var bad=JsonDocument.Parse(invalid);Assert.ThrowsAny<Exception>(()=>gate.CheckReply(bad.RootElement,true));}

    }
}
