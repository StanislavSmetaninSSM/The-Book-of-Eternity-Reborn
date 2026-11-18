using System.Text.Json;
using System.Diagnostics;
using System.Runtime.InteropServices;
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
                first.MarkSettled(Stopped(session,oldOwner));
            }
            using var second=LinuxDaemonInstanceGuard.Acquire(root,Stopped(session,oldOwner));
            second.Validate(); Assert.NotEqual(oldOwner,second.OwnerId);
        } finally {Directory.Delete(root,true);}
    }
    [Theory]
    [InlineData("missing")][InlineData("null")][InlineData("legacy")][InlineData("running")]
    [InlineData("failed")][InlineData("unsettled")][InlineData("wrong-session")][InlineData("wrong-owner")]
    [InlineData("duplicate")][InlineData("case-collision")][InlineData("string-version")][InlineData("string-settled")]
    public void ExistingGuardRequiresExactCleanPriorOwner(string fault)
    {
        var root=NewRoot();
        try {
            string status;
            using(var first=LinuxDaemonInstanceGuard.Acquire(root,null)) {status=Stopped(first.SessionPath,first.OwnerId);first.MarkSettled(status);}
            string? candidate=fault switch {
                "missing"=>null,"null"=>"null","legacy"=>"{\"status\":\"stopped\",\"pid\":111}",
                "running"=>status.Replace("stopped","running"),"failed"=>status.Replace("stopped","failed"),
                "unsettled"=>status.Replace("true","false"),"wrong-session"=>status.Replace("game_session","wrong_session"),
                "wrong-owner"=>status.Replace("daemonGuardOwnerId","wrongOwner"),
                "duplicate"=>status.Replace("{","{\"status\":\"running\","),
                "case-collision"=>status.Replace("{","{\"Status\":\"running\","),
                "string-version"=>status.Replace("\"daemonGuardVersion\":1","\"daemonGuardVersion\":\"1\""),
                "string-settled"=>status.Replace("true","\"true\""),_=>throw new InvalidOperationException()};
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
    [Fact]
    public async Task ActualIndependentProcessExclusionAndEofRelease()
    {
        var root=NewRoot();
        try {
            var owner=await Peer(null, async record => {
                Assert.ThrowsAny<IOException>(()=>LinuxDaemonInstanceGuard.Acquire(root,Stopped(record.Session,record.Owner)));
                await Task.CompletedTask;
            });
            var statusPath=Path.Combine(root,"prior-status.json");
            await File.WriteAllTextAsync(statusPath,Stopped(owner.Session,owner.Owner));
            var next=await Peer(statusPath, _=>Task.CompletedTask);
            Assert.NotEqual(owner.Owner,next.Owner);
        } finally {Directory.Delete(root,true);}

        async Task<(string Owner,string Session)> Peer(string? status,Func<(string Owner,string Session),Task> body)
        {
            var start=new ProcessStartInfo("pwsh") {UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
            foreach(var arg in new[]{"-NoProfile","-File",Path.Combine(TestRepoPaths.RepoRoot,"tests/fixtures/GmDaemonSingleton/peer.ps1"),
                "-Assembly",typeof(LinuxDaemonInstanceGuard).Assembly.Location,"-Root",root})start.ArgumentList.Add(arg);
            if(status is not null){start.ArgumentList.Add("-StatusPath");start.ArgumentList.Add(status);}
            using var child=Process.Start(start)!;
            var errors=child.StandardError.ReadToEndAsync();
            try {
                var line=await child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));
                if(line is null) Assert.Fail("Peer returned EOF before ready: "+await errors);
                using var json=JsonDocument.Parse(line!);
                var result=(json.RootElement.GetProperty("owner").GetString()!,json.RootElement.GetProperty("session").GetString()!);
                await body(result);
                child.StandardInput.Close();
                await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal("",await child.StandardOutput.ReadToEndAsync());
                Assert.Equal("",await errors);Assert.Equal(0,child.ExitCode);
                return result;
            } finally {
                child.StandardInput.Close();
                if(!child.HasExited){child.Kill(true);await child.WaitForExitAsync();}
                await errors;
            }
        }
    }

    [Fact]
    public void FailedStatusReaderDoesNotGrantOrPermitFreshRetry()
    {
        var root=NewRoot();
        try {
            var reads=0;
            Assert.Throws<IOException>(()=>LinuxDaemonInstanceGuard.AcquireWithStatusReader(root,()=>{reads++;throw new IOException("read refused");}));
            Assert.Equal(1,reads);
            Assert.ThrowsAny<IOException>(()=>LinuxDaemonInstanceGuard.Acquire(root,null));
        } finally {Directory.Delete(root,true);}
    }
    [Fact]
    public void CallbackRunsUnderHeldExclusionAndIsNotRetained()
    {
        var root=NewRoot();
        try {
            var reads=0;
            using var held=LinuxDaemonInstanceGuard.AcquireWithStatusReader(root,()=>{
                reads++;Assert.ThrowsAny<IOException>(()=>LinuxDaemonInstanceGuard.Acquire(root,null));return null;
            });
            held.Validate();held.Validate();Assert.Equal(1,reads);
        } finally {Directory.Delete(root,true);}
    }

    [Fact]
    public void WrittenStoppedStatusWithoutConfirmedSettlementCannotRestart()
    {
        var root=NewRoot();
        try {
            string status;
            using(var first=LinuxDaemonInstanceGuard.Acquire(root,null)) status=Stopped(first.SessionPath,first.OwnerId);
            Assert.ThrowsAny<IOException>(()=>LinuxDaemonInstanceGuard.Acquire(root,status));
        } finally {Directory.Delete(root,true);}
    }

    [DllImport("libc",EntryPoint="link",SetLastError=true)]
    private static extern int CreateLink(string existing,string added);
    [Theory]
    [InlineData("mode")][InlineData("hardlink")]
    public void HeldGuardRechecksPrivateSingleLinkFile(string mutation)
    {
        var root=NewRoot();
        try {
            using var guard=LinuxDaemonInstanceGuard.Acquire(root,null);
            var path=Path.Combine(root,".boe_runtime/locks/gm-daemon-instance.lock");
            if(mutation=="mode") File.SetUnixFileMode(path,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.OtherRead);
            else Assert.Equal(0,CreateLink(path,path+".link"));
            Assert.ThrowsAny<IOException>(guard.Validate);
        } finally {Directory.Delete(root,true);}
    }

}
