using System.Reflection;
using System.Text.Json;
using System.Text;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.Services.GmWorkers;

namespace BookOfEternityClient.Tests;

// Finite contract assertions for the nonce-gated failure report, never a Stop replacement.
internal static class MainStopDiagnosticScenarioAssertions
{
    internal const string NonceVariable="BOE_MAIN_STOP_DIAGNOSTIC_NONCE";
    internal const string Prefix="BOE_MAIN_STOP_FAILURE ";
    internal const string FirstExceptionKey="BOE.MainStop.FirstException";
    internal const string CurrentExceptionKey="BOE.MainStop.CurrentException";

    internal static JsonElement ReadReport(string output, string nonce, GmSessionRunIdentity identity,
        string firstType, string currentPredicate)
    {
        var lines=output.Split('\n').Where(line=>line.StartsWith(Prefix,StringComparison.Ordinal)).ToArray();
        Require(lines.Length==1,"Missing exactly one first Stop failure diagnostic from the actual RPC boundary.");
        Require(Encoding.UTF8.GetByteCount(lines[0]+Environment.NewLine)<=32768,"Stop failure diagnostic exceeded its finite output bound.");
        using var parsed=JsonDocument.Parse(lines[0][Prefix.Length..]);
        var report=parsed.RootElement;
        Require(report.GetProperty("SchemaVersion").GetInt32()==1 && report.GetProperty("Nonce").GetString()==nonce,"Diagnostic version/nonce mismatch.");
        var actual=JsonSerializer.Deserialize<GmSessionRunIdentity>(report.GetProperty("OriginalOwnerIdentity").GetRawText());
        Require(actual==identity,"Diagnostic redirected the original owner identity.");
        Require(report.GetProperty("FirstFailure").GetProperty("ExceptionType").GetString()==firstType,"Diagnostic replaced the first Stop failure.");
        Require(report.GetProperty("CurrentFailure").GetProperty("FirstFailedPredicate").GetString()==currentPredicate,"Diagnostic did not identify the first actually failed predicate.");
        Require(report.GetProperty("CaptureComplete").GetBoolean(),"Diagnostic capture incomplete.");
        Require(report.GetProperty("DistinctFailureCount").GetInt32()==(firstType=="System.TimeoutException"?2:1),"Failure report duplicated or discarded actual exception references.");
        return report.Clone();
    }

    internal static void CaptureFailureCannotReplaceRefusal(Exception actualRefusal)
    {
        var method=typeof(GmSessionRunCoordinator).GetMethod("TryFreezeStopFailureText",BindingFlags.Static|BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Bounded failure text capture missing.");
        var originalStack=actualRefusal.StackTrace;var first=actualRefusal.Data[FirstExceptionKey];
        Exception[] controls=[new GetterFailure(true),new GetterFailure(false),new LargeTextFailure(true),new LargeTextFailure(false)];
        foreach(var hostile in controls) {
            var capture=method.Invoke(null,[hostile])!;var type=capture.GetType();
            Require(!(bool)type.GetProperty("CaptureComplete")!.GetValue(capture)! &&
                type.GetProperty("CaptureFailure")!.GetValue(capture)!=null &&
                ReferenceEquals(type.GetProperty("Exception")!.GetValue(capture),hostile),"Virtual getter capture fault was not contained honestly.");
        }
        Require(actualRefusal.StackTrace==originalStack && ReferenceEquals(first,actualRefusal.Data[FirstExceptionKey]),"Text capture fault replaced actual Stop provenance.");
    }

    internal static void PinTimeoutState(JsonElement report,GmSessionRunIdentity identity,TerminalIdentity terminal)
    {
        var first=report.GetProperty("FirstFailure");var current=report.GetProperty("CurrentFailure");
        Require(first.GetProperty("Stage").GetString()=="pin_drain_timeout","First cause lost its actual BeginStop pin-drain boundary.");
        // StackText is the exception's immutable text at capture, possibly null before async rethrow.
        Require(first.GetProperty("StackText").ValueKind is JsonValueKind.String or JsonValueKind.Null,"First-cause stack absence was hidden.");
        foreach(var entry in new[]{first,current}) {
            var state=entry.GetProperty("State");
            Require(state.GetProperty("Pins").GetInt32()==1 && !state.GetProperty("PinDrainCompleted").GetBoolean() &&
                !state.GetProperty("LifecyclePresent").GetBoolean() && !state.GetProperty("LifecycleActive").GetBoolean() &&
                state.GetProperty("OwnerClosed").GetBoolean() && !state.GetProperty("OwnerRetired").GetBoolean() &&
                !state.GetProperty("MetadataDebt").GetBoolean(),"Report changed actual held pin/drain/lifecycle/owner state.");
            var record=JsonSerializer.Deserialize<GmSessionRunRecord>(state.GetProperty("InMemoryRecord").GetRawText());
            Require(record?.Identity==identity && record.Disposition==GmSessionRunDisposition.Stopping,"Failure snapshot changed original identity or Stopping record.");
        }
        Require(!first.GetProperty("State").GetProperty("OwnerUncertain").GetBoolean() &&
            current.GetProperty("State").GetProperty("OwnerUncertain").GetBoolean(),"Report lost before/after NotifyUncertain state.");
        var settled=current.GetProperty("State");
        Require(settled.GetProperty("InputDrainCompleted").GetBoolean() && settled.GetProperty("RegisteredLoopStatuses").GetArrayLength()==6 &&
            settled.GetProperty("RegisteredLoopStatuses").EnumerateArray().All(s=>s.GetString()=="RanToCompletion") &&
            settled.GetProperty("RootExitStatus").GetString()=="RanToCompletion" && settled.GetProperty("AuthorityLostStatus").GetString()=="WaitingForActivation", "Report misrepresented actual managed settlement.");
        var proof=JsonSerializer.Deserialize<TerminalStopEvidence>(settled.GetProperty("NativeProof").GetRawText());
        Require(proof?.Identity==terminal && proof.State==GmWorkerStopState.StoppedWithinScope && proof.CleanupComplete && !proof.AuthorityRetained,"Report changed original native proof identity/outcome.");
    }

    internal static void NonPrimaryRefusalState(JsonElement report,GmSessionRunIdentity identity)
    {
        var state=report.GetProperty("CurrentFailure").GetProperty("State");
        var record=JsonSerializer.Deserialize<GmSessionRunRecord>(state.GetProperty("InMemoryRecord").GetRawText());
        Require(record?.Identity==identity && record.Disposition==GmSessionRunDisposition.Stopping &&
            state.GetProperty("LifecycleActive").GetBoolean() && !state.GetProperty("InputDrainCompleted").GetBoolean() &&
            !state.GetProperty("OwnerUncertain").GetBoolean() && state.GetProperty("RootExitStatus").GetString()!="RanToCompletion",
            "Non-primary refusal lost successful BeginStop, active lifecycle, absent input drain or live original root.");
    }

    internal static void SameExceptionProvenance(Exception current, Exception expectedFirst)
    {
        Require(ReferenceEquals(current.Data[FirstExceptionKey],expectedFirst),"First Stop exception reference was lost or replaced.");
        Require(ReferenceEquals(current.Data[CurrentExceptionKey],current),"Current Stop exception reference was replaced.");
    }

    internal static JsonElement ReportObservedRefusal(Type hostType,Exception refusal,string nonce,
        GmSessionRunIdentity identity,string firstType,string predicate)
    {
        var method=hostType.GetMethod("ReportStopFailure",BindingFlags.Static|BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Failure-only RPC reporter missing.");
        using var sink=new StringWriter();var original=Console.Error;
        try {Console.SetError(sink);method.Invoke(null,[refusal]);}
        finally {Console.SetError(original);}
        return ReadReport(sink.ToString(),nonce,identity,firstType,predicate);
    }

    internal static void ExportFailureCannotReplaceRefusal(Type hostType, Exception current)
    {
        var report=hostType.GetMethod("ReportStopFailure",BindingFlags.Static|BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Failure-only RPC reporter missing.");
        var originalError=Console.Error;var stack=current.StackTrace;var message=current.Message;
        var first=current.Data[FirstExceptionKey];var own=current.Data[CurrentExceptionKey];
        var sink=new RejectingDiagnosticWriter();
        try
        {
            Console.SetError(sink);
            report.Invoke(null,[current]);
        }
        finally {Console.SetError(originalError);}
        Require(sink.Attempts>0,"Export failure control never reached the actual reporter.");
        Require(current.StackTrace==stack && current.Message==message && ReferenceEquals(first,current.Data[FirstExceptionKey]) &&
            ReferenceEquals(own,current.Data[CurrentExceptionKey]),"Diagnostic sink failure changed original exception provenance.");
    }

    internal static void DisabledOrMismatchedNonceCannotExport(Type hostType,Exception current)
    {
        var report=hostType.GetMethod("ReportStopFailure",BindingFlags.Static|BindingFlags.NonPublic)!;
        var nonce=Environment.GetEnvironmentVariable(NonceVariable);var first=current.Data[FirstExceptionKey];
        var originalError=Console.Error;
        try {
            foreach(var invalid in new[]{null,"invalid",Guid.NewGuid().ToString("N")}) {
                Environment.SetEnvironmentVariable(NonceVariable,invalid);
                using var sink=new StringWriter();Console.SetError(sink);report.Invoke(null,[current]);
                Require(sink.ToString().Length==0,"Disabled/invalid/mismatched nonce exported owner-bound failure.");
                Require(ReferenceEquals(first,current.Data[FirstExceptionKey]),"Nonce mismatch replaced original first cause.");
            }
        } finally {Console.SetError(originalError);Environment.SetEnvironmentVariable(NonceVariable,nonce);}
    }

    internal static void Require(bool condition,string message)
    {if(!condition)throw new InvalidOperationException(message);}
    private sealed class RejectingDiagnosticWriter : StringWriter
    {
        internal int Attempts;
        public override void WriteLine(string? value){Attempts++;throw new IOException("controlled diagnostic sink failure");}
    }
    private sealed class GetterFailure(bool message) : IOException("controlled diagnostic text failure")
    {
        public override string Message=>message ? throw new IOException("controlled Message getter failure") : base.Message;
        public override string? StackTrace=>!message ? throw new IOException("controlled StackTrace getter failure") : base.StackTrace;
    }
    private sealed class LargeTextFailure(bool message) : IOException("controlled diagnostic text overflow")
    {
        public override string Message=>message?new string('x',2049):base.Message;
        public override string? StackTrace=>!message?new string('x',4097):base.StackTrace;
    }
}
