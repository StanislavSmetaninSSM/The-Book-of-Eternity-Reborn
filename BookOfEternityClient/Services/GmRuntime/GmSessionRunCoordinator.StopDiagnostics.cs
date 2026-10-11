using System.Text.Json.Serialization;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services.GmRuntime;

// Failure-only DEBUG provenance. Never admission, settlement, retry or recovery authority.
internal sealed partial class GmSessionRunCoordinator
{
    internal const string StopDiagnosticNonceVariable="BOE_MAIN_STOP_DIAGNOSTIC_NONCE";
    private const string StopContextKey="BOE.MainStop.Context";
    private const string FirstExceptionKey="BOE.MainStop.FirstException";
    private const string CurrentExceptionKey="BOE.MainStop.CurrentException";
#if DEBUG
    private readonly string? _stopDiagnosticNonce=ReadStopDiagnosticNonce();
    private GmSessionRunIdentity? _stopDiagnosticIdentity;
    private bool _firstStopFailureReserved;
    private Exception? _firstStopException;
    private StopFailure? _firstStopFailure;
#endif
    internal sealed record FrozenStopText([property:JsonIgnore] Exception Exception,string ExceptionType,
        string? Message,string? StackText,bool CaptureComplete,string? CaptureFailure);
    internal sealed record StopFailure(string Stage,string? FirstFailedPredicate,
        IReadOnlyDictionary<string,bool?>? EvaluatedPredicates,string ExceptionType,string? Message,
        string? StackText,StopFailureState? State,bool CaptureComplete,string? CaptureFailure);
    internal sealed record StopFailureState(int Pins,bool PinDrainCompleted,bool LifecyclePresent,
        bool LifecycleActive,bool OwnerUncertain,bool OwnerRetired,bool OwnerClosed,bool MetadataDebt,
        GmSessionRunRecord? InMemoryRecord,bool InputDrainCompleted,string[] RegisteredLoopStatuses,
        string? RootExitStatus,string? AuthorityLostStatus,TerminalStopEvidence? NativeProof,
        string SnapshotConsistency="non-atomic memory observations; native proof only from already-completed original Bridge stop task");
    internal sealed record StopFailureReport(int SchemaVersion,string Nonce,GmSessionRunIdentity OriginalOwnerIdentity,
        StopFailure? FirstFailure,StopFailure CurrentFailure,int DistinctFailureCount,bool CaptureComplete,
        [property:JsonIgnore] GmSessionRunCoordinator Owner,[property:JsonIgnore] Exception FirstException,
        [property:JsonIgnore] Exception CurrentException);

    private static string? ReadStopDiagnosticNonce()
    {
#if DEBUG
        try {var n=Environment.GetEnvironmentVariable(StopDiagnosticNonceVariable);return Guid.TryParseExact(n,"N",out _)?n:null;}
        catch { }
#endif
        return null;
    }
    private void BindStopDiagnosticIdentity(GmSessionRunIdentity identity)
    {
#if DEBUG
        if(_stopDiagnosticNonce!=null)lock(_sync)_stopDiagnosticIdentity??=identity;
#endif
    }
    private static string? BoundStopText(string? value,int limit)=>value?.Length>limit?value[..limit]:value;
    private static FrozenStopText TryFreezeStopFailureText(Exception failure)
    {
        try {return new(failure,failure.GetType().FullName!,BoundStopText(failure.Message,2048),
            BoundStopText(failure.StackTrace,4096),true,null);}
        catch(Exception captureFailure) {return new(failure,failure.GetType().FullName!,null,null,false,captureFailure.GetType().FullName);}
    }
    private IOException StopInvalid(string predicate,Task? inputDrain=null,TerminalStopEvidence? validatedProof=null,int firstGate=-1)
    {
        var failure=GmSessionRunPersistence.Invalid();
        RecordStopFailure(failure,"confirm_settled_stop",predicate,inputDrain,validatedProof,firstGate);
        return failure;
    }
    internal void RecordStopFailure(Exception failure,string stage,string? predicate=null,Task? inputDrain=null,
        TerminalStopEvidence? actualBridgeProof=null,int firstGate=-1)
    {
#if DEBUG
        try
        {
            var nonce=_stopDiagnosticNonce;
            if(nonce==null || Environment.GetEnvironmentVariable(StopDiagnosticNonceVariable)!=nonce)return;
            bool first;GmSessionRunIdentity identity;
            lock(_sync)
            {
                if(_stopDiagnosticIdentity==null || _record?.Identity!=_stopDiagnosticIdentity)return;
                identity=_stopDiagnosticIdentity;
                first=!_firstStopFailureReserved;
                if(first){_firstStopFailureReserved=true;_firstStopException=failure;}
            }
            // Virtual exception getters are outside every owner/Bridge lock; failures reserve first provenance.
            var text=TryFreezeStopFailureText(failure);
            var prior=failure.Data[StopContextKey] as StopFailureReport;
            var previous=prior is { } && ReferenceEquals(prior.Owner,this) && prior.Nonce==nonce && prior.OriginalOwnerIdentity==identity
                ?prior.CurrentFailure:null;
            var state=CaptureStopFailureState(inputDrain,actualBridgeProof);
            IReadOnlyDictionary<string,bool?>? evaluations=previous?.EvaluatedPredicates;
            if(firstGate>=0) {
                string[] names=["originalTerminalRef","bindingPresent","bindingSessionRef","inputDrainCompleted","registeredLoopsCompleted","stopLifecycleActive"];
                evaluations=names.Select((name,index)=>(name,value:index<firstGate?(bool?)true:index==firstGate?(bool?)false:null))
                    .ToDictionary(item=>item.name,item=>item.value);
            }
            var current=new StopFailure(previous?.Stage??stage,previous?.FirstFailedPredicate??predicate,evaluations,
                text.ExceptionType,text.Message,text.StackText,state,text.CaptureComplete,text.CaptureFailure);
            StopFailure? original;Exception firstException;
            lock(_sync)
            {
                if(first)_firstStopFailure=current;
                original=_firstStopFailure;firstException=_firstStopException!;
            }
            var report=new StopFailureReport(1,nonce,identity,original,current,ReferenceEquals(firstException,failure)?1:2,
                original?.CaptureComplete==true && current.CaptureComplete,this,firstException,failure);
            failure.Data[FirstExceptionKey]=firstException;failure.Data[CurrentExceptionKey]=failure;
            failure.Data[StopContextKey]=report;
        }
        catch { /* Diagnostics must not replace the actual Stop failure or its first reservation. */ }
#endif
    }
#if DEBUG
    private StopFailureState CaptureStopFailureState(Task? inputDrain,TerminalStopEvidence? actualBridgeProof)
    {
        int pins;bool pinDrain,lifecyclePresent,lifecycleActive,uncertain,retired,closed,debt;
        GmSessionRunRecord? record;IOwnedTerminalSession? original;Task[] loops;
        lock(_sync) {
            pins=_pins;pinDrain=_drained.Task.IsCompleted;lifecyclePresent=_stopLifecycle!=null;lifecycleActive=_stopLifecycle?.IsActive==true;
            uncertain=_uncertain;retired=_retired;closed=_closed;debt=_persistence.HasDebt;
            record=_record;original=_terminal;loops=_retirement?.Loops??[];
        }
        // Trusted original adapter/tasks only, not caller-supplied original/proof or an InputDrain callback.
        return new(pins,pinDrain,lifecyclePresent,lifecycleActive,uncertain,retired,closed,debt,record,
            inputDrain?.IsCompleted==true,loops.Select(task=>task.Status.ToString()).ToArray(),
            original?.RootExited.Status.ToString(),original?.AuthorityLost.Status.ToString(),actualBridgeProof);
    }
#endif
    internal static StopFailureReport? GetStopFailureReport(Exception failure)
    {
#if DEBUG
        try {
            if(failure.Data[StopContextKey] is not StopFailureReport r || !ReferenceEquals(r.CurrentException,failure) ||
                r.Owner._stopDiagnosticNonce!=r.Nonce || r.Owner._stopDiagnosticIdentity!=r.OriginalOwnerIdentity ||
                Environment.GetEnvironmentVariable(StopDiagnosticNonceVariable)!=r.Nonce)return null;
            // Freeze the current propagated exception text outside locks at the actual export boundary.
            var text=TryFreezeStopFailureText(failure);
            return r with { CurrentFailure=r.CurrentFailure with { Message=text.Message,StackText=text.StackText,
                CaptureComplete=r.CurrentFailure.CaptureComplete && text.CaptureComplete,CaptureFailure=text.CaptureFailure??r.CurrentFailure.CaptureFailure },
                CaptureComplete=r.CaptureComplete && text.CaptureComplete };
        } catch { }
#endif
        return null;
    }
}
