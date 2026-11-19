using System.Text.Json;
using System.Diagnostics;
namespace BookOfEternityClient.Services.GmRuntime;

// Observational DATA only. Never used for admission, retirement, retry or cleanup.
internal static class MainOperationFailureDiagnostic
{
    internal sealed record Observation(string Nonce,string Stage,DateTimeOffset CapturedAtUtc,long CaptureMonotonicTimestamp,GmSessionRunIdentity? OriginalOwner,MainOperationReply? OriginalPin,
        MainOperationClose? OriginalCloseProjection,MainOperationClose? TerminalClose,bool? Closing,long? Sequence,string? Action,
        string ExceptionType,string? Message,string? StackText,string? ExceptionText,
        bool CaptureComplete,string? CaptureFailure,
        string Consistency="Caller-supplied immutable observations; timestamp is DATA capture entry before text extraction, not event or authority time");
    // Per-acquisition immutable DATA, never a registry or an authority token.
    internal sealed record PhysicalLeaseContext(string Nonce, long Occurrence, string Root,
        string Purpose, MainOperationReply? OriginalPin);
    internal sealed record PhysicalLeaseObservation(string Nonce, long Occurrence, string Root,
        string Purpose, MainOperationReply? OriginalPin, string Stage, int Attempt,
        int ProcessId, DateTimeOffset CapturedAtUtc, long CaptureMonotonicTimestamp);
    private static long _physicalOccurrence;

    internal static PhysicalLeaseContext? BeginPhysicalLease(string? nonce, string root,
        string purpose, MainOperationReply? pin)
    {
        try
        {
            if (!Guid.TryParseExact(nonce, "N", out _) || nonce != ReadNonce() ||
                !GmSessionRunValidation.IsRootKey(root) || string.IsNullOrWhiteSpace(purpose)) return null;
            if (pin != null)
            {
                GmSessionRunValidation.ValidateIdentity(pin.Identity);
                if (!pin.Ok || pin.State is null || !Enum.IsDefined(pin.State.Value) ||
                    !GmSessionRunValidation.IsId(pin.PinId, false) ||
                    !GmSessionRunValidation.IsId(pin.CloseId, false) ||
                    !GmSessionRunValidation.IsId(pin.OperationId, false) || pin.Error != null ||
                    !GmSessionRunValidation.AdmissionRootMatches(pin.Identity!.RootKey, root, pin.Identity.Backend)) return null;
            }
            return new(nonce!, Interlocked.Increment(ref _physicalOccurrence), root, purpose, pin);
        }
        catch { return null; }
    }

    internal static PhysicalLeaseObservation? CapturePhysical(PhysicalLeaseContext? context, string stage, int attempt)
    {
        if (context == null) return null;
        try
        {
            // Capture immediately at the physical transition, before nonce lookup/serialization/sink.
            var utc = DateTimeOffset.UtcNow;
            var monotonic = Stopwatch.GetTimestamp();
            if (context.Nonce != ReadNonce() || attempt < 1 || stage is not
                ("physical-opened" or "lease-granted" or "physical-dispose-completed" or "contention-exhausted")) return null;
            return new(context.Nonce, context.Occurrence, context.Root, context.Purpose, context.OriginalPin,
                stage, attempt, Environment.ProcessId, utc, monotonic);
        }
        catch { return null; }
    }

    internal static void WritePhysical(PhysicalLeaseObservation? observation, TextWriter sink, string? currentNonce)
    {
        try
        {
            if (observation == null || observation.Nonce != currentNonce ||
                !Guid.TryParseExact(currentNonce, "N", out _)) return;
            sink.WriteLine("BOE_CANONICAL_LEASE_OBSERVATION " + JsonSerializer.Serialize(observation));
        }
        catch { /* Synchronous stderr can block; only exception/policy noninterference is promised. */ }
    }

    internal static void ReportPhysical(PhysicalLeaseContext? context, string stage, int attempt)
    {
        if (context == null) return;
        try { WritePhysical(CapturePhysical(context, stage, attempt), Console.Error, ReadNonce()); }
        catch { /* Observation cannot replace acquisition/disposal results. */ }
    }
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
        long? sequence=null,string? action=null,GmSessionRunIdentity? owner=null,MainOperationClose? projection=null)
    {
        var capturedAt=DateTimeOffset.UtcNow;var monotonic=Stopwatch.GetTimestamp();
        if(capturedNonce!=currentNonce || !Guid.TryParseExact(capturedNonce,"N",out _))return null;
        string? message=null,stack=null,text=null,captureFailure=null;
        try {message=failure.Message;}catch(Exception error){captureFailure=error.GetType().FullName;}
        try {stack=failure.StackTrace;}catch(Exception error){captureFailure??=error.GetType().FullName;}
        try {text=failure.ToString();}catch(Exception error){captureFailure??=error.GetType().FullName;}
        return new(capturedNonce!,stage,capturedAt,monotonic,owner,pin,projection,close,closing,sequence,action,
            failure.GetType().FullName!,message,stack,text,captureFailure==null,captureFailure);
    }
    internal static void Report(string? nonce,string stage,MainOperationReply? pin,Exception failure,
        MainOperationClose? close=null,bool? closing=null,long? sequence=null,string? action=null,GmSessionRunIdentity? owner=null,MainOperationClose? projection=null)
    {
        try {Write(Capture(nonce,ReadNonce(),stage,pin,failure,close,closing,sequence,action,owner,projection),Console.Error,ReadNonce());}
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
