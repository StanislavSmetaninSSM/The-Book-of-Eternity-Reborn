using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Contains only the current spiritual wound choice information safe to show the GM.
    /// It does not carry a source, packet, image, journal or owner capability.
    /// </summary>
    /// <param name="OpportunityRef">
    /// Opaque public reference required in the one decision response.
    /// </param>
    /// <param name="MinimumSeverityRank">
    /// Lowest severity accepted for materialization, including a worsening target's current rank.
    /// </param>
    /// <param name="RequiredSeverityRank">
    /// Exact rank required by a guaranteed source, or <see langword="null"/> for optional harm.
    /// </param>
    /// <param name="MaximumSeverityRank">
    /// Highest severity rank admitted by the current source.
    /// </param>
    /// <param name="Target">
    /// Readable target description without a private actor coordinate.
    /// </param>
    /// <param name="Cause">
    /// Readable cause of the opportunity.
    /// </param>
    /// <param name="AllowedLocationKinds">
    /// Closed location-kind vocabulary available to this wound.
    /// </param>
    /// <param name="AllowedDecisions">
    /// Closed response vocabulary for the current opportunity.
    /// </param>
    internal sealed record SpiritualC2PrivateOffer(
        string OpportunityRef, int MinimumSeverityRank, int? RequiredSeverityRank,
        int MaximumSeverityRank,
        string Target, string Cause, IReadOnlyList<string> AllowedLocationKinds,
        IReadOnlyList<string> AllowedDecisions);

    /// <summary>
    /// Reports one private C2 adapter boundary without publishing accepted state.
    /// </summary>
    /// <param name="Disposition">
    /// Offer, dependent_continuation, completed_unpublished, repair_required or blocked.
    /// </param>
    /// <param name="Session">
    /// Retained private owner for an offer, saved dependent selection or completed frontier;
    /// the caller must dispose it.
    /// </param>
    /// <param name="Issues">
    /// Validation or transport failures, or dependent-draft diagnostics alongside a retained session.
    /// </param>
    internal sealed record SpiritualC2PrivateOpenResult(
        string Disposition, SpiritualC2PrivateSession? Session,
        IReadOnlyList<ValidationIssue> Issues);

    /// <summary>
    /// Owns one matched C2 pair and at most one offered decision until its next transport.
    /// The session is an internal capability and must never be serialized to the GM.
    /// </summary>
    internal sealed partial class SpiritualC2PrivateSession : IDisposable
    {
        private readonly ValidationService _validator;
        private SpiritualOriginalTurnCapture _capture;
        private readonly AcceptedMechanicsPlanner.SpiritualExchangeInterval? _interval;
        private readonly PreparedSpiritualSource? _source;
        private readonly WoundAcceptedTurnBinding? _binding;
        private readonly WoundOpportunityAuthority? _opportunity;
        private readonly SemaphoreSlim _submitGate = new(1, 1);
        private bool _used;
        private bool _disposed;

        /// <summary>
        /// Retains the exact owner and one optional offer after matched-pair classification.
        /// </summary>
        /// <param name="validator">
        /// Validator that reopens the next pair after a confirmed transport.
        /// </param>
        /// <param name="capture">
        /// Fresh owner retained by the checkpoint-first classifier.
        /// </param>
        /// <param name="interval">
        /// Current closed exchange interval, or <see langword="null"/> at completion.
        /// </param>
        /// <param name="source">
        /// Current positive source, or <see langword="null"/> at completion.
        /// </param>
        /// <param name="binding">
        /// Ordinary wound event binding for the current offer, if any.
        /// </param>
        /// <param name="opportunity">
        /// Ordinary wound authority for the current offer, if any.
        /// </param>
        internal SpiritualC2PrivateSession(ValidationService validator,
            SpiritualOriginalTurnCapture capture,
            AcceptedMechanicsPlanner.SpiritualExchangeInterval? interval,
            PreparedSpiritualSource? source, WoundAcceptedTurnBinding? binding,
            WoundOpportunityAuthority? opportunity)
        {
            _validator = validator;
            _capture = capture;
            _interval = interval;
            _source = source;
            _binding = binding;
            _opportunity = opportunity;
            Offer = opportunity is null ? null : ProjectC2SafeOffer(opportunity);
        }

        /// <summary>
        /// Gets the allowlisted current offer, or <see langword="null"/> for a saved selection or completed frontier.
        /// </summary>
        internal SpiritualC2PrivateOffer? Offer { get; }

        /// <summary>
        /// Continues the exact saved selection after a fresh owner-derived dependency comparison, without another decision.
        /// The transport rechecks that policy against the exact input image it executes.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease retained through physical rechecks and checkpoint advancement.
        /// </param>
        /// <returns>
        /// A next offer, completed unpublished frontier, retained dependent boundary or rejection.
        /// </returns>
        internal async Task<SpiritualC2PrivateOpenResult> ResumeDependentContinuationAsync(
            FileSystemManager.CanonicalWriteLease lease)
        {
            await _submitGate.WaitAsync();
            try
            {
                if (_disposed || _used || Offer is not null || !_capture.HasC2PendingSubmission)
                    return AdapterFailure("spiritual_c2_private_submission_unavailable");
                var dependent = _capture.NeedsSequentialDependentContext
                    ? await ReadSequentialDependentContextAsync(lease)
                    : await _capture.ReadC2DependentContextAsync(lease);
                if (dependent?.DraftPolicy is not { } policy)
                {
                    Dispose();
                    return AdapterFailure("spiritual_c2_dependent_correction_invalid");
                }
                _used = true;
                var transported = await _capture.CommitC2SavedTransportAsync(lease, dependentPolicy: policy);
                Dispose();
                if (transported.Disposition != "committed")
                    return new(transported.RequiresDependentContinuation ? "dependent_continuation" :
                        transported.Disposition == "repair_required" ? "repair_required" : "blocked",
                        null, transported.Issues);
                return await _validator.OpenC2PrivateSessionAsync(lease);
            }
            finally { _submitGate.Release(); }
        }

        /// <summary>
        /// Composes one response through the ordinary wound authority and saves its owner-derived successor.
        /// </summary>
        /// <param name="lease">
        /// Active canonical write lease retained through pair checks, command write and transport.
        /// </param>
        /// <param name="decision">
        /// One GM wound decision for <see cref="Offer"/>; source and packet fields are not accepted.
        /// </param>
        /// <param name="finalSceneText">
        /// Optional GM scene text validated by the ordinary command composer.
        /// </param>
        /// <param name="afterCommandWriteAsync">
        /// Optional internal test hook after command publication and before saved transport.
        /// </param>
        /// <returns>
        /// Next private offer, completed unpublished owner, repair disposition or rejection.
        /// </returns>
        internal async Task<SpiritualC2PrivateOpenResult> SubmitDecisionAsync(
            FileSystemManager.CanonicalWriteLease lease, JsonElement decision,
            string? finalSceneText,
            Func<FileSystemManager.CanonicalWriteLease, Task>? afterCommandWriteAsync = null)
        {
            await _submitGate.WaitAsync();
            try
            {
                if (_disposed || _used || _interval is null || _source is null ||
                    _binding is null || _opportunity is null)
                    return AdapterFailure("spiritual_c2_private_session_unavailable");
                var next = await _capture.ReadC2NextSourceAsync(lease);
                if (next.Interval is null || next.Source is null || next.Issues.Count != 0 ||
                    !ReferenceEquals(next.Interval, _interval) ||
                    !ReferenceEquals(next.Source, _source))
                    return new("blocked", null, next.Issues.Count != 0
                        ? next.Issues : AdapterFailure("spiritual_c2_private_offer_stale").Issues);
                var composed = WoundResponseInputComposer.Compose(_binding,
                    [_opportunity], [decision], finalSceneText, []);
                if (!composed.Success || composed.CommandRoot is null)
                    return new("blocked", null, composed.Issues);
                _used = true;
                var commandBytes = Encoding.UTF8.GetBytes(composed.CommandRoot.ToJsonString());
                try
                {
                    await _validator._fs.WriteFileAtomicBytesAsync(lease,
                        AcceptedMechanicsPlan.WoundCommandPath, commandBytes);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    Dispose();
                    return AdapterFailure("spiritual_c2_private_command_write_failed");
                }
                if (afterCommandWriteAsync is not null)
                    await afterCommandWriteAsync(lease);
                var transported = await _capture.CommitC2SavedTransportAsync(lease,
                    expectedCommandBytes: commandBytes);
                if (transported.Disposition != "committed")
                {
                    Dispose();
                    return new(transported.RequiresDependentContinuation
                        ? "dependent_continuation"
                        : transported.Disposition == "repair_required"
                            ? "repair_required" : "blocked", null, transported.Issues);
                }
                Dispose();
                return await _validator.OpenC2PrivateSessionAsync(lease);
            }
            finally { _submitGate.Release(); }
        }

        /// <summary>
        /// Revokes the unpublished owner and any unconsumed decision authority.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _capture.Dispose();
        }
    }

    /// <summary>
    /// Projects only the allowed decision vocabulary and severity bounds of one owner-minted wound.
    /// </summary>
    /// <param name="opportunity">
    /// Current owner-derived opportunity whose private provenance stays inside the adapter.
    /// </param>
    /// <returns>
    /// Visibility-safe offer with the exact guarantee and worsening lower bound, if present.
    /// </returns>
    internal static SpiritualC2PrivateOffer ProjectC2SafeOffer(
        WoundOpportunityAuthority opportunity)
    {
        var minimum = Math.Max(opportunity.MinimumSeverityRank ?? 1,
            (opportunity.WorseningTarget?.Wound.Severity.Rank ?? 0) + 1);
        return new SpiritualC2PrivateOffer(
            opportunity.PublicRef, minimum, opportunity.RequiredSeverityRank,
            opportunity.MaximumSeverityRank, opportunity.SafeContext.Target,
            opportunity.SafeContext.Cause,
            Array.AsReadOnly(opportunity.SafeContext.AllowedLocationKinds.ToArray()),
            Array.AsReadOnly(opportunity.RequiredSeverityRank is not null
                ? new[] { "materialize" }
                : minimum <= opportunity.MaximumSeverityRank
                    ? new[] { "none", "materialize" }
                    : new[] { "none" }));
    }

    /// <summary>
    /// Repairs the derived pending projection before exposing the current owner-bound offer.
    /// </summary>
    /// <param name="lease">
    /// Active canonical lease used to replay, repair and read the exact current frontier.
    /// </param>
    /// <returns>
    /// A private offer, completed unpublished owner or a fail-closed disposition.
    /// </returns>
    internal async Task<SpiritualC2PrivateOpenResult> OpenC2PrivateSessionAsync(
        FileSystemManager.CanonicalWriteLease lease)
    {
        var repaired = await RepairSpiritualPendingAsync(lease);
        if (repaired.Disposition == "no_checkpoint")
            return AdapterFailure("spiritual_c2_private_checkpoint_required");
        if (repaired.Disposition is not ("match" or "repaired") ||
            repaired.Capture is not { } capture)
            return new(repaired.Disposition == "not_committed"
                ? "repair_required" : "blocked", null,
                repaired.Issues.Count != 0 ? repaired.Issues :
                    AdapterFailure("spiritual_c2_private_repair_invalid").Issues);
        return await OpenC2ClassifiedSessionAsync(lease, capture, allowAutomaticTransport: true);
    }

    /// <summary>
    /// Opens a genuinely classified owner, optionally retaining automatic outcomes without saving them.
    /// </summary>
    /// <param name="lease">
    /// Active canonical lease covering the matched pair and original source inputs.
    /// </param>
    /// <param name="capture">
    /// Fresh matched owner; this method disposes it unless the result retains a session.
    /// </param>
    /// <param name="allowAutomaticTransport">
    /// Whether to save and advance an already satisfied guarantee through the ordinary execution path.
    /// </param>
    /// <returns>
    /// A retained private boundary or a failure with no retained owner.
    /// </returns>
    private async Task<SpiritualC2PrivateOpenResult> OpenC2ClassifiedSessionAsync(
        FileSystemManager.CanonicalWriteLease lease, SpiritualOriginalTurnCapture capture,
        bool allowAutomaticTransport)
    {
        var retained = false;
        try
        {
            if (!capture.HasC2PendingSubmission)
            {
                var commandIssues = await capture.CheckC2ColdCommittedCommandAsync(lease);
                if (commandIssues.Count != 0)
                    return new("blocked", null, commandIssues);
            }
            if (capture.HasC2PendingSubmission)
            {
                var dependentIssues = await capture.ReadC2DependentIssuesAsync(lease);
                if (!capture.IsCurrentOwner || dependentIssues.Count != 0 &&
                    !SpiritualOriginalTurnCapture.IsCorrectableDependentConflictFailure(dependentIssues) &&
                    !capture.CanInspectBindingTerminalControl(dependentIssues))
                    return new("blocked", null, dependentIssues);
                retained = true;
                return new("dependent_continuation",
                    new SpiritualC2PrivateSession(this, capture, null, null, null, null), dependentIssues);
            }
            var next = await capture.ReadC2NextSourceAsync(lease);
            if (next.Source is null || next.Interval is null || next.Issues.Count != 0)
            {
                if (next.Issues.Count == 1 &&
                    next.Issues[0].Code == "spiritual_c2_next_source_missing")
                {
                    var complete = await capture.CheckC2CompletedFrontierAsync(lease);
                    if (complete.Count == 0)
                    {
                        retained = true;
                        return new("completed_unpublished",
                            new SpiritualC2PrivateSession(this, capture, null, null, null, null), []);
                    }
                    return new("blocked", null, complete);
                }
                return new("blocked", null, next.Issues);
            }
            var admission = await capture.AdmitWoundSourceAsync(lease, next.Interval, next.Source);
            if (admission.Admission is null || admission.Issues.Count != 0)
                return new("blocked", null, admission.Issues.Count != 0
                    ? admission.Issues : AdapterFailure("spiritual_c2_private_admission_missing").Issues);
            var offered = await capture.ReadWoundOpportunityAsync(lease, admission.Admission);
            if (offered.Satisfaction is not null && offered.Issues.Count == 0)
            {
                if (!allowAutomaticTransport)
                {
                    retained = true;
                    return new("automatic_continuation",
                        new SpiritualC2PrivateSession(this, capture, null, null, null, null), []);
                }
                var transported = await capture.CommitC2SavedTransportAsync(lease);
                if (transported.Disposition != "committed")
                    return new(transported.RequiresDependentContinuation
                        ? "dependent_continuation"
                        : transported.Disposition == "repair_required"
                            ? "repair_required" : "blocked", null, transported.Issues);
                capture.Dispose();
                return await OpenC2PrivateSessionAsync(lease);
            }
            if (offered.Binding is null || offered.Opportunity is null ||
                offered.Issues.Count != 0)
                return new("blocked", null, offered.Issues.Count != 0
                    ? offered.Issues : AdapterFailure("spiritual_c2_private_offer_missing").Issues);
            retained = true;
            return new("offer", new SpiritualC2PrivateSession(this, capture,
                next.Interval, next.Source, offered.Binding, offered.Opportunity), []);
        }
        finally
        {
            if (!retained)
                capture.Dispose();
        }
    }

    private static SpiritualC2PrivateOpenResult AdapterFailure(string code) =>
        new("blocked", null,
        [
            new ValidationIssue(SpiritualWoundCaptureCheckpointState.StatePath,
                IssueSeverity.Error, "The current private spiritual decision is unavailable.",
                code: code, section: "AcceptedTurnWoundMaterialization")
        ]);

    internal sealed partial class SpiritualOriginalTurnCapture
    {
        /// <summary>
        /// Rejects an unexplained physical command before a cold session exposes an offer or completion.
        /// Live submission checks remain separate because they intentionally replace the committed command.
        /// </summary>
        /// <param name="lease">
        /// Active lease covering the owner-matched checkpoint and current command image.
        /// </param>
        /// <returns>
        /// Empty issues only when the physical command equals the genuinely replayed committed input.
        /// </returns>
        internal async Task<IReadOnlyList<ValidationIssue>> CheckC2ColdCommittedCommandAsync(
            FileSystemManager.CanonicalWriteLease lease)
        {
            await _gate.WaitAsync();
            try
            {
                EnsureCurrent(lease);
                if (_matchedC2Pair is not { } pair || _c2PendingSubmission)
                    throw new InvalidOperationException("An unselected matched checkpoint is required.");
                var freshness = await CheckRetainedInputsAsync(lease);
                if (freshness.Count != 0)
                    return freshness;
                var parsed = SpiritualWoundCaptureCheckpointState.Parse(
                    DecodePhysicalRoot(pair.CheckpointBytes),
                    SpiritualWoundCaptureCheckpointState.StatePath, _draftInputs.PathInventory);
                if (!parsed.IsValid || parsed.State is not { } checkpoint || checkpoint.HasPendingSubmission)
                    throw new InvalidOperationException("The committed command owner is unavailable.");
                var expected = ReadC2CommittedInputLayer(checkpoint)[AcceptedMechanicsPlan.WoundCommandPath];
                var bytes = await _validator._fs.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath);
                if (!SameExactImage(expected, new CanonicalBeforeImage(bytes is not null, bytes)))
                    throw new InvalidOperationException("The physical command has no retained selection.");
                return [];
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                InvalidOperationException or FormatException or JsonException or OverflowException)
            {
                if (IsCurrentOwner)
                    RevokeUnderLease(lease);
                return AdapterFailure("spiritual_c2_private_committed_command_changed").Issues;
            }
            finally { _gate.Release(); }
        }

        /// <summary>
        /// Rechecks an exhausted cursor and its retained ordinary reduction against the original source frontier.
        /// </summary>
        /// <param name="lease">
        /// Active lease for the exact physical pair and current original owners.
        /// </param>
        /// <returns>
        /// Empty issues only when no source remains and the completed ordinary reduction is retained.
        /// </returns>
        internal async Task<IReadOnlyList<ValidationIssue>> CheckC2CompletedFrontierAsync(
            FileSystemManager.CanonicalWriteLease lease)
        {
            await _gate.WaitAsync();
            try
            {
                var next = await ReadC2NextSourceCoreAsync(lease);
                if (next.Issues.Count != 1 ||
                    next.Issues[0].Code != "spiritual_c2_next_source_missing" ||
                    _resources is null || _effects is null)
                    return next.Issues.Count != 0 ? next.Issues :
                        AdapterFailure("spiritual_c2_private_completion_not_ready").Issues;
                if (_completedOrdinaryReduction?.Reduction is null)
                    return AdapterFailure("spiritual_c2_private_completion_not_ready").Issues;
                return await _source.CheckCompletionInputsAsync(lease,
                    _nextResourceOrdinal, _resources, _effects);
            }
            finally { _gate.Release(); }
        }
    }
}
