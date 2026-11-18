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
        // Test-first observability baseline: existing runtime discarded this failure.
        return null;
    }
    internal static void Write(Observation? observation,TextWriter sink,string? currentNonce)
    {
        if(observation==null || observation.Nonce!=currentNonce || !Guid.TryParseExact(currentNonce,"N",out _))return;
        try {sink.WriteLine("BOE_MAIN_OPERATION_FAILURE "+JsonSerializer.Serialize(observation));}
        catch { /* Diagnostic sink cannot change original failure handling. */ }
    }
}
