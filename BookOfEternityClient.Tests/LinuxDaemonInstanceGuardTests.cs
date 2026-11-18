using System.Text.Json;
using BookOfEternityClient.Services.GmRuntime;
using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class LinuxDaemonInstanceGuardTests
{
    private static string NewRoot()
    {
        var path = Path.Combine(TestDiagnosticPaths.Root,"daemon-singleton",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path); return path;
    }
    private static string Stopped(string session,string owner) => JsonSerializer.Serialize(new {
        daemonGuardVersion=1,daemonGuardOwnerId=owner,sessionPath=session,status="stopped",terminalSettled=true,pid=111 });
    [Fact]
    public void FreshRootThenExactCleanStopCanRestartDespiteHistoricalPid()
    {
        var root=NewRoot();
        try {
            string oldOwner,session;
            using(var first=LinuxDaemonInstanceGuard.Acquire(root,null)) {
                first.Validate(); oldOwner=first.OwnerId; session=first.SessionPath;
                Assert.Equal(Path.Combine(root,"game_session"),session);
                Assert.True(Guid.TryParseExact(oldOwner,"N",out _));
            }
            using var second=LinuxDaemonInstanceGuard.Acquire(root,Stopped(session,oldOwner));
            second.Validate(); Assert.NotEqual(oldOwner,second.OwnerId);
        } finally {Directory.Delete(root,true);}
    }
    [Theory]
    [InlineData("missing")][InlineData("null")][InlineData("legacy")][InlineData("running")]
    [InlineData("failed")][InlineData("unsettled")][InlineData("wrong-session")][InlineData("wrong-owner")]
    public void ExistingGuardRequiresExactCleanPriorOwner(string fault)
    {
        var root=NewRoot();
        try {
            string status;
            using(var first=LinuxDaemonInstanceGuard.Acquire(root,null)) status=Stopped(first.SessionPath,first.OwnerId);
            string? candidate=fault switch {
                "missing"=>null,"null"=>"null","legacy"=>"{\"status\":\"stopped\",\"pid\":111}",
                "running"=>status.Replace("stopped","running"),"failed"=>status.Replace("stopped","failed"),
                "unsettled"=>status.Replace("true","false"),"wrong-session"=>status.Replace("game_session","wrong_session"),
                "wrong-owner"=>status.Replace("daemonGuardOwnerId","wrongOwner"),_=>throw new InvalidOperationException()};
            Assert.ThrowsAny<IOException>(()=>LinuxDaemonInstanceGuard.Acquire(root,candidate));
        } finally {Directory.Delete(root,true);}
    }
    [Fact]
    public void HeldGuardRejectsSecondOwnerAndAllowsDifferentRoot()
    {
        var root=NewRoot();var other=NewRoot();
        try {
            using var first=LinuxDaemonInstanceGuard.Acquire(root,null);
            Assert.ThrowsAny<IOException>(()=>LinuxDaemonInstanceGuard.Acquire(root,Stopped(first.SessionPath,first.OwnerId)));
            using var distinct=LinuxDaemonInstanceGuard.Acquire(other,null); distinct.Validate(); first.Validate();
        } finally {Directory.Delete(root,true);Directory.Delete(other,true);}
    }
    [Fact]
    public void ReplacedGuardPathCannotValidateOriginalHandle()
    {
        var root=NewRoot();
        try {
            using var held=LinuxDaemonInstanceGuard.Acquire(root,null);
            var path=Path.Combine(root,".boe_runtime/locks/gm-daemon-instance.lock");
            File.Move(path,path+".original");File.WriteAllText(path,"replacement");
            Assert.ThrowsAny<IOException>(held.Validate);
        } finally {Directory.Delete(root,true);}
    }
}
