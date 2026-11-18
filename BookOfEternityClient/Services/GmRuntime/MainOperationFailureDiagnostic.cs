using System.Text.Json;
namespace BookOfEternityClient.Services.GmRuntime;

// Observational DATA only. Never used for admission, retirement, retry or cleanup.
internal static class MainOperationFailureDiagnostic
{
    internal sealed record Observation(string Nonce,string Stage,GmSessionRunIdentity? OriginalOwner,MainOperationReply? OriginalPin,
        MainOperationClose? TerminalClose,bool? Closing,long? Sequence,string? Action,
        string ExceptionType,string? Message,string? StackText,string? ExceptionText,
        bool CaptureComplete,string? CaptureFailure,
        string Consistency="Caller-supplied immutable observations; stage identifies capture site");
    internal static string? ReadNonce()
    {
#if DEBUG
        try {var value=Environment.GetEnvironmentVariable(GmSessionRunCoordinator.StopDiagnosticNonceVariable);
            return Guid.TryParseExact(value,"N",out _)?value:null;} catch {return null;}
#else
        return null;
#endif
    }
    internal static Observation? Capture(string? capturedNonce,string? currentNonce,string stage,
        MainOperationReply? pin,Exception failure,MainOperationClose? close=null,bool? closing=null,
        long? sequence=null,string? action=null,GmSessionRunIdentity? owner=null)
    {
        if(capturedNonce!=currentNonce || !Guid.TryParseExact(capturedNonce,"N",out _))return null;
        string? message=null,stack=null,text=null,captureFailure=null;
        try {message=failure.Message;}catch(Exception error){captureFailure=error.GetType().FullName;}
        try {stack=failure.StackTrace;}catch(Exception error){captureFailure??=error.GetType().FullName;}
        try {text=failure.ToString();}catch(Exception error){captureFailure??=error.GetType().FullName;}
        return new(capturedNonce!,stage,owner,pin,close,closing,sequence,action,
            failure.GetType().FullName!,message,stack,text,captureFailure==null,captureFailure);
    }
    internal static void Report(string? nonce,string stage,MainOperationReply? pin,Exception failure,
        MainOperationClose? close=null,bool? closing=null,long? sequence=null,string? action=null,GmSessionRunIdentity? owner=null)
    {
        try {Write(Capture(nonce,ReadNonce(),stage,pin,failure,close,closing,sequence,action,owner),Console.Error,ReadNonce());}
        catch { /* Observation must not replace the original failure. */ }
    }
    internal static void Mark(string? nonce,string stage,MainOperationReply? pin=null,MainOperationClose? close=null)
    {
        try {
            if(nonce==null || nonce!=ReadNonce())return;
            Console.Error.WriteLine("BOE_MAIN_OPERATION_MARKER "+JsonSerializer.Serialize(new{Nonce=nonce,Stage=stage,ProcessId=Environment.ProcessId,ObservedAtUtc=DateTime.UtcNow,OriginalPin=pin,OriginalClose=close}));
        }catch { }
    }
    internal static void Write(Observation? observation,TextWriter sink,string? currentNonce)
    {
        if(observation==null || observation.Nonce!=currentNonce || !Guid.TryParseExact(currentNonce,"N",out _))return;
        try {sink.WriteLine("BOE_MAIN_OPERATION_FAILURE "+JsonSerializer.Serialize(observation));}
        catch { /* Diagnostic sink cannot change original failure handling. */ }
    }
}
