using System.Text.Json;
using BookOfEternityClient.Services.GmRuntime;
using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class DaemonSettlementObservationTests
{
    [Fact]
    public void SharedReadObservesHeldGuardWithoutReleasingOriginalOwnership()
    {
        Assert.True(OperatingSystem.IsLinux());
        var root=Path.Combine(TestDiagnosticPaths.Root,"daemon-observation",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var original=LinuxDaemonInstanceGuard.Acquire(root,null);
            var path=Path.Combine(root,".boe_runtime/locks/gm-daemon-instance.lock");
            void RequireContenderBlockedBeforeStatusRead()
            {
                var statusRead=false;
                Assert.ThrowsAny<IOException>(()=>LinuxDaemonInstanceGuard.AcquireWithStatusReader(root,()=> {
                    statusRead=true; return null;
                }));
                Assert.False(statusRead); // Failure must occur at the held guard, not retained-status validation.
            }
            original.Validate();
            RequireContenderBlockedBeforeStatusRead();
            var before=OwnedTerminalScenarioDriver.ReadDaemonObservationBytes(path,root+"-read-before");
            using(var document=JsonDocument.Parse(before))
            {
                var guard=document.RootElement;
                Assert.Equal(4,guard.EnumerateObject().Count());
                Assert.Equal(1,guard.GetProperty("version").GetInt32());
                Assert.Equal(original.SessionPath,guard.GetProperty("sessionPath").GetString());
                Assert.Equal(original.OwnerId,guard.GetProperty("ownerId").GetString());
                Assert.False(guard.GetProperty("settled").GetBoolean());
            }
            original.Validate();
            RequireContenderBlockedBeforeStatusRead();
            var after=OwnedTerminalScenarioDriver.ReadDaemonObservationBytes(path,root+"-read-after");
            Assert.Equal(before,after);
            original.Validate();
            // A read does not settle the owner. Only its normal explicit operation does so.
            original.MarkSettled(JsonSerializer.Serialize(new {
                daemonGuardVersion=1,daemonGuardOwnerId=original.OwnerId,sessionPath=original.SessionPath,
                status="stopped",terminalSettled=true,pid=111 }));
        }
        finally { Directory.Delete(root,true); }
    }
}
