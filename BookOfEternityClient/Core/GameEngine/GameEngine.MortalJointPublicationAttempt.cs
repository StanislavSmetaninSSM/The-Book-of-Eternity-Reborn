using System.Text.Json.Nodes;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    private static readonly object JointAttemptIssuer = new();
    private MortalJointPublicationAttempt? _mortalJointPublicationAttempt;

    internal enum JointAttemptStage { Preparation, Prepared, Effects, Final, Common, Preflight }

    // AR can name and inspect the actual owner, but cannot construct or drive it.
    internal sealed class MortalJointPublicationAttempt
    {
        private readonly GameEngine _engine;
        private readonly FileSystemManager _files;
        private readonly FileSystemManager.CanonicalWriteLease _lease;
        private readonly string _generation;
        private readonly object _frame;
        private readonly MortalJointRepairCheckpoint.RejectedCapture _capture;
        private readonly Dictionary<JointAttemptStage, object> _stages = new();
        private object? _continuation, _reservation;
        private bool _capturing = true, _expired, _delegated;
        private string _phase = "Capturing";
        private JsonObject _binding, _custody;
        private AcceptedTurnAuthorityRegistry.JointUnattemptedCloseResult? _close;
        private PendingTurnSnapshotReadAuthority? _hotOriginal;
        private PendingTurnSnapshotReader.OriginalReadEvidence? _hotEvidence;
        private MortalJointNarrativeInvocation? _narrative;
        private readonly MortalJointNarrativeInvocation? _narrativeCandidate;
        private string _narrativeDisposition = "UnavailableNotConsumed";
        internal MortalJointPublicationAttempt(object issuer, GameEngine engine, FileSystemManager files,
            FileSystemManager.CanonicalWriteLease lease, object actualFrame,
            MortalJointRepairCheckpoint.RejectedCapture actualCapture, JsonObject binding, JsonObject custody)
        {
            RequireIssuer(issuer);
            _engine = engine; _files = files; _lease = lease; _frame = actualFrame; _capture = actualCapture;
            _narrativeCandidate = engine._mortalJointNarrativeInvocation;
            _generation = files.ReadLocalGenerationSnapshot(lease).Binding.Id
                ?? throw new InvalidDataException("Joint attempt requires its existing physical generation.");
            _binding = binding.DeepClone().AsObject(); _custody = custody.DeepClone().AsObject();
        }
        private static void RequireIssuer(object issuer)
        {
            if (!ReferenceEquals(issuer, JointAttemptIssuer)) throw new InvalidOperationException("Joint attempt requires its actual engine issuer.");
        }
        internal bool Delegated => _delegated;
        internal bool Live => !_expired;
        internal bool CloseRecorded => _close is not null;
        internal object? Continuation => _continuation;
        internal object? Reservation => _reservation;
        internal string Generation => _generation;
        internal bool IsCurrentOwner(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease) =>
            !_expired && ReferenceEquals(files, _files) && ReferenceEquals(lease, _lease) &&
            ReferenceEquals(_engine._mortalJointPublicationAttempt, this) && lease.IsActive;
        internal void RequireActualOwner(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
        {
            if (!IsCurrentOwner(files, lease) || !files.IsCurrentSessionGeneration(lease, _generation))
                throw new InvalidDataException("The current joint attempt owner is unavailable.");
            _capture.EnsureCurrent(files, lease);
            if (_frame is not ValidatedPendingTurnSnapshotContext frame ||
                !_engine.MatchesMortalJointRepairFrame(frame, frame.TurnNumber))
                throw new InvalidDataException("Joint attempt lost its actual private admitted frame.");
        }
        internal void RetainHotSource(object issuer, PendingTurnSnapshotReadAuthority original,
            PendingTurnSnapshotReader.OriginalReadEvidence evidence)
        {
            RequireIssuer(issuer); RequireActualOwner(_files, _lease);
            if (!_capturing || _hotOriginal is not null) throw new InvalidOperationException("HOT source requires the actual open capture.");
            evidence.RequireCurrent(_files, _lease, original);
            _hotOriginal = original; _hotEvidence = evidence;
        }
        internal void RequireMechanicalOriginalAgreement(FileSystemManager files,
            FileSystemManager.CanonicalWriteLease lease, PendingTurnSnapshotReadAuthority mechanical)
        {
            RequireActualOwner(files, lease);
            if (_hotOriginal is not { } hot || _hotEvidence is null) return;
            _hotEvidence.RequireCurrent(files, lease, hot);
            if (hot.SessionId != mechanical.SessionId || hot.RequestId != mechanical.RequestId ||
                hot.SnapshotToken != mechanical.SnapshotToken || hot.TurnNumber != mechanical.TurnNumber || hot.Realm != mechanical.Realm)
                throw new InvalidDataException("The two genuine original operations belong to different sources.");
            foreach (var path in hot.CoveredLogicalPaths.Intersect(mechanical.CoveredLogicalPaths, StringComparer.Ordinal))
                if (!hot.ReadRequiredBytes(path).AsSpan().SequenceEqual(mechanical.ReadRequiredBytes(path)))
                    throw new InvalidDataException("Overlapping HOT/mechanical original source changed: " + path);
        }
        internal void CloseCapture(object issuer, JsonObject custody)
        {
            RequireIssuer(issuer); RequireActualOwner(_files, _lease);
            if (!_capturing || _phase != "Capturing") throw new InvalidOperationException("Joint capture cannot reopen.");
            _custody = custody.DeepClone().AsObject(); _capturing = false; _phase = "PreparationStarted";
        }
        internal void RetainNarrativeContribution(object issuer, MortalJointNarrativeInvocation invocation)
        {
            RequireIssuer(issuer); RequireActualOwner(_files, _lease);
            if (!_capturing || _narrative is not null)
                throw new InvalidOperationException("N contribution requires the actual open capture once.");
            _narrative = invocation; _narrativeDisposition = "MatchedLiveN";
        }
        internal void RetainHistoricalNarrativeUnavailable(object issuer)
        {
            RequireIssuer(issuer); RequireActualOwner(_files, _lease);
            if (!_capturing || _narrative is not null)
                throw new InvalidOperationException("Historical N unavailability requires the actual resume capture.");
            _narrativeDisposition = "HistoricalNUnavailable";
        }
        internal void RetainStage(object issuer, JointAttemptStage stage, object actualResult)
        {
            RequireIssuer(issuer); RequireActualOwner(_files, _lease);
            if (_capturing || _delegated || _close is not null || (int)stage != _stages.Count || !_stages.TryAdd(stage, actualResult))
                throw new InvalidOperationException("Joint stage must be retained once before delegation.");
            _phase = stage is JointAttemptStage.Common or JointAttemptStage.Preflight ? "V2CommonSealed" : "Building";
        }
        internal void RetainContinuation(object issuer, object continuation)
        {
            RequireIssuer(issuer); RequireActualOwner(_files, _lease);
            if (_capturing || _continuation is not null || _delegated) throw new InvalidOperationException("Joint continuation can be retained once.");
            _continuation = continuation;
        }
        internal void RetainReservation(object issuer, object reservation)
        {
            RequireIssuer(issuer); RequireActualOwner(_files, _lease);
            if (_continuation is null || _reservation is not null || _delegated) throw new InvalidOperationException("Joint reservation can be retained once.");
            _reservation = reservation; _phase = "Reserved";
        }
        internal void MarkDelegated(object issuer)
        {
            RequireIssuer(issuer); RequireActualOwner(_files, _lease);
            if (_capturing || _delegated || _close is not null || !_stages.ContainsKey(JointAttemptStage.Preflight))
                throw new InvalidOperationException("Only the complete actual preflight can delegate this attempt.");
            _delegated = true; _phase = "Delegated";
        }
        internal void RecordClose(object issuer, AcceptedTurnAuthorityRegistry.JointUnattemptedCloseResult close)
        {
            RequireIssuer(issuer);
            if (_delegated || _close is not null) throw new InvalidOperationException("Attempt close cannot replace transaction ownership.");
            _capturing = false; _close = close; _phase = close.Disposition;
        }
        internal void Expire(object issuer)
        {
            RequireIssuer(issuer); _expired = true; _capturing = false;
            _hotOriginal = null; _hotEvidence = null;
            if ((_narrative ?? _narrativeCandidate) is { } narrative)
            {
                if (ReferenceEquals(_engine._mortalJointNarrativeInvocation, narrative))
                    _engine.ExpireCurrentMortalJointNarrativeInvocation();
                else narrative.Dispose();
                _narrative = null;
            }
        }
        internal JsonObject ExportDiagnostic() => new()
        {
            ["phase"] = _phase, ["closeDisposition"] = _close?.Disposition ?? (_delegated ? "UnavailableDelegated" : "UnavailableNotReserved"),
            ["delegated"] = _delegated, ["captureCanOpen"] = _capturing && !_expired,
            ["liveCapabilitiesExpired"] = _expired, ["fullSemanticAdmission"] = false,
            ["hasS"] = false, ["hasB"] = false, ["hasM3"] = false,
            ["narrativeContribution"] = _narrativeDisposition,
            ["binding"] = _binding.DeepClone(), ["custody"] = _custody.DeepClone()
        };
    }

    private MortalJointPublicationAttempt BeginCurrentJointAttempt(FileSystemManager.CanonicalWriteLease lease,
        ValidatedPendingTurnSnapshotContext frame, MortalJointRepairCheckpoint.RejectedCapture capture)
    {
        if (_mortalJointPublicationAttempt is { } previous && previous.Live)
            throw new InvalidOperationException("A live joint attempt already owns this engine.");
        capture.EnsureCurrent(_fs, lease);
        var attempt = new MortalJointPublicationAttempt(JointAttemptIssuer, this, _fs, lease, frame, capture,
            HotBindingDiagnostic(_hotOrdinaryOriginal), HotCustodyDiagnostic(null));
        _mortalJointPublicationAttempt = attempt;
        try
        {
            attempt.RequireActualOwner(_fs, lease);
            return attempt;
        }
        catch
        {
            // The caller cannot receive this owner when Begin fails. Expire
            // exactly this unreserved registration without touching any debt.
            attempt.Expire(JointAttemptIssuer);
            throw;
        }
    }

    private async Task CaptureCurrentHotCustodyAsync(MortalJointPublicationAttempt attempt,
        FileSystemManager.CanonicalWriteLease lease, ValidatedPendingTurnSnapshotContext frame)
    {
        await ConsumeCurrentMortalJointInterfaceAsync(lease, attempt);
        await RequireCurrentMortalJointNarrativeAsync(lease, frame, attempt, beforeInterfacePreparation: false);
        var read = _hotRepairReady;
        if (read is { Faulted: false, Status: "ConsumedReadyMatched", ReadyDeleteReturned: true, ReadyReleaseReturned: true })
        {
            try
            {
                await RecheckHotReadySourceAsync(lease, read, requireReadyAbsent: true);
                read.CurrentSourceRecheckSucceeded = true;
                attempt.RetainHotSource(JointAttemptIssuer, read.Source!.Snapshot, read.Source.Evidence);
            }
            catch (Exception error)
            {
                read.Fail(IsOptionalHotFailure(error) ? "SourceChanged" : "OwnerFailure");
                if (!IsOptionalHotFailure(error)) throw;
            }
        }
        attempt.CloseCapture(JointAttemptIssuer, HotCustodyDiagnostic(read));
    }

    private void CloseCurrentJointAttempt(MortalJointPublicationAttempt attempt,
        FileSystemManager.CanonicalWriteLease lease)
    {
        if (!attempt.Delegated && !attempt.CloseRecorded)
            attempt.RecordClose(JointAttemptIssuer, AcceptedTurnAuthorityRegistry.CloseOwnedJointAttemptUnattempted(_fs, lease, attempt));
    }

    private JsonObject ExportMortalJointPublicationAttemptDiagnostic() =>
        _mortalJointPublicationAttempt?.ExportDiagnostic() ?? new JsonObject { ["closeDisposition"] = "UnavailableNotReserved" };
}
