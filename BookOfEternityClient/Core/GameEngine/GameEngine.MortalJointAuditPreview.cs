using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    // Detached comparison DATA only. Never grants publication authority.
    internal sealed record MortalJointAuditPreview(string InputFingerprint,
        string PreparedPlanFingerprint, string InventoryFingerprint,
        string SemanticCandidateJson, string NarrativeSourcesJson,
        string ProgressionBasisJson, string ProgressionInputFingerprint);

    private MortalJointAuditPreview? _mortalJointAuditExpected;

    /// <summary>Captures the real candidate and releases every attempt capability before returning DATA.</summary>
    internal async Task<MortalJointAuditPreview> CaptureCurrentMortalJointAuditPreviewAsync()
    {
        using var timing = _fs.MeasureOperation("MortalJointAudit.PreviewAndClose");
        if (_mortalJointPublicationAttempt is { Live: true } ||
            _mortalJointRepairCanonicalRefresh is not null || !_mortalJointRepairCorrectionReady ||
            _mortalJointRepairCheckpoint is not { UsesInterfacePublication: true } checkpoint ||
            IsCurrentMortalJointNarrativeResume(checkpoint) || _mortalJointInterfaceReady is not { } ready)
            throw new InvalidDataException("joint_audit_preview_requires_current_live_unattempted_owner");
        // No caller-provided frame or authority. The original invocation or actual
        // consumed Ready supplies the engine's existing authenticated frame.
        var frame = _mortalJointNarrativeInvocation?.GetActualFrame(JointNarrativeIssuer)
            as ValidatedPendingTurnSnapshotContext ?? ready.Frame;
        MortalJointPublicationAttempt? attempt = null;
        MortalJointAuditPreview? data = null;
        try
        {
            await RefreshCurrentMortalJointAuditNarrativeAsync(frame);
            await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
            {
                RequireOrdinaryJointRepairLease(lease);
                using var sourceOperation = _fs.BeginSourceDataOperationIfNeeded();
                using var recoveryValidation = _fs.BeginRecoveryValidation(lease);
                var intake = await InspectOrdinaryMortalJointRepairAsync(lease, frame, frame.TurnNumber);
                if (intake.Disposition != MortalJointRepairRawDisposition.Owned)
                    throw new MortalJointPreparationRejectedException(intake.Issues);
                await intake.ValidateCurrentAsync(_fs, lease, frame.SessionId, frame.RequestId,
                    frame.Payload.ManifestPayloadHash, frame.TurnNumber);
                var capture = await ReadCurrentJointRejectedCaptureAsync(lease, checkpoint.ToJsonObject());
                attempt = BeginCurrentJointAttempt(lease, frame, capture);
                try
                {
                    var preflight = await BuildCurrentMortalJointPreflightAsync(lease, frame, capture, attempt, _normalizer.BindTo(lease));
                    data = CaptureMortalJointAuditCandidate(preflight);
                    if (_mortalJointAuditExpected is { } expected && data != expected)
                        throw new InvalidDataException("joint_audit_candidate_changed_before_preview_close");
                    await preflight.Preparation.ValidateInventoryCurrentAsync(_fs, lease);
                    await RecheckMortalJointNarrativeBeforePreparationAsync(attempt, lease, frame);
                    await attempt.RecheckPreflightDebugPrefixAsync(JointAttemptIssuer, preflight);
                    // Closing the unattempted reservation performs fresh source reads.
                    // Complete the source operation only after that owner has closed.
                    CloseCurrentJointAttempt(attempt, lease);
                    if (attempt.CloseDisposition != "ClosedNoNativeAttempt" || attempt.Delegated)
                        throw new InvalidDataException("joint_audit_preview_unattempted_close_unavailable: " + attempt.CloseDisposition);
                    if (sourceOperation is not null) await sourceOperation.CompleteAsync();
                    lease.EnsureNoPendingLocalDecision();
                }
                catch
                {
                    CloseCurrentJointAttempt(attempt, lease);
                    throw;
                }
            }
        }
        catch (Exception error) when (error is not SessionReplacedException)
        {
            _mortalJointRepairCanonicalRefresh = new(MortalJointRepairPublicationDisposition.PublicationUncertain,
                null, error is MortalJointPreparationRejectedException rejected ? rejected.Issues : JointRepairAdmissionIssue(error.Message),
                Frame: frame);
            throw;
        }
        finally { attempt?.Expire(JointAttemptIssuer); }
        // Both the real await-using release and expiry have returned successfully.
        _mortalJointAuditExpected = data ?? throw new InvalidDataException("joint_audit_preview_data_missing");
        return _mortalJointAuditExpected;
    }

    private async Task RefreshCurrentMortalJointAuditNarrativeAsync(ValidatedPendingTurnSnapshotContext frame)
    {
        MortalJointNarrativeInvocation invocation;
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            RequireOrdinaryJointRepairLease(lease);
            if (_mortalJointPublicationAttempt is { Live: true } ||
                _mortalJointRepairCheckpoint is not { UsesInterfacePublication: true } checkpoint ||
                IsCurrentMortalJointNarrativeResume(checkpoint) ||
                _mortalJointInterfaceReady is not { Faulted: false, DeleteReturned: true, ReleaseReturned: true } ready ||
                _mortalJointInterfaceData is not { State: "Published", HasErrors: false } published ||
                !SameMortalJointOriginalFrame(ready.Frame, frame, frame.TurnNumber))
                throw new InvalidDataException("joint_audit_fresh_n_requires_current_published_live_owner");
            RequireNoKnownMortalJointNarrativeHold(checkpoint);
            await RequireMortalJointInterfacePendingAsync(lease, published, frame);
            await RecheckMortalJointInterfaceReadyAsync(lease, ready, requireReadyAbsent: true);
            if (_mortalJointNarrativeInvocation is { } previous)
            {
                var previousFrame = (ValidatedPendingTurnSnapshotContext)previous.GetActualFrame(JointNarrativeIssuer);
                await RequireCurrentMortalJointNarrativeAsync(lease, previousFrame, null, beforeInterfacePreparation: false);
            }
            ExpireCurrentMortalJointNarrativeInvocation();
            var generation = _fs.ReadLocalGenerationSnapshot(lease);
            invocation = new(JointNarrativeIssuer, this, frame, lease.MainAdmission?.Parent,
                generation.Binding.Id ?? string.Empty, generation.Bytes);
            _mortalJointNarrativeInvocation = invocation;
            lease.EnsureNoPendingLocalDecision();
        }
        try
        {
            var pass = await _validator.ValidateMortalJointNarrativeReadOnlyPolicyPassAsync();
            invocation.Complete(JointNarrativeIssuer, pass);
            if (pass.Issues.Any(issue => issue.Severity == IssueSeverity.Error))
                throw new MortalJointPreparationRejectedException(pass.Issues);
        }
        catch
        {
            ExpireCurrentMortalJointNarrativeInvocation();
            _mortalJointNarrativeDisposition = "UnavailableAuditReadOnlyInvocationFault";
            throw;
        }
    }

    private void RequireCurrentMortalJointAuditCandidate(CanonicalStateNormalizer.JointRepairPublicationPreflight preflight)
    {
        if (_mortalJointAuditExpected is { } expected && CaptureMortalJointAuditCandidate(preflight) != expected)
            throw new InvalidDataException("joint_audit_candidate_changed_before_publication_take");
    }

    private MortalJointAuditPreview CaptureMortalJointAuditCandidate(CanonicalStateNormalizer.JointRepairPublicationPreflight preflight)
    {
        var plan = preflight.Validated.Plan;
        var semantic = new JsonObject
        {
            ["definitions"] = plan.DefinitionAfterImage, ["state"] = plan.StateAfterImage,
            ["history"] = plan.HistoryAfterImage, ["effectIdentity"] = plan.EffectIdentityAfterImage,
            ["woundIdentity"] = plan.WoundIdentityAfterImage, ["woundHistory"] = plan.WoundHistoryAfterImage,
            ["effects"] = JsonSerializer.SerializeToNode(plan.EffectCarrierAfterImages),
            ["owners"] = JsonSerializer.SerializeToNode(plan.OwnerCompanionAfterImages),
            ["wounds"] = JsonSerializer.SerializeToNode(plan.WoundCarrierAfterImages),
            ["pending"] = JsonSerializer.SerializeToNode(plan.PendingAfterImages),
            ["consumed"] = JsonSerializer.SerializeToNode(plan.ConsumedPaths.OrderBy(path => path, StringComparer.Ordinal))
        };
        return new(plan.InputFingerprint, plan.PreparedPlanFingerprint, preflight.Preparation.InventoryFingerprint,
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(semantic),
            (_mortalJointNarrativeInvocation ?? throw new InvalidDataException("joint_audit_actual_n_missing"))
                .CaptureAuditSourceData(JointNarrativeIssuer),
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(plan.JointRepairCommonAuthority!.Progression.Inputs.ToDiagnosticJson()),
            plan.JointRepairCommonAuthority.Progression.Inputs.Fingerprint);
    }
}
