using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    private static readonly object JointNarrativeIssuer = new();
    private MortalJointNarrativeInvocation? _mortalJointNarrativeInvocation;
    private string _mortalJointNarrativeDisposition = "UnavailableNotRun";
    private (string Fingerprint, string Generation)? _mortalJointNarrativeHold;

    // Only this engine can associate a validator's actual returned DATA with its
    // real invocation. No serialized result or caller tuple constructs this owner.
    internal sealed class MortalJointNarrativeInvocation : IDisposable
    {
        private readonly GameEngine _engine;
        private readonly long _iteration;
        private readonly string _generation, _checkpointFingerprint;
        private readonly byte[]? _generationBytes;
        private ValidatedPendingTurnSnapshotContext? _frame;
        private FileSystemManager.MainAdmission? _main;
        private MortalJointInterfaceReady? _ready;
        private HotOrdinaryOriginal? _stage;
        private ValidationService.NarrativePolicyPass? _pass;
        private bool _expired;

        internal MortalJointNarrativeInvocation(object issuer, GameEngine engine, object actualFrame,
            FileSystemManager.MainAdmission? actualMain, string generation, byte[]? generationBytes)
        {
            RequireIssuer(issuer);
            _engine = engine;
            _frame = actualFrame as ValidatedPendingTurnSnapshotContext
                ?? throw new InvalidOperationException("Narrative contribution requires its actual admitted frame.");
            _main = actualMain; _generation = generation; _generationBytes = generationBytes?.ToArray();
            _iteration = engine._mortalJointInterfaceValidationIteration;
            _checkpointFingerprint = engine._mortalJointRepairCheckpoint!.ToJsonObject()["fingerprint"]!.GetValue<string>();
            _ready = engine._mortalJointInterfaceReady; _stage = engine._hotOrdinaryOriginal;
        }

        private static void RequireIssuer(object issuer)
        {
            if (!ReferenceEquals(issuer, JointNarrativeIssuer))
                throw new InvalidOperationException("Narrative invocation requires its actual engine issuer.");
        }

        internal void Complete(object issuer, ValidationService.NarrativePolicyPass pass)
        {
            RequireIssuer(issuer);
            if (_expired || _pass is not null || !ReferenceEquals(_engine._mortalJointNarrativeInvocation, this))
            {
                pass.Dispose();
                throw new InvalidDataException("joint_narrative_invocation_incomplete_or_replaced");
            }
            pass.RequireIssuer(_engine._validator);
            _pass = pass;
            _engine._mortalJointNarrativeDisposition = "CapturedActualN";
        }

        internal async Task RequireCurrentAsync(object issuer, FileSystemManager files,
            FileSystemManager.CanonicalWriteLease lease, object actualFrame,
            MortalJointInterfacePublication.Record? authenticatedInterfaceTransition)
        {
            RequireIssuer(issuer);
            using var timing = files.MeasureOperation("MortalJointNarrative.RequireCurrent");
            RequireBinding(files, lease, actualFrame);
            var frame = _frame!;
            _pass!.RequireIssuer(_engine._validator);
            _pass.RequireOriginalContext(frame.SessionId, frame.RequestId, frame.Payload.ManifestPayloadHash, frame.TurnNumber);
            if (authenticatedInterfaceTransition is { } result)
            {
                var binding = result.Binding;
                if (!ReferenceEquals(result, _engine._mortalJointInterfaceData) || result.State != "Published" || result.HasErrors ||
                    binding["iterationOrdinal"]!.GetValue<long>() != _iteration ||
                    binding["stageOrdinal"]!.GetValue<long>() != _ready!.StageOrdinal ||
                    binding["readyOrdinal"]!.GetValue<long>() != _ready.ReadyOrdinal ||
                    binding["checkpointFingerprint"]!.GetValue<string>() != _checkpointFingerprint ||
                    !_engine.IsOwnedMortalJointInterfaceReadyTransition(_ready.StageOrdinal, _ready.ReadyOrdinal,
                        result.BeforeBytes, result.AfterBytes))
                    throw new InvalidDataException("joint_narrative_interface_transition_is_not_its_actual_publication");
            }
            await _pass.RequireCurrentAsync(files, lease, authenticatedInterfaceTransition);
            RequireBinding(files, lease, actualFrame);
            _engine._mortalJointNarrativeDisposition = "MatchedLiveN";
        }

        private void RequireBinding(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease, object actualFrame)
        {
            if (_expired || _pass is null || !ReferenceEquals(files, _engine._fs) ||
                !ReferenceEquals(_engine._mortalJointNarrativeInvocation, this) || !ReferenceEquals(_frame, actualFrame) ||
                _iteration != _engine._mortalJointInterfaceValidationIteration || _iteration <= 0)
                throw new InvalidDataException("joint_narrative_actual_invocation_unavailable");
            RequireOrdinaryJointRepairLease(lease);
            files.EnsureCanonicalWriteLeaseActive(lease);
            var main = lease.MainAdmission ?? throw new InvalidDataException("joint_narrative_current_main_missing");
            main.Validate(lease);
            if (_main is not { Closed: false, Acquired: true } || !main.SharesAccess(_main) ||
                _ready is not { Faulted: false, DeleteReturned: true, ReleaseReturned: true } ||
                !ReferenceEquals(_ready, _engine._mortalJointInterfaceReady) ||
                !ReferenceEquals(_stage, _engine._hotOrdinaryOriginal) || !ReferenceEquals(_stage, _ready.Stage) ||
                _ready.Owner.Closed || !_ready.Owner.Acquired || !main.SharesAccess(_ready.Owner) ||
                _stage is not { Preparation: not null, Ordinal: > 0 } || _stage.Generation != _generation ||
                _engine._mortalJointRepairCheckpoint is not { UsesInterfacePublication: true } checkpoint ||
                checkpoint.ToJsonObject()["fingerprint"]!.GetValue<string>() != _checkpointFingerprint ||
                !_engine.MatchesMortalJointRepairFrame(_frame, _frame!.TurnNumber) ||
                !SameMortalJointOriginalFrame(_ready.Frame, _frame, _frame.TurnNumber))
                throw new InvalidDataException("joint_narrative_frame_ready_or_owning_main_changed");
            var generation = files.ReadLocalGenerationSnapshot(lease);
            if (generation.Binding.Id != _generation || _generationBytes is null || !HotBytesEqual(generation.Bytes, _generationBytes))
                throw new InvalidDataException("joint_narrative_generation_changed");
            lease.EnsureNoPendingLocalDecision();
        }

        /// <summary>Expires invocation DATA without disposing any borrowed Main, Ready or frame owner.</summary>
        public void Dispose()
        {
            if (_expired) return;
            _expired = true; _pass?.Dispose(); _pass = null;
            _frame = null; _main = null; _ready = null; _stage = null;
        }
    }

    private void ExpireCurrentMortalJointNarrativeInvocation()
    {
        var invocation = _mortalJointNarrativeInvocation;
        _mortalJointNarrativeInvocation = null;
        invocation?.Dispose();
        // A known refusal deliberately outlives the disposable invocation.
    }

    private void RequireNoKnownMortalJointNarrativeHold(MortalJointRepairCheckpoint checkpoint)
    {
        var binding = checkpoint.ToJsonObject();
        if (_mortalJointNarrativeHold is { } hold &&
            hold.Fingerprint == binding["fingerprint"]!.GetValue<string>() &&
            hold.Generation == binding["sessionGeneration"]!.GetValue<string>())
        {
            _mortalJointNarrativeDisposition = "KnownLiveNHold";
            throw new MortalJointRepairPublicationHoldException();
        }
    }

    private bool IsCurrentMortalJointNarrativeResume(MortalJointRepairCheckpoint checkpoint) =>
        _mortalJointInterfaceResumeCheckpoint == checkpoint.ToJsonObject()["fingerprint"]!.GetValue<string>();

    private async Task RequireCurrentMortalJointNarrativeAsync(FileSystemManager.CanonicalWriteLease lease,
        ValidatedPendingTurnSnapshotContext frame, MortalJointPublicationAttempt? attempt, bool beforeInterfacePreparation)
    {
        if (_mortalJointRepairCheckpoint is not { UsesInterfacePublication: true } checkpoint) return;
        RequireNoKnownMortalJointNarrativeHold(checkpoint);
        if (IsCurrentMortalJointNarrativeResume(checkpoint))
        {
            // This marker is issued only by the existing physically authenticated
            // resume adapter after its new owning release, never by memory absence.
            _mortalJointNarrativeDisposition = "HistoricalNUnavailable";
            attempt?.RetainHistoricalNarrativeUnavailable(JointAttemptIssuer);
            return;
        }
        try
        {
            var invocation = _mortalJointNarrativeInvocation
                ?? throw new InvalidDataException("joint_narrative_fresh_invocation_missing");
            await invocation.RequireCurrentAsync(JointNarrativeIssuer, _fs, lease, frame,
                beforeInterfacePreparation ? null : _mortalJointInterfaceData);
            if (attempt is not null) attempt.RetainNarrativeContribution(JointAttemptIssuer, invocation);
        }
        catch
        {
            var binding = checkpoint.ToJsonObject();
            _mortalJointNarrativeHold = (binding["fingerprint"]!.GetValue<string>(), binding["sessionGeneration"]!.GetValue<string>());
            _mortalJointNarrativeDisposition = "KnownLiveNHold";
            throw;
        }
    }

    private async Task RecheckMortalJointNarrativeBeforePreparationAsync(MortalJointPublicationAttempt attempt,
        FileSystemManager.CanonicalWriteLease lease, ValidatedPendingTurnSnapshotContext frame)
    {
        attempt.RequireActualOwner(_fs, lease);
        // Capture has closed, so do not register or reopen it at this fresh fence.
        await RequireCurrentMortalJointNarrativeAsync(lease, frame, null, beforeInterfacePreparation: false);
    }
}
