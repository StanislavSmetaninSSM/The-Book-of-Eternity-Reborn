using BookOfEternityClient.Services.GmRuntime;
using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class MainOperationFailureDiagnosticTests
{
    private static readonly string Nonce=Guid.NewGuid().ToString("N");
    [Theory]
    [InlineData(null,null)]
    [InlineData("malformed","malformed")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")]
    public void DisabledMalformedOrStaleNonceDoesNotExport(string? captured,string? current)
        => Assert.Null(MainOperationFailureDiagnostic.Capture(captured,current,"activate-read",null,new IOException("refused")));
    [Theory]
    [InlineData("grant-write")]
    [InlineData("activate-read")]
    [InlineData("activate-reply")]
    [InlineData("active-close-reply-read")]
    [InlineData("command")]
    [InlineData("status-publication")]
    public void OriginalFailureStageAndFullTextAreRetained(string stage)
    {
        var owner=new GmSessionRunIdentity("/original","run","generation",2,GmSessionRunBackend.LinuxSupervisor,"host","boot");
        var pin=new MainOperationReply(true,MainOperationState.Active,"pin","close","operation",owner);
        var close=new MainOperationClose("pin","close","operation",owner,MainOperationOutcome.Failed,true);
        Exception error;
        try {throw new IOException(new string('界',40000));} catch(Exception original){error=original;}
        var capture=MainOperationFailureDiagnostic.Capture(Nonce,Nonce,stage,pin,error,close:close,closing:true,sequence:7,action:"daemon-request-end",owner:owner);
        Assert.NotNull(capture);Assert.Same(pin,capture.OriginalPin);Assert.Equal(stage,capture.Stage);
        Assert.Same(owner,capture.OriginalOwner);Assert.Same(close,capture.TerminalClose);Assert.True(capture.Closing);
        Assert.NotEmpty(error.StackTrace!);Assert.Equal(error.StackTrace,capture.StackText);
        Assert.Equal(error.Message,capture.Message);Assert.Equal(error.ToString(),capture.ExceptionText);
        Assert.Equal(7,capture.Sequence);Assert.Equal("daemon-request-end",capture.Action);Assert.True(capture.CaptureComplete);
        using var sink=new StringWriter();MainOperationFailureDiagnostic.Write(capture,sink,Nonce);
        Assert.Contains("BOE_MAIN_OPERATION_FAILURE ",sink.ToString());Assert.Contains(stage,sink.ToString());
        Assert.Equal(MainOperationState.Active,pin.State);
    }
    [Fact]
    public void ExportRejectsNonceChangedAfterCapture()
    {
        var capture=MainOperationFailureDiagnostic.Capture(Nonce,Nonce,"close-read",null,new IOException("original"));
        Assert.NotNull(capture);
        using var sink=new StringWriter();MainOperationFailureDiagnostic.Write(capture,sink,Guid.NewGuid().ToString("N"));
        Assert.Equal("",sink.ToString());
    }
    private sealed class HostileException:Exception {public override string Message=>throw new IOException("getter");public override string StackTrace=>"original independent stack";}
    private sealed class HostileSink:StringWriter {public int Attempts;public override void WriteLine(string? value){Attempts++;throw new IOException("sink");}}
    [Fact]
    public void GetterAndSinkFailureAreDiagnosticOnly()
    {
        var capture=MainOperationFailureDiagnostic.Capture(Nonce,Nonce,"close-read",null,new HostileException());
        Assert.NotNull(capture);Assert.False(capture.CaptureComplete);Assert.NotNull(capture.CaptureFailure);
        Assert.Equal("original independent stack",capture.StackText);
        var sink=new HostileSink();MainOperationFailureDiagnostic.Write(capture,sink,Nonce);Assert.Equal(1,sink.Attempts);
    }
}
