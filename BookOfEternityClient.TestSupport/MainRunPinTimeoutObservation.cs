using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.Services.GmWorkers;

namespace BookOfEternityClient.Tests;

// Two finite, non-atomic observations for the existing held-pin timeout case only.
// Reflection is resolved before the attempt; handlers read memory, never validate authority.
internal sealed class MainRunPinTimeoutObservation
{
    [ThreadStatic] private static bool _insideHandler;
    private readonly GmSessionRunCoordinator _owner;
    private readonly object _host, _input, _binding, _guard;
    private readonly IOwnedTerminalSession _terminal;
    private readonly GmSessionRunIdentity _expectedOwnerIdentity;
    private readonly TerminalIdentity _expectedTerminalIdentity;
    private readonly Dictionary<string, FieldInfo> _ownerFields, _hostFields, _inputFields;
    private readonly FieldInfo _guardStream;
    private readonly PropertyInfo _bindingSession, _bindingLoops;
    private readonly Type _beginStopStateMachine, _confirmStateMachine;
    private int _active;
    private Exception? _firstTimeoutException, _firstConfirmException;
    private Capture? _firstTimeout, _firstConfirm;
    private string? _captureFailure;

    internal sealed record Snapshot(int Pins, string PinDrainStatus, bool PinDrainCompleted, bool LifecyclePresent,
        bool LifecycleActive, bool OwnerUncertain, bool OwnerRetired, bool OwnerClosed,
        bool GuardStreamPresent, bool OriginalTerminalMatches, bool BindingPresent,
        bool BindingSessionMatches, string[] RegisteredLoopStatuses, bool RegisteredLoopsCompleted, bool InputRevoked,
        string? InputDrainStatus, bool InputDrainCompleted, string? NativeStopTaskStatus,
        TerminalStopEvidence? NativeProof, string? NativeDisposeTaskStatus,
        TerminalIdentity ActualTerminalIdentity, string RootExitStatus, string AuthorityLostStatus, bool BridgeUncertain,
        GmSessionRunRecord? InMemoryRecord, bool MetadataDebt);
    private sealed record Capture(string ExceptionType, string Message, bool AdmissionMarker,
        string? ExceptionStackText, string ThrowStackText, Snapshot State);

    internal MainRunPinTimeoutObservation(GmSessionRunCoordinator owner, object host, IOwnedTerminalSession terminal)
    {
        _owner=owner; _host=host; _terminal=terminal;
        _expectedOwnerIdentity=owner.Identity; _expectedTerminalIdentity=terminal.Identity;
        _ownerFields=Fields(owner.GetType(), "_pins", "_drained", "_stopLifecycle", "_uncertain", "_retired", "_closed", "_terminal", "_retirement", "_guard");
        _hostFields=Fields(host.GetType(), "_inputLifetime", "_terminalStopTask", "_terminalDisposeTask", "_terminalUncertain");
        _input=_hostFields["_inputLifetime"].GetValue(host) ?? throw new InvalidOperationException("Original input missing before pin-timeout attempt.");
        _inputFields=Fields(_input.GetType(), "Revoked", "DrainTask");
        _binding=_ownerFields["_retirement"].GetValue(owner) ?? throw new InvalidOperationException("Original retirement binding missing.");
        _bindingSession=_binding.GetType().GetProperty("Session")!;
        _bindingLoops=_binding.GetType().GetProperty("Loops")!;
        _guard=_ownerFields["_guard"].GetValue(owner)!;
        _guardStream=Fields(_guard.GetType(), "_stream")["_stream"];
        _beginStopStateMachine=StateMachine(nameof(GmSessionRunCoordinator.BeginStopAsync));
        _confirmStateMachine=StateMachine(nameof(GmSessionRunCoordinator.ConfirmSettledStopAsync));
    }

    private static Dictionary<string, FieldInfo> Fields(Type type, params string[] names)=>names.ToDictionary(
        name=>name, name=>type.GetField(name, BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Observation field missing: {type.FullName}.{name}"));
    private static Type StateMachine(string method)=>typeof(GmSessionRunCoordinator)
        .GetMethod(method, BindingFlags.Instance|BindingFlags.NonPublic)!
        .GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType;

    internal void Start()
    {
        Volatile.Write(ref _active, 1);
        AppDomain.CurrentDomain.FirstChanceException+=Observe;
    }
    internal void Stop()
    {
        Volatile.Write(ref _active, 0);
        AppDomain.CurrentDomain.FirstChanceException-=Observe;
    }

    private void Observe(object? sender, FirstChanceExceptionEventArgs args)
    {
        if(Volatile.Read(ref _active)==0 || _insideHandler)return;
        _insideHandler=true;
        try
        {
            var failure=args.Exception;
            if(failure is not (TimeoutException or IOException))return;
            var stack=new StackTrace();
            var types=stack.GetFrames().Select(frame=>frame.GetMethod()?.DeclaringType).ToArray();
            if(failure is TimeoutException && types.Contains(_beginStopStateMachine) &&
                Interlocked.CompareExchange(ref _firstTimeoutException, failure, null)==null)
                _firstTimeout=CaptureFailure(failure, stack);
            else if(GmSessionRunPersistence.IsAdmissionRefusal(failure) && types.Contains(_confirmStateMachine) &&
                Interlocked.CompareExchange(ref _firstConfirmException, failure, null)==null)
                _firstConfirm=CaptureFailure(failure, stack);
        }
        catch(Exception failure)
        {
            // A recursively raised observer exception is ignored by the guard, but invalidates evidence.
            _captureFailure=failure.GetType().FullName;
        }
        finally {_insideHandler=false;}
    }

    private Capture CaptureFailure(Exception failure, StackTrace stack)=>new(failure.GetType().FullName!,
        failure.Message, GmSessionRunPersistence.IsAdmissionRefusal(failure), failure.StackTrace, stack.ToString(), ReadSnapshot());

    private Snapshot ReadSnapshot()
    {
        object? Owner(string field)=>_ownerFields[field].GetValue(_owner);
        object? Host(string field)=>_hostFields[field].GetValue(_host);
        var lifecycle=(FileSystemManager.SessionLifecycleLease?)Owner("_stopLifecycle");
        var inputDrain=(Task?)_inputFields["DrainTask"].GetValue(_input);
        var stop=(Task<TerminalStopEvidence>?)Host("_terminalStopTask");
        var binding=Owner("_retirement");
        var loops=(Task[])_bindingLoops.GetValue(_binding)!;
        var pinDrain=((TaskCompletionSource)Owner("_drained")!).Task;
        return new((int)Owner("_pins")!, pinDrain.Status.ToString(), pinDrain.IsCompleted,
            lifecycle!=null, lifecycle?.IsActive==true, (bool)Owner("_uncertain")!, (bool)Owner("_retired")!,
            (bool)Owner("_closed")!, _guardStream.GetValue(_guard)!=null, ReferenceEquals(Owner("_terminal"), _terminal),
            binding!=null, ReferenceEquals(binding,_binding) && ReferenceEquals(_bindingSession.GetValue(_binding),_terminal),
            loops.Select(task=>task.Status.ToString()).ToArray(), loops.All(task=>task.IsCompleted),
            (bool)_inputFields["Revoked"].GetValue(_input)!, inputDrain?.Status.ToString(), inputDrain?.IsCompleted==true,
            stop?.Status.ToString(), stop?.IsCompletedSuccessfully==true ? stop.Result : null,
            ((Task?)Host("_terminalDisposeTask"))?.Status.ToString(), _terminal.Identity, _terminal.RootExited.Status.ToString(),
            _terminal.AuthorityLost.Status.ToString(), (bool)Host("_terminalUncertain")!, _owner.Record, _owner.HasMetadataDebt);
    }

    internal (object Evidence, bool Complete) Freeze(Exception? finalFailure, GmSessionRunRecord durableRecord)
    {
        // Caller has unsubscribed. Frozen evidence and export precede held-operation release/cleanup retry.
        var final=ReadSnapshot();
        var timeout=_firstTimeout?.State;
        var refusal=_firstConfirm?.State;
        var complete=_captureFailure==null && timeout!=null && refusal!=null &&
            timeout.Pins>0 && !timeout.PinDrainCompleted && !timeout.LifecyclePresent &&
            _firstTimeoutException is TimeoutException && _firstConfirm!.AdmissionMarker &&
            !ReferenceEquals(_firstTimeoutException, _firstConfirmException) && ReferenceEquals(finalFailure,_firstConfirmException) &&
            refusal.OriginalTerminalMatches && refusal.BindingPresent && refusal.BindingSessionMatches &&
            refusal.InputRevoked && refusal.InputDrainCompleted && refusal.RegisteredLoopsCompleted &&
            refusal.RegisteredLoopStatuses.All(status=>status=="RanToCompletion") &&
            !refusal.LifecyclePresent && !refusal.LifecycleActive && refusal.OwnerUncertain && refusal.Pins>0 &&
            refusal.NativeProof is { State: GmWorkerStopState.StoppedWithinScope, CleanupComplete: true, AuthorityRetained: false } &&
            refusal.NativeDisposeTaskStatus=="RanToCompletion" && final.BridgeUncertain && final.GuardStreamPresent && !final.OwnerRetired &&
            timeout.InMemoryRecord?.Identity==_expectedOwnerIdentity && refusal.InMemoryRecord?.Identity==_expectedOwnerIdentity &&
            final.InMemoryRecord?.Identity==_expectedOwnerIdentity && durableRecord.Identity==_expectedOwnerIdentity &&
            timeout.ActualTerminalIdentity==_expectedTerminalIdentity && refusal.ActualTerminalIdentity==_expectedTerminalIdentity &&
            final.ActualTerminalIdentity==_expectedTerminalIdentity && refusal.NativeProof.Identity==_expectedTerminalIdentity &&
            _expectedTerminalIdentity.RunId==_expectedOwnerIdentity.RunId &&
            durableRecord.Disposition!=GmSessionRunDisposition.Stopped;
        return (new { SnapshotConsistency="non-atomic memory observations; first timeout precedes catch/NotifyUncertain",
            GuardStreamMeaning="presence only; no authority validation or ownership grant",
            ExpectedOriginalOwnerIdentity=_expectedOwnerIdentity, ExpectedOriginalTerminalIdentity=_expectedTerminalIdentity,
            FirstTimeout=_firstTimeout, FirstConfirmRefusal=_firstConfirm, FinalBeforePinRelease=final,
            FinalFailureType=finalFailure?.GetType().FullName, FinalFailureStackText=finalFailure?.StackTrace,
            FinalIsFirstConfirmRefusal=ReferenceEquals(finalFailure,_firstConfirmException), DurableRecordBeforePinRelease=durableRecord,
            CaptureFailure=_captureFailure, MeasurementComplete=complete }, complete);
    }
}
